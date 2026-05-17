using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using FlowDesigner.Shared.Models;

namespace FlowDesigner.Api.Services;

public class HighPerformanceExecutionEngine : IDisposable
{
    private readonly FlowService _flowService;
    private readonly NodeRegistryService _nodeRegistry;
    private readonly PerformanceMonitor _performanceMonitor;
    private readonly ILogger<HighPerformanceExecutionEngine> _logger;
    
    private readonly ConcurrentDictionary<string, FlowRuntimeContext> _runtimeContexts = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _flowSemaphores = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _flowCancellations = new();
    
    private readonly Channel<ExecutionTask> _executionQueue;
    private readonly Channel<MessageTask> _messageQueue;
    
    private readonly ThreadPoolManager _threadPoolManager;
    private readonly MessageRouter _messageRouter;
    private readonly ResourcePool _resourcePool;
    
    private readonly int _maxConcurrency;
    private readonly int _maxQueueSize;
    private readonly TimeSpan _defaultTimeout;
    
    private readonly Task[] _executorTasks;
    private readonly Task[] _messageProcessorTasks;
    private readonly CancellationTokenSource _globalCancellation;
    
    private bool _disposed;

    public HighPerformanceExecutionEngine(
        FlowService flowService,
        NodeRegistryService nodeRegistry,
        PerformanceMonitor performanceMonitor,
        ILogger<HighPerformanceExecutionEngine> logger,
        IConfiguration configuration)
    {
        _flowService = flowService;
        _nodeRegistry = nodeRegistry;
        _performanceMonitor = performanceMonitor;
        _logger = logger;
        
        _maxConcurrency = configuration.GetValue("Execution:MaxConcurrency", 100);
        _maxQueueSize = configuration.GetValue("Execution:MaxQueueSize", 10000);
        _defaultTimeout = TimeSpan.FromSeconds(30);
        
        var channelOptions = new BoundedChannelOptions(_maxQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };
        
        _executionQueue = Channel.CreateBounded<ExecutionTask>(channelOptions);
        _messageQueue = Channel.CreateBounded<MessageTask>(channelOptions);
        
        _threadPoolManager = new ThreadPoolManager(_maxConcurrency);
        _messageRouter = new MessageRouter();
        _resourcePool = new ResourcePool();
        
        _globalCancellation = new CancellationTokenSource();
        
        var executorCount = Math.Max(4, Environment.ProcessorCount);
        _executorTasks = new Task[executorCount];
        for (int i = 0; i < executorCount; i++)
        {
            _executorTasks[i] = ProcessExecutionQueue(_globalCancellation.Token);
        }
        
        var processorCount = Math.Max(2, Environment.ProcessorCount / 2);
        _messageProcessorTasks = new Task[processorCount];
        for (int i = 0; i < processorCount; i++)
        {
            _messageProcessorTasks[i] = ProcessMessageQueue(_globalCancellation.Token);
        }
        
        _logger.LogInformation("高性能执行引擎已初始化: 并发数={MaxConcurrency}, 队列大小={MaxQueueSize}", 
            _maxConcurrency, _maxQueueSize);
    }

