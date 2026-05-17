namespace FlowDesigner.Shared.Models;

public class FlowMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public object? Payload { get; set; }
    public string? Topic { get; set; }
    public Dictionary<string, object?> Metadata { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
