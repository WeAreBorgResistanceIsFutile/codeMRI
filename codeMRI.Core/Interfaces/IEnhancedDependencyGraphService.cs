using System.Collections.Concurrent;
using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

public interface IEnhancedDependencyGraphService
{
    Task<EnhancedDependencyGraph> BuildGraphAsync(List<CodeComponent> components, CancellationToken cancellationToken = default);
    Task<GraphAnalysisResult> AnalyzeGraphAsync(EnhancedDependencyGraph graph, CancellationToken cancellationToken = default);
    Task<List<string>> IdentifyEntryPointsAsync(EnhancedDependencyGraph graph, CancellationToken cancellationToken = default);
    Task<Dictionary<string, double>> CalculateImportanceScoresAsync(EnhancedDependencyGraph graph, CancellationToken cancellationToken = default);
    Task<Dictionary<string, double>> EstimateTokensAsync(EnhancedDependencyGraph graph, CancellationToken cancellationToken = default);
    Task<Models.ModuleTree> DecomposeHierarchicallyAsync(EnhancedDependencyGraph graph, int maxTokensPerModule = 32768, CancellationToken cancellationToken = default);
    Task<List<CodeComponent>> GetComponentsAsync(string repositoryPath, CancellationToken cancellationToken = default);
    Task<Dictionary<string, HashSet<string>>> PartitionByDirectoryStructureAsync(EnhancedDependencyGraph graph, CancellationToken cancellationToken = default);
}

public class EnhancedDependencyGraph
{
    private readonly ConcurrentDictionary<string, GraphNode> _nodes = new();
    private readonly ConcurrentDictionary<(string, string), GraphEdge> _edges = new();
    
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
    
    public IEnumerable<GraphNode> GetNodes() => _nodes.Values;
    public IEnumerable<GraphEdge> GetEdges() => _edges.Values;
    
    public IEnumerable<string> GetZeroInDegreeNodes()
    {
        return _nodes.Values.Where(n => n.InEdges.Count == 0).Select(n => n.ComponentId);
    }
    
    public IEnumerable<string> GetHighOutDegreeNodes(int threshold = 10)
    {
        return _nodes.Values.Where(n => n.OutEdges.Count > threshold).Select(n => n.ComponentId);
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
    MethodCall,
    PropertyAccess,
    TypeReference,
    CrossBoundary
}

public class NodeMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public int CyclomaticComplexity { get; set; }
    public int NestingDepth { get; set; }
    public int FanIn { get; set; }
    public int FanOut { get; set; }
    public bool IsPublic { get; set; }
    public bool HasDocumentation { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public double EstimatedTokens { get; set; }
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