    public async Task<ExecutionResult> ExecuteFlowAsync(
        string flowId, 
        FlowMessage? input = null,
        ExecutionOptions? options = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var opts = options ?? new ExecutionOptions();
        
        _performanceMonitor.RecordFlowStart(flowId);
        
        try
        {
            var flow = await _flowService.GetFlowAsync(flowId);
            if (flow == null)
            {
                return new ExecutionResult 
                { 
                    Success = false, 
                    Error = "流程不存在" 
                };
            }
            
            var context = GetOrCreateContext(flow);
            
            var semaphore = _flowSemaphores.GetOrAdd(flowId, 
                _ => new SemaphoreSlim(opts.MaxConcurrentExecutions, opts.MaxConcurrentExecutions));
            
            await semaphore.WaitAsync(opts.Timeout);
            
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(_globalCancellation.Token);
                cts.CancelAfter(opts.Timeout);
                
                var result = await ExecuteFlowInternalAsync(flow, context, input ?? new FlowMessage(), cts.Token);
                
                stopwatch.Stop();
                _performanceMonitor.RecordFlowEnd(flowId, stopwatch.ElapsedMilliseconds, result.Success);
                
                return result;
            }
            finally
            {
                semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _performanceMonitor.RecordFlowEnd(flowId, stopwatch.ElapsedMilliseconds, false);
            return new ExecutionResult 
            { 
                Success = false, 
                Error = "执行超时" 
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _performanceMonitor.RecordFlowEnd(flowId, stopwatch.ElapsedMilliseconds, false);
            _logger.LogError(ex, "流程执行失败: {FlowId}", flowId);
            return new ExecutionResult 
            { 
                Success = false, 
                Error = ex.Message 
            };
        }
    }

    public async Task<ExecutionResult> ExecuteNodeAsync(
        string flowId,
        string nodeId,
        FlowMessage? input = null,
        ExecutionOptions? options = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var opts = options ?? new ExecutionOptions();
        
        try
        {
            var flow = await _flowService.GetFlowAsync(flowId);
            if (flow == null)
            {
                return new ExecutionResult { Success = false, Error = "流程不存在" };
            }
            
            var node = flow.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null)
            {
                return new ExecutionResult { Success = false, Error = "节点不存在" };
            }
            
            var context = GetOrCreateContext(flow);
            
            var result = await ExecuteNodeInternalAsync(node, context, input ?? new FlowMessage());
            
            stopwatch.Stop();
            _performanceMonitor.RecordNodeExecution(nodeId, node.Type, stopwatch.ElapsedMilliseconds, result.Success);
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "节点执行失败: {NodeId}", nodeId);
            return new ExecutionResult { Success = false, Error = ex.Message };
        }
    }

    public async Task TriggerNodeAsync(string flowId, string nodeId, FlowMessage message)
    {
        var task = new ExecutionTask
        {
            FlowId = flowId,
            NodeId = nodeId,
            Message = message,
            CreatedAt = DateTime.UtcNow
        };
        
        await _executionQueue.Writer.WriteAsync(task);
    }

    public void StopFlow(string flowId)
    {
        if (_flowCancellations.TryRemove(flowId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
        
        if (_runtimeContexts.TryRemove(flowId, out var context))
        {
            context.Dispose();
        }
        
        _logger.LogInformation("流程已停止: {FlowId}", flowId);
    }

    public ExecutionFlowStatus GetFlowStatus(string flowId)
    {
        if (_runtimeContexts.TryGetValue(flowId, out var context))
        {
            return new ExecutionFlowStatus
            {
                FlowId = flowId,
                IsRunning = context.IsRunning,
                ActiveExecutions = context.ActiveExecutions,
                TotalExecutions = context.TotalExecutions,
                LastExecutionTime = context.LastExecutionTime,
                QueueSize = _executionQueue.Reader.Count
            };
        }
        
        return new ExecutionFlowStatus
        {
            FlowId = flowId,
            IsRunning = false
        };
    }

    public EngineStatistics GetStatistics()
    {
        return new EngineStatistics
        {
            ActiveFlows = _runtimeContexts.Count,
            QueueSize = _executionQueue.Reader.Count,
            MessageQueueSize = _messageQueue.Reader.Count,
            AvailableThreads = _threadPoolManager.AvailableThreads,
            TotalExecutions = _runtimeContexts.Values.Sum(c => c.TotalExecutions),
            MemoryUsage = GC.GetTotalMemory(false)
        };
    }

    private FlowRuntimeContext GetOrCreateContext(Flow flow)
    {
        return _runtimeContexts.GetOrAdd(flow.Id, _ => new FlowRuntimeContext(flow));
    }

    private async Task<ExecutionResult> ExecuteFlowInternalAsync(
        Flow flow,
        FlowRuntimeContext context,
        FlowMessage input,
        CancellationToken cancellationToken)
    {
        var outputs = new ConcurrentBag<FlowMessage>();
        var errors = new ConcurrentBag<string>();
        
        context.IncrementActiveExecutions();
        
        try
        {
            var startNodes = flow.Nodes
                .Where(n => n.Type == "inject" || n.Inputs.Count == 0)
                .ToList();
            
            var tasks = startNodes.Select(node => 
                ExecuteNodeWithDependenciesAsync(flow, node, context, input, outputs, errors, cancellationToken));
            
            await Task.WhenAll(tasks);
            
            return new ExecutionResult
            {
                Success = errors.IsEmpty,
                Outputs = outputs.ToList(),
                Error = errors.IsEmpty ? null : string.Join("; ", errors)
            };
        }
        finally
        {
            context.DecrementActiveExecutions();
        }
    }

    private async Task ExecuteNodeWithDependenciesAsync(
        Flow flow,
        FlowNode node,
        FlowRuntimeContext context,
        FlowMessage input,
        ConcurrentBag<FlowMessage> outputs,
        ConcurrentBag<string> errors,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<string>();
        await ExecuteNodeRecursiveAsync(flow, node, context, input, outputs, errors, visited, cancellationToken);
    }

    private async Task ExecuteNodeRecursiveAsync(
        Flow flow,
        FlowNode node,
        FlowRuntimeContext context,
        FlowMessage input,
        ConcurrentBag<FlowMessage> outputs,
        ConcurrentBag<string> errors,
        HashSet<string> visited,
        CancellationToken cancellationToken)
    {
        if (visited.Contains(node.Id) || cancellationToken.IsCancellationRequested)
            return;
        
        visited.Add(node.Id);
        
        try
        {
            var result = await ExecuteNodeInternalAsync(node, context, input);
            
            if (result.Success)
            {
                foreach (var output in result.Outputs)
                {
                    outputs.Add(output);
                    
                    var nextConnections = flow.Connections
                        .Where(c => c.SourceNodeId == node.Id)
                        .ToList();
                    
                    var nextTasks = nextConnections.Select(conn =>
                    {
                        var nextNode = flow.Nodes.FirstOrDefault(n => n.Id == conn.TargetNodeId);
                        if (nextNode != null)
                        {
                            return ExecuteNodeRecursiveAsync(flow, nextNode, context, output, 
                                outputs, errors, visited, cancellationToken);
                        }
                        return Task.CompletedTask;
                    });
                    
                    await Task.WhenAll(nextTasks);
                }
            }
            else
            {
                errors.Add(result.Error ?? $"节点 {node.Id} 执行失败");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"节点 {node.Id} 异常: {ex.Message}");
        }
    }

    private async Task<ExecutionResult> ExecuteNodeInternalAsync(
        FlowNode node,
        FlowRuntimeContext context,
        FlowMessage input)
    {
        var output = new FlowMessage
        {
            Payload = input.Payload,
            Topic = input.Topic,
            Metadata = new Dictionary<string, object?>(input.Metadata ?? new Dictionary<string, object?>())
        };
        
        switch (node.Type)
        {
            case "inject":
                output.Payload = node.Properties.GetValueOrDefault("payload") ?? "Hello World";
                output.Topic = node.Properties.GetValueOrDefault("topic")?.ToString() ?? "";
                break;
                
            case "debug":
                _logger.LogInformation("Debug [{NodeId}]: {Payload}", node.Id, output.Payload);
                break;
                
            case "function":
                output = await ExecuteFunctionNodeAsync(node, input);
                break;
                
            case "delay":
                var timeout = Convert.ToInt32(node.Properties.GetValueOrDefault("timeout") ?? 1000);
                await Task.Delay(timeout);
                break;
                
            case "modbus-read":
            case "modbus-write":
            case "s7-read":
            case "s7-write":
                output = await ExecutePlcNodeAsync(node, input);
                break;
                
            default:
                break;
        }
        
        return new ExecutionResult
        {
            Success = true,
            Outputs = new List<FlowMessage> { output }
        };
    }

    private Task<FlowMessage> ExecuteFunctionNodeAsync(FlowNode node, FlowMessage input)
    {
        var output = new FlowMessage
        {
            Payload = input.Payload,
            Topic = input.Topic,
            Metadata = new Dictionary<string, object?>(input.Metadata ?? new Dictionary<string, object?>())
        };
        
        return Task.FromResult(output);
    }

    private Task<FlowMessage> ExecutePlcNodeAsync(FlowNode node, FlowMessage input)
    {
        var output = new FlowMessage
        {
            Payload = input.Payload,
            Topic = input.Topic,
            Metadata = new Dictionary<string, object?>(input.Metadata ?? new Dictionary<string, object?>())
        };
        
        return Task.FromResult(output);
    }

    private async Task ProcessExecutionQueue(CancellationToken cancellationToken)
    {
        await foreach (var task in _executionQueue.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                var flow = await _flowService.GetFlowAsync(task.FlowId);
                if (flow == null) continue;
                
                var node = flow.Nodes.FirstOrDefault(n => n.Id == task.NodeId);
                if (node == null) continue;
                
                var context = GetOrCreateContext(flow);
                
                await ExecuteNodeInternalAsync(node, context, task.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "执行队列任务失败");
            }
        }
    }

    private async Task ProcessMessageQueue(CancellationToken cancellationToken)
    {
        await foreach (var task in _messageQueue.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                _messageRouter.Route(task);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "消息队列处理失败");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _globalCancellation.Cancel();
        
        Task.WaitAll(_executorTasks, TimeSpan.FromSeconds(5));
        Task.WaitAll(_messageProcessorTasks, TimeSpan.FromSeconds(5));
        
        _globalCancellation.Dispose();
        
        foreach (var semaphore in _flowSemaphores.Values)
        {
            semaphore.Dispose();
        }
        
        foreach (var cts in _flowCancellations.Values)
        {
            cts.Dispose();
        }
        
        foreach (var context in _runtimeContexts.Values)
        {
            context.Dispose();
        }
        
        _threadPoolManager.Dispose();
        _resourcePool.Dispose();
        
        _logger.LogInformation("高性能执行引擎已关闭");
    }
}

