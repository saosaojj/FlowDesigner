namespace FlowDesigner.Shared.Models;

public class NodeDefinition
{
    public string Type { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#3b82f6";
    public string Icon { get; set; } = "fa-box";
    public List<PortDefinition> Inputs { get; set; } = new();
    public List<PortDefinition> Outputs { get; set; } = new();
    public Dictionary<string, PropertyDefinition> Properties { get; set; } = new();
}

public class PortDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "any";
    public string? Label { get; set; }
}

public class PropertyDefinition
{
    public string Type { get; set; } = "string";
    public string? Label { get; set; }
    public object? DefaultValue { get; set; }
    public bool Required { get; set; }
}
