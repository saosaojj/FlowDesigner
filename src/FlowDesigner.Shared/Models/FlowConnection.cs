namespace FlowDesigner.Shared.Models;

public class FlowConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceNodeId { get; set; } = string.Empty;
    public string SourcePortId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string TargetPortId { get; set; } = string.Empty;
}
