using FlowDesigner.Api.Services;
using FlowDesigner.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesigner.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExecutionController : ControllerBase
{
    private readonly HighPerformanceExecutionEngine _engine;
    private readonly PerformanceMonitor _monitor;
    private readonly BackpressureController _backpressure;
    
    public ExecutionController(
        HighPerformanceExecutionEngine engine,
        PerformanceMonitor monitor,
        BackpressureController backpressure)
    {
        _engine = engine;
        _monitor = monitor;
        _backpressure = backpressure;
    }
    
    [HttpPost("flow/{flowId}/run")]
    public async Task<ActionResult<ExecutionResult>> RunFlow(
        string flowId,
        [FromBody] ExecutionRequest? request = null)
    {
        var options = new ExecutionOptions
        {
            Timeout = TimeSpan.FromMilliseconds(request?.TimeoutMs ?? 30000),
            MaxConcurrentExecutions = request?.MaxConcurrency ?? 10
        };
        
        var result = await _engine.ExecuteFlowAsync(flowId, request?.Input, options);
        return Ok(result);
    }
    
    [HttpPost("flow/{flowId}/node/{nodeId}/run")]
    public async Task<ActionResult<ExecutionResult>> RunNode(
        string flowId,
        string nodeId,
        [FromBody] ExecutionRequest? request = null)
    {
        var result = await _engine.ExecuteNodeAsync(flowId, nodeId, request?.Input);
        return Ok(result);
    }
    
    [HttpPost("flow/{flowId}/stop")]
    public ActionResult StopFlow(string flowId)
    {
        _engine.StopFlow(flowId);
        return Ok(new { message = "流程已停止", flowId });
    }
    
    [HttpGet("flow/{flowId}/status")]
    public ActionResult<Services.ExecutionFlowStatus> GetFlowStatus(string flowId)
    {
        var status = _engine.GetFlowStatus(flowId);
        return Ok(status);
    }
    
    [HttpGet("statistics")]
    public ActionResult<EngineStatistics> GetEngineStatistics()
    {
        var stats = _engine.GetStatistics();
        return Ok(stats);
    }
    
    [HttpGet("metrics/flows")]
    public ActionResult<Dictionary<string, FlowMetrics>> GetFlowMetrics()
    {
        var metrics = _monitor.GetAllFlowMetrics();
        return Ok(metrics);
    }
    
    [HttpGet("metrics/flows/{flowId}")]
    public ActionResult<FlowMetrics> GetFlowMetrics(string flowId)
    {
        var metrics = _monitor.GetFlowMetrics(flowId);
        if (metrics == null)
            return NotFound();
        return Ok(metrics);
    }
    
    [HttpGet("metrics/nodes")]
    public ActionResult<Dictionary<string, NodeMetrics>> GetNodeMetrics()
    {
        var metrics = _monitor.GetAllNodeMetrics();
        return Ok(metrics);
    }
    
    [HttpGet("metrics/nodes/{nodeId}")]
    public ActionResult<NodeMetrics> GetNodeMetrics(string nodeId)
    {
        var metrics = _monitor.GetNodeMetrics(nodeId);
        if (metrics == null)
            return NotFound();
        return Ok(metrics);
    }
    
    [HttpGet("metrics/system")]
    public ActionResult<Services.ExecutionSystemStats> GetSystemMetrics()
    {
        var stats = _monitor.GetSystemStatistics();
        return Ok(stats);
    }
    
    [HttpPost("metrics/reset")]
    public ActionResult ResetMetrics()
    {
        _monitor.ResetMetrics();
        return Ok(new { message = "指标已重置" });
    }
    
    [HttpGet("backpressure")]
    public ActionResult<BackpressureStatistics> GetBackpressureStatistics()
    {
        var stats = _backpressure.GetStatistics();
        return Ok(stats);
    }
    
    [HttpPost("backpressure/reset")]
    public ActionResult ResetBackpressure()
    {
        _backpressure.Reset();
        return Ok(new { message = "背压控制器已重置" });
    }
}

public class ExecutionRequest
{
    public FlowMessage? Input { get; set; }
    public int TimeoutMs { get; set; } = 30000;
    public int MaxConcurrency { get; set; } = 10;
}
