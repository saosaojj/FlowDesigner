using System.Collections.Concurrent;
using System.Diagnostics;

namespace FlowDesigner.Api.Services;

public class PerformanceMonitor
{
    private readonly ConcurrentDictionary<string, FlowMetrics> _flowMetrics = new();
    private readonly ConcurrentDictionary<string, NodeMetrics> _nodeMetrics = new();
    private readonly ConcurrentQueue<SystemMetric> _systemMetrics = new();
    private readonly Timer _monitoringTimer;
    private readonly ILogger<PerformanceMonitor> _logger;
    
    private long _totalMessagesProcessed;
    private long _totalExecutionTime;
    private long _totalErrors;
    
    public PerformanceMonitor(ILogger<PerformanceMonitor> logger)
    {
        _logger = logger;
        _monitoringTimer = new Timer(CollectSystemMetrics, null, 
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }
    
    public void RecordFlowStart(string flowId)
    {
        var metrics = _flowMetrics.GetOrAdd(flowId, _ => new FlowMetrics());
        Interlocked.Increment(ref metrics.StartCount);
        metrics.LastStartTime = DateTime.UtcNow;
    }
    
    public void RecordFlowEnd(string flowId, long executionTimeMs, bool success)
    {
        if (_flowMetrics.TryGetValue(flowId, out var metrics))
        {
            Interlocked.Increment(ref metrics.EndCount);
            Interlocked.Add(ref metrics.TotalExecutionTime, executionTimeMs);
            
            if (!success)
                Interlocked.Increment(ref metrics.ErrorCount);
            
            metrics.LastEndTime = DateTime.UtcNow;
            metrics.LastExecutionTime = executionTimeMs;
            
            var total = metrics.EndCount;
            if (total > 0)
                metrics.AverageExecutionTime = metrics.TotalExecutionTime / total;
        }
        
        Interlocked.Increment(ref _totalMessagesProcessed);
        Interlocked.Add(ref _totalExecutionTime, executionTimeMs);
        
        if (!success)
            Interlocked.Increment(ref _totalErrors);
    }
    
    public void RecordNodeExecution(string nodeId, string nodeType, long executionTimeMs, bool success)
    {
        var metrics = _nodeMetrics.GetOrAdd(nodeId, _ => new NodeMetrics
        {
            NodeType = nodeType
        });
        
        Interlocked.Increment(ref metrics.ExecutionCount);
        Interlocked.Add(ref metrics.TotalExecutionTime, executionTimeMs);
        
        if (!success)
            Interlocked.Increment(ref metrics.ErrorCount);
        
        var count = metrics.ExecutionCount;
        if (count > 0)
            metrics.AverageExecutionTime = metrics.TotalExecutionTime / count;
        
        metrics.LastExecutionTime = executionTimeMs;
    }
    
    public FlowMetrics? GetFlowMetrics(string flowId)
    {
        _flowMetrics.TryGetValue(flowId, out var metrics);
        return metrics;
    }
    
    public NodeMetrics? GetNodeMetrics(string nodeId)
    {
        _nodeMetrics.TryGetValue(nodeId, out var metrics);
        return metrics;
    }
    
    public Dictionary<string, FlowMetrics> GetAllFlowMetrics()
    {
        return new Dictionary<string, FlowMetrics>(_flowMetrics);
    }
    
    public Dictionary<string, NodeMetrics> GetAllNodeMetrics()
    {
        return new Dictionary<string, NodeMetrics>(_nodeMetrics);
    }
    
    public SystemStatistics GetSystemStatistics()
    {
        var process = Process.GetCurrentProcess();
        
        return new SystemStatistics
        {
            TotalMessagesProcessed = Interlocked.Read(ref _totalMessagesProcessed),
            TotalExecutionTime = Interlocked.Read(ref _totalExecutionTime),
            TotalErrors = Interlocked.Read(ref _totalErrors),
            AverageExecutionTime = _totalMessagesProcessed > 0 
                ? _totalExecutionTime / _totalMessagesProcessed 
                : 0,
            CpuUsage = GetCpuUsage(),
            MemoryUsage = process.WorkingSet64,
            ThreadCount = process.Threads.Count,
            HandleCount = process.HandleCount,
            Timestamp = DateTime.UtcNow
        };
    }
    
    public List<SystemMetric> GetRecentSystemMetrics(int count = 60)
    {
        return _systemMetrics.Take(count).ToList();
    }
    
    public void ResetMetrics()
    {
        _flowMetrics.Clear();
        _nodeMetrics.Clear();
        _systemMetrics.Clear();
        _totalMessagesProcessed = 0;
        _totalExecutionTime = 0;
        _totalErrors = 0;
    }
    
    private void CollectSystemMetrics(object? state)
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var metric = new SystemMetric
            {
                CpuUsage = GetCpuUsage(),
                MemoryUsage = process.WorkingSet64,
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount,
                MessagesPerSecond = CalculateMessagesPerSecond(),
                Timestamp = DateTime.UtcNow
            };
            
            _systemMetrics.Enqueue(metric);
            
            while (_systemMetrics.Count > 3600)
            {
                _systemMetrics.TryDequeue(out _);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect system metrics");
        }
    }
    
    private double GetCpuUsage()
    {
        return 0.0;
    }
    
    private double CalculateMessagesPerSecond()
    {
        return 0.0;
    }
}

public class FlowMetrics
{
    public long StartCount;
    public long EndCount;
    public long ErrorCount;
    public long TotalExecutionTime;
    public long LastExecutionTime;
    public long AverageExecutionTime;
    public DateTime LastStartTime;
    public DateTime LastEndTime;
}

public class NodeMetrics
{
    public string NodeType = string.Empty;
    public long ExecutionCount;
    public long ErrorCount;
    public long TotalExecutionTime;
    public long LastExecutionTime;
    public long AverageExecutionTime;
}

public class SystemMetric
{
    public double CpuUsage { get; set; }
    public long MemoryUsage { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public double MessagesPerSecond { get; set; }
    public DateTime Timestamp { get; set; }
}

public class SystemStatistics
{
    public long TotalMessagesProcessed { get; set; }
    public long TotalExecutionTime { get; set; }
    public long TotalErrors { get; set; }
    public long AverageExecutionTime { get; set; }
    public double CpuUsage { get; set; }
    public long MemoryUsage { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public DateTime Timestamp { get; set; }
}
