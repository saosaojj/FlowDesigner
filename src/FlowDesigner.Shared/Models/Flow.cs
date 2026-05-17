namespace FlowDesigner.Shared.Models;

public class Flow
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<FlowNode> Nodes { get; set; } = new();
    public List<FlowConnection> Connections { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
