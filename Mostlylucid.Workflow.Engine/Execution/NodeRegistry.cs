using Mostlylucid.Workflow.Engine.Interfaces;

namespace Mostlylucid.Workflow.Engine.Execution;

/// <summary>
/// Registry for workflow node types
/// </summary>
public class NodeRegistry : INodeRegistry
{
    private readonly Dictionary<string, Type> _nodeTypes = new();
    private readonly IServiceProvider _serviceProvider;

    public NodeRegistry(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Registers a node type
    /// </summary>
    public void RegisterNode<TNode>(string nodeType) where TNode : IWorkflowNode
    {
        _nodeTypes[nodeType] = typeof(TNode);
    }

    /// <summary>
    /// Gets a node instance by type
    /// </summary>
    public IWorkflowNode? GetNode(string nodeType)
    {
        if (!_nodeTypes.TryGetValue(nodeType, out var type))
        {
            return null;
        }

        return _serviceProvider.GetService(type) as IWorkflowNode
               ?? Activator.CreateInstance(type) as IWorkflowNode;
    }

    /// <summary>
    /// Gets all registered node types
    /// </summary>
    public IEnumerable<string> GetRegisteredNodeTypes()
    {
        return _nodeTypes.Keys;
    }
}
