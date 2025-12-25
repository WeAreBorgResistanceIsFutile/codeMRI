using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.MCP.Services;

public class QueryEngine
{
    private readonly GraphIndexService _graphIndex;
    private readonly ILogger<QueryEngine> _logger;

    public QueryEngine(GraphIndexService graphIndex, ILogger<QueryEngine> logger)
    {
        _graphIndex = graphIndex;
        _logger = logger;
    }

    public async Task<List<Reference>> FindAllReferencesAsync(string symbolName, string? filePath = null)
    {
        await Task.CompletedTask; // For async consistency
        
        var references = new List<Reference>();
        var nodes = _graphIndex.FindNodesByName(symbolName);

        foreach (var node in nodes)
        {
            // Find all incoming edges (who references this node)
            var incomingEdges = _graphIndex.GetIncomingEdges(node.Id);
            
            foreach (var edge in incomingEdges)
            {
                references.Add(new Reference
                {
                    SymbolName = symbolName,
                    ReferencedFrom = edge.Source,
                    EdgeType = edge.Type,
                    FilePath = node.Properties?.ToString() ?? ""
                });
            }
        }

        _logger.LogInformation("Found {Count} references to {Symbol}", references.Count, symbolName);
        return references;
    }

    public async Task<CallHierarchy> GetCallHierarchyAsync(string methodName, int depth = 5, string direction = "both")
    {
        await Task.CompletedTask;
        
        var hierarchy = new CallHierarchy { MethodName = methodName };
        var nodes = _graphIndex.FindNodesByName(methodName);

        if (nodes.Count == 0)
        {
            _logger.LogWarning("Method not found: {MethodName}", methodName);
            return hierarchy;
        }

        var rootNode = nodes.First();

        if (direction is "callers" or "both")
        {
            hierarchy.Callers = BuildCallTree(rootNode.Id, depth, isIncoming: true);
        }

        if (direction is "callees" or "both")
        {
            hierarchy.Callees = BuildCallTree(rootNode.Id, depth, isIncoming: false);
        }

        return hierarchy;
    }

    private List<CallNode> BuildCallTree(string nodeId, int depth, bool isIncoming, HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>();
        
        if (depth <= 0 || visited.Contains(nodeId))
        {
            return new List<CallNode>();
        }

        visited.Add(nodeId);
        var result = new List<CallNode>();
        var edges = isIncoming ? _graphIndex.GetIncomingEdges(nodeId) : _graphIndex.GetOutgoingEdges(nodeId);

        foreach (var edge in edges.Where(e => e.Type.Equals("Calls", StringComparison.OrdinalIgnoreCase)))
        {
            var targetId = isIncoming ? edge.Source : edge.Target;
            var children = BuildCallTree(targetId, depth - 1, isIncoming, visited);

            result.Add(new CallNode
            {
                NodeId = targetId,
                EdgeType = edge.Type,
                Children = children
            });
        }

        return result;
    }

    public async Task<List<ASTGraphNode>> FindImplementationsAsync(string interfaceOrBaseClass)
    {
        await Task.CompletedTask;
        
        var implementations = new List<ASTGraphNode>();
        var interfaceNodes = _graphIndex.FindNodesByName(interfaceOrBaseClass);

        foreach (var interfaceNode in interfaceNodes)
        {
            var incomingEdges = _graphIndex.GetIncomingEdges(interfaceNode.Id);
            
            foreach (var edge in incomingEdges.Where(e => 
                e.Type.Equals("Implements", StringComparison.OrdinalIgnoreCase) ||
                e.Type.Equals("Inherits", StringComparison.OrdinalIgnoreCase)))
            {
                var implementingNodes = _graphIndex.FindNodesByName(edge.Source);
                implementations.AddRange(implementingNodes);
            }
        }

        _logger.LogInformation("Found {Count} implementations of {Interface}", implementations.Count, interfaceOrBaseClass);
        return implementations.Distinct().ToList();
    }

    public async Task<DependencyTree> GetDependencyTreeAsync(string nodeId, int depth = 3, string direction = "dependencies")
    {
        await Task.CompletedTask;
        
        var tree = new DependencyTree { RootNodeId = nodeId };
        var isOutgoing = direction.Equals("dependencies", StringComparison.OrdinalIgnoreCase);

        tree.Dependencies = BuildDependencyTree(nodeId, depth, isOutgoing);

        return tree;
    }

