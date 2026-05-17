using FlowDesigner.Shared.Models;
using System.Collections.Concurrent;

namespace FlowDesigner.Api.Services;

public class ExecutionEngine
{
    private readonly FlowService _flowService;
    private readonly ConcurrentDictionary<string, FlowRuntime> _runtimes = new();
    private readonly ILogger<ExecutionEngine> _logger;

    public ExecutionEngine(FlowService flowService, ILogger<ExecutionEngine> logger)
    {
        _flowService = flowService;
        _logger = logger;
    }

    public async Task<ExecutionResult> ExecuteFlowAsync(string flowId, FlowMessage? input = null)
    {
        var flow = await _flowService.GetFlowAsync(flowId);
        if (flow == null)
            return new ExecutionResult { Success = false, Error = "流程不存在" };

        var runtime = GetOrCreateRuntime(flow);
        return await runtime.ExecuteAsync(input ?? new FlowMessage());
    }

    public async Task<ExecutionResult> ExecuteNodeAsync(string flowId, string nodeId, FlowMessage? input = null)
    {
        var flow = await _flowService.GetFlowAsync(flowId);
        if (flow == null)
            return new ExecutionResult { Success = false, Error = "流程不存在" };

        var node = flow.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node == null)
            return new ExecutionResult { Success = false, Error = "节点不存在" };

        var runtime = GetOrCreateRuntime(flow);
        return await runtime.ExecuteNodeAsync(node, input ?? new FlowMessage());
    }

    private FlowRuntime GetOrCreateRuntime(Flow flow)
    {
        return _runtimes.GetOrAdd(flow.Id, _ => new FlowRuntime(flow, _logger));
    }

    public void StopFlow(string flowId)
    {
        if (_runtimes.TryRemove(flowId, out var runtime))
        {
            runtime.Stop();
        }
    }
}

public class FlowRuntime
{
    private readonly Flow _flow;
    private readonly ILogger _logger;
    private bool _isRunning;

    public FlowRuntime(Flow flow, ILogger logger)
    {
        _flow = flow;
        _logger = logger;
    }

    public async Task<ExecutionResult> ExecuteAsync(FlowMessage input)
    {
        _isRunning = true;
        var outputs = new List<FlowMessage>();
        var errors = new List<string>();

        try
        {
            var startNodes = _flow.Nodes.Where(n => n.Type == "inject").ToList();
            foreach (var node in startNodes)
            {
                var result = await ExecuteNodeAsync(node, input);
                if (!result.Success)
                    errors.Add(result.Error ?? "未知错误");
                outputs.AddRange(result.Outputs);
            }

            return new ExecutionResult
            {
                Success = errors.Count == 0,
                Outputs = outputs,
                Error = errors.Count > 0 ? string.Join("; ", errors) : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "流程执行失败");
            return new ExecutionResult { Success = false, Error = ex.Message };
        }
        finally
        {
            _isRunning = false;
        }
    }

    public async Task<ExecutionResult> ExecuteNodeAsync(FlowNode node, FlowMessage input)
    {
        if (!_isRunning)
            return new ExecutionResult { Success = false, Error = "流程未运行" };

        try
        {
            var output = ExecuteNode(node, input);
            var outputs = new List<FlowMessage> { output };

            var nextConnections = _flow.Connections.Where(c => c.SourceNodeId == node.Id).ToList();
            foreach (var conn in nextConnections)
            {
                var nextNode = _flow.Nodes.FirstOrDefault(n => n.Id == conn.TargetNodeId);
                if (nextNode != null)
                {
                    var result = await ExecuteNodeAsync(nextNode, output);
                    outputs.AddRange(result.Outputs);
                }
            }

            return new ExecutionResult { Success = true, Outputs = outputs };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "节点 {NodeId} 执行失败", node.Id);
            return new ExecutionResult { Success = false, Error = ex.Message };
        }
    }

    private FlowMessage ExecuteNode(FlowNode node, FlowMessage input)
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
                _logger.LogInformation("Debug: {Payload}", output.Payload);
                break;

            case "change":
                output = ApplyChangeRules(node, input);
                break;

            case "template":
                var template = node.Properties.GetValueOrDefault("template")?.ToString() ?? "";
                output.Payload = RenderTemplate(template, input);
                break;

            case "delay":
                var timeout = Convert.ToInt32(node.Properties.GetValueOrDefault("timeout") ?? 1000);
                Thread.Sleep(timeout);
                break;

            case "switch":
                var property = node.Properties.GetValueOrDefault("property")?.ToString() ?? "payload";
                var propValue = property == "payload" ? input.Payload : input.Metadata?.GetValueOrDefault(property);
                output.Metadata?["_switch_result"] = propValue;
                break;

            default:
                break;
        }

        return output;
    }

    private FlowMessage ApplyChangeRules(FlowNode node, FlowMessage input)
    {
        var output = new FlowMessage
        {
            Payload = input.Payload,
            Topic = input.Topic,
            Metadata = new Dictionary<string, object?>(input.Metadata ?? new Dictionary<string, object?>())
        };
        return output;
    }

    private string RenderTemplate(string template, FlowMessage input)
    {
        var result = template;
        result = result.Replace("{{payload}}", input.Payload?.ToString() ?? "");
        result = result.Replace("{{topic}}", input.Topic ?? "");
        return result;
    }

    public void Stop()
    {
        _isRunning = false;
    }
}
