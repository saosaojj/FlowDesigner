namespace FlowDesigner.Shared.Models;

public class ExecutionResult
{
    public bool Success { get; set; }
    public List<FlowMessage> Outputs { get; set; } = new();
    public string? Error { get; set; }
}
