namespace FlowDesigner.Shared.Models;

public class FlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public Dictionary<string, object?> Properties { get; set; } = new();
    public List<NodePort> Inputs { get; set; } = new();
    public List<NodePort> Outputs { get; set; } = new();
}

public class NodePort
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "any";
}