public class ExecutionTask
{
    public string FlowId { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public FlowMessage Message { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public class MessageTask
{
    public string TargetNodeId { get; set; } = string.Empty;
    public FlowMessage Message { get; set; } = null!;
    public int Priority { get; set; }
}

public class ExecutionOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxConcurrentExecutions { get; set; } = 10;
    public bool EnableCaching { get; set; } = true;
    public bool EnableProfiling { get; set; } = false;
}

public class FlowRuntimeContext : IDisposable
{
    private int _activeExecutions;
    private long _totalExecutions;
    
    public Flow Flow { get; }
    public bool IsRunning { get; private set; }
    public DateTime LastExecutionTime { get; private set; }
    
    public int ActiveExecutions => _activeExecutions;
    public long TotalExecutions => _totalExecutions;
    
    public FlowRuntimeContext(Flow flow)
    {
        Flow = flow;
        IsRunning = true;
    }
    
    public void IncrementActiveExecutions()
    {
        Interlocked.Increment(ref _activeExecutions);
        Interlocked.Increment(ref _totalExecutions);
        LastExecutionTime = DateTime.UtcNow;
    }
    
    public void DecrementActiveExecutions()
    {
        Interlocked.Decrement(ref _activeExecutions);
    }
    
    public void Dispose()
    {
        IsRunning = false;
    }
}

public class ExecutionFlowStatus
{
    public string FlowId { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public int ActiveExecutions { get; set; }
    public long TotalExecutions { get; set; }
    public DateTime LastExecutionTime { get; set; }
    public int QueueSize { get; set; }
}

public class EngineStatistics
{
    public int ActiveFlows { get; set; }
    public int QueueSize { get; set; }
    public int MessageQueueSize { get; set; }
    public int AvailableThreads { get; set; }
    public long TotalExecutions { get; set; }
    public long MemoryUsage { get; set; }
}

public class ThreadPoolManager : IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    
    public ThreadPoolManager(int maxThreads)
    {
        _semaphore = new SemaphoreSlim(maxThreads, maxThreads);
    }
    
    public int AvailableThreads => _semaphore.CurrentCount;
    
    public async Task<IDisposable> AcquireThreadAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        return new ThreadReleaser(_semaphore);
    }
    
    public void Dispose()
    {
        _semaphore.Dispose();
    }
    
    private class ThreadReleaser : IDisposable
    {
        private readonly SemaphoreSlim _sem;
        
        public ThreadReleaser(SemaphoreSlim sem)
        {
            _sem = sem;
        }
        
        public void Dispose()
        {
            _sem.Release();
        }
    }
}

public class MessageRouter
{
    private readonly ConcurrentDictionary<string, List<Action<FlowMessage>>> _subscribers = new();
    
