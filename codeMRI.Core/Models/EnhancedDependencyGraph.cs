using System.Collections.Concurrent;

namespace codeMRI.Core.Models;

public class EnhancedDependencyGraph
{
    private readonly ConcurrentDictionary<(string, string), GraphEdge> _edges = new();
    private readonly ConcurrentDictionary<string, GraphNode> _nodes = new();

    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int NodeCount => _nodes.Count;
    public int EdgeCount => _edges.Count;

    public void AddNode(string componentId, NodeMetadata metadata)
    {
        _nodes.TryAdd(componentId, new GraphNode
        {
            ComponentId = componentId,
            Metadata = metadata,
            InEdges = new ConcurrentBag<string>(),
            OutEdges = new ConcurrentBag<string>()
        });
    }

    public void AddEdge(string fromComponentId, string toComponentId, EdgeType type, double strength)
    {
        if (_nodes.ContainsKey(fromComponentId) && _nodes.ContainsKey(toComponentId))
        {
            var edgeKey = (fromComponentId, toComponentId);
            _edges.TryAdd(edgeKey, new GraphEdge
            {
                From = fromComponentId,
                To = toComponentId,
                Type = type,
                Strength = strength,
                CreatedAt = DateTime.UtcNow
            });

            _nodes[fromComponentId].OutEdges.Add(toComponentId);
            _nodes[toComponentId].InEdges.Add(fromComponentId);
        }
    }

    public GraphNode? GetNode(string componentId)
    {
        _nodes.TryGetValue(componentId, out var node);
        return node;
    }

    public IEnumerable<GraphNode> GetNodes()
    {
        return _nodes.Values;
    }

    public IEnumerable<GraphEdge> GetEdges()
    {
        return _edges.Values;
    }

    public IEnumerable<string> GetZeroInDegreeNodes()
    {
        return _nodes.Values.Where(n => n.InEdges.Count == 0).Select(n => n.ComponentId);
    }

    public IEnumerable<string> GetHighOutDegreeNodes(int threshold = 10)
    {
        return _nodes.Values.Where(n => n.OutEdges.Count > threshold).Select(n => n.ComponentId);
    }

    public void RemoveNodesByFilePath(string filePath)
    {
        var nodesToRemove = _nodes.Values
            .Where(n => n.Metadata.FilePath != null && n.Metadata.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase))
            .Select(n => n.ComponentId)
            .ToList();

        foreach (var nodeId in nodesToRemove)
        {
            RemoveNode(nodeId);
        }
    }

    public void RemoveNode(string nodeId)
    {
        if (_nodes.TryRemove(nodeId, out var node))
        {
            // Remove edges where this node is the source
            foreach (var targetId in node.OutEdges)
            {
                if (_nodes.TryGetValue(targetId, out var targetNode))
                {
                    var remainingIn = targetNode.InEdges.Where(id => id != nodeId).ToList();
                    targetNode.InEdges = new ConcurrentBag<string>(remainingIn);
                }
                _edges.TryRemove((nodeId, targetId), out _);
            }

            // Remove edges where this node is the target
            foreach (var sourceId in node.InEdges)
            {
                if (_nodes.TryGetValue(sourceId, out var sourceNode))
                {
                    var remainingOut = sourceNode.OutEdges.Where(id => id != nodeId).ToList();
                    sourceNode.OutEdges = new ConcurrentBag<string>(remainingOut);
                }
                _edges.TryRemove((sourceId, nodeId), out _);
            }
        }
    }
}

public class GraphNode
{
    public string ComponentId { get; set; } = string.Empty;
    public NodeMetadata Metadata { get; set; } = new();
    public ConcurrentBag<string> InEdges { get; set; } = new();
    public ConcurrentBag<string> OutEdges { get; set; } = new();
    public double PageRankScore { get; set; }
    public int InDegree => InEdges.Count;
    public int OutDegree => OutEdges.Count;
}

public class GraphEdge
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public EdgeType Type { get; set; }
    public double Strength { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum EdgeType
{
    Dependency,
    Inheritance,
    Implementation,
    Composition,
    Aggregation,
    MethodCall,
    PropertyAccess,
    TypeReference,
    CrossBoundary,
    Call, // Added missing type from tests
    Contains,
    ChildOf
}

public class NodeMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public int CyclomaticComplexity { get; set; }
    public int NestingDepth { get; set; }
    public int FanIn { get; set; }
    public int FanOut { get; set; }
    public bool IsPublic { get; set; }
    public bool HasDocumentation { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string ContentSnippet { get; set; } = string.Empty;
    public double EstimatedTokens { get; set; }

    // Enhanced properties for filtering and visualization
    public string Layer { get; set; } = string.Empty;
    public double Complexity { get; set; }
    public string Visibility { get; set; } = "public";
    public Dictionary<string, object> Properties { get; set; } = new();
}

public class GraphAnalysisResult
{
    public Dictionary<string, double> PageRankScores { get; set; } = new();
    public List<List<string>> StronglyConnectedComponents { get; set; } = new();
    public List<string> ZeroInDegreeNodes { get; set; } = new();
    public Dictionary<string, int> NodeDegrees { get; set; } = new();
    public GraphStatistics Statistics { get; set; } = new();
}

public class GraphStatistics
{
    public int TotalNodes { get; set; }
    public int TotalEdges { get; set; }
    public double AverageDegree { get; set; }
    public int MaxInDegree { get; set; }
    public int MaxOutDegree { get; set; }
    public double Density { get; set; }
    public int ConnectedComponents { get; set; }
}