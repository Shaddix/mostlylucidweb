namespace Mostlylucid.Workflow.Shared.Models;

/// <summary>
/// Represents a node in a workflow
/// </summary>
public class WorkflowNode
{
    /// <summary>
    /// Unique identifier for this node instance
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Type of node (e.g., "HttpRequest", "Condition", "Transform")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the node
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this node does
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Input configuration for the node (JSON)
    /// </summary>
    public Dictionary<string, object> Inputs { get; set; } = new();

    /// <summary>
    /// Output mapping configuration (JSON)
    /// </summary>
    public Dictionary<string, string> Outputs { get; set; } = new();

    /// <summary>
    /// Conditional routing configuration
    /// </summary>
    public Dictionary<string, string> Conditions { get; set; } = new();

    /// <summary>
    /// Position on the visual canvas (x, y coordinates)
    /// </summary>
    public NodePosition Position { get; set; } = new();

    /// <summary>
    /// Visual styling for the node
    /// </summary>
    public NodeStyle Style { get; set; } = new();

    /// <summary>
    /// Custom metadata for the node
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Position of a node on the visual canvas
/// </summary>
public class NodePosition
{
    public double X { get; set; }
    public double Y { get; set; }
}

/// <summary>
/// Visual styling for a node
/// </summary>
public class NodeStyle
{
    /// <summary>
    /// Background color (hex code, e.g., "#3B82F6")
    /// </summary>
    public string BackgroundColor { get; set; } = "#3B82F6";

    /// <summary>
    /// Text color (hex code)
    /// </summary>
    public string TextColor { get; set; } = "#FFFFFF";

    /// <summary>
    /// Border color (hex code)
    /// </summary>
    public string BorderColor { get; set; } = "#2563EB";

    /// <summary>
    /// Icon name or emoji to display
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Width of the node in pixels
    /// </summary>
    public int Width { get; set; } = 200;

    /// <summary>
    /// Height of the node in pixels
    /// </summary>
    public int Height { get; set; } = 100;
}
