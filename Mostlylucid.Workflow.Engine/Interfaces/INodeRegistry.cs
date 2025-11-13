namespace Mostlylucid.Workflow.Engine.Interfaces;

/// <summary>
/// Registry for workflow node types
/// </summary>
public interface INodeRegistry
{
    /// <summary>
    /// Registers a node type
    /// </summary>
    void RegisterNode<TNode>(string nodeType) where TNode : IWorkflowNode;

    /// <summary>
    /// Gets a node instance by type
    /// </summary>
    IWorkflowNode? GetNode(string nodeType);

    /// <summary>
    /// Gets all registered node types
    /// </summary>
    IEnumerable<string> GetRegisteredNodeTypes();
}