    private List<DependencyNode> BuildDependencyTree(string nodeId, int depth, bool isOutgoing, HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>();
        
        if (depth <= 0 || visited.Contains(nodeId))
        {
            return new List<DependencyNode>();
        }

        visited.Add(nodeId);
        var result = new List<DependencyNode>();
        var edges = isOutgoing ? _graphIndex.GetOutgoingEdges(nodeId) : _graphIndex.GetIncomingEdges(nodeId);

        foreach (var edge in edges.Where(e => e.Type.Equals("DependsOn", StringComparison.OrdinalIgnoreCase)))
        {
            var targetId = isOutgoing ? edge.Target : edge.Source;
            var children = BuildDependencyTree(targetId, depth - 1, isOutgoing, visited);

            result.Add(new DependencyNode
            {
                NodeId = targetId,
                EdgeType = edge.Type,
                Children = children
            });
        }

        return result;
    }

    public async Task<TypeHierarchy> GetTypeHierarchyAsync(string typeName, int depth = 10, string direction = "both")
    {
        await Task.CompletedTask;
        
        var hierarchy = new TypeHierarchy { TypeName = typeName };
        var nodes = _graphIndex.FindNodesByName(typeName);

        if (nodes.Count == 0)
        {
            return hierarchy;
        }

        var rootNode = nodes.First();

        if (direction is "ancestors" or "both")
        {
            hierarchy.Ancestors = BuildTypeTree(rootNode.Id, depth, isAncestors: true);
        }

        if (direction is "descendants" or "both")
        {
            hierarchy.Descendants = BuildTypeTree(rootNode.Id, depth, isAncestors: false);
        }

        return hierarchy;
    }

    private List<TypeNode> BuildTypeTree(string nodeId, int depth, bool isAncestors, HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>();
        
        if (depth <= 0 || visited.Contains(nodeId))
        {
            return new List<TypeNode>();
        }

        visited.Add(nodeId);
        var result = new List<TypeNode>();
        var edges = isAncestors ? _graphIndex.GetOutgoingEdges(nodeId) : _graphIndex.GetIncomingEdges(nodeId);

        foreach (var edge in edges.Where(e => 
            e.Type.Equals("Inherits", StringComparison.OrdinalIgnoreCase) ||
            e.Type.Equals("Implements", StringComparison.OrdinalIgnoreCase)))
        {
            var targetId = isAncestors ? edge.Target : edge.Source;
            var children = BuildTypeTree(targetId, depth - 1, isAncestors, visited);

            result.Add(new TypeNode
            {
                NodeId = targetId,
                RelationType = edge.Type,
                Children = children
            });
        }

        return result;
    }

    public async Task<List<ASTGraphNode>> SemanticSearchAsync(string query, string type = "any", int limit = 10)
    {
        await Task.CompletedTask;
        
        // Simple implementation: search by name/type matching
        // TODO: Could be enhanced with embeddings/vector search
        var allNodes = type.Equals("any", StringComparison.OrdinalIgnoreCase)
            ? _graphIndex.FindNodesByName(query)
            : _graphIndex.FindNodesByType(type).Where(n => n.Id.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        return allNodes.Take(limit).ToList();
    }
}

// Result models
public class Reference
{
    public string SymbolName { get; set; } = string.Empty;
    public string ReferencedFrom { get; set; } = string.Empty;
    public string EdgeType { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}

public class CallHierarchy
{
    public string MethodName { get; set; } = string.Empty;
    public List<CallNode> Callers { get; set; } = new();
    public List<CallNode> Callees { get; set; } = new();
}

public class CallNode
{
    public string NodeId { get; set; } = string.Empty;
    public string EdgeType { get; set; } = string.Empty;
    public List<CallNode> Children { get; set; } = new();
}

public class DependencyTree
{
    public string RootNodeId { get; set; } = string.Empty;
    public List<DependencyNode> Dependencies { get; set; } = new();
}

public class DependencyNode
{
    public string NodeId { get; set; } = string.Empty;
    public string EdgeType { get; set; } = string.Empty;
    public List<DependencyNode> Children { get; set; } = new();
}

public class TypeHierarchy
{
    public string TypeName { get; set; } = string.Empty;
    public List<TypeNode> Ancestors { get; set; } = new();
    public List<TypeNode> Descendants { get; set; } = new();
}

public class TypeNode
{
    public string NodeId { get; set; } = string.Empty;
    public string RelationType { get; set; } = string.Empty;
    public List<TypeNode> Children { get; set; } = new();
}