    public void Subscribe(string nodeId, Action<FlowMessage> handler)
    {
        var handlers = _subscribers.GetOrAdd(nodeId, _ => new List<Action<FlowMessage>>());
        lock (handlers)
        {
            handlers.Add(handler);
        }
    }
    
    public void Unsubscribe(string nodeId, Action<FlowMessage> handler)
    {
        if (_subscribers.TryGetValue(nodeId, out var handlers))
        {
            lock (handlers)
            {
                handlers.Remove(handler);
            }
        }
    }
    
    public void Route(MessageTask task)
    {
        if (_subscribers.TryGetValue(task.TargetNodeId, out var handlers))
        {
            foreach (var handler in handlers.ToArray())
            {
                handler(task.Message);
            }
        }
    }
}

public class ResourcePool : IDisposable
{
    private readonly ConcurrentDictionary<Type, ConcurrentBag<object>> _pools = new();
    
    public T Get<T>() where T : class, new()
    {
        var pool = _pools.GetOrAdd(typeof(T), _ => new ConcurrentBag<object>());
        
        if (pool.TryTake(out var item))
        {
            return (T)item;
        }
        
        return new T();
    }
    
    public void Return<T>(T item) where T : class
    {
        var pool = _pools.GetOrAdd(typeof(T), _ => new ConcurrentBag<object>());
        pool.Add(item);
    }
    
    public void Dispose()
    {
        _pools.Clear();
    }
}
