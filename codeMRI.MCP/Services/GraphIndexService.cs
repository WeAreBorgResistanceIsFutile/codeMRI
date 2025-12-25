using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.MCP.Services;

public class GraphIndexService
{
    private readonly IASTServiceClient _astService;
    private readonly IndexStateService _indexState;
    private readonly ILogger<GraphIndexService> _logger;

    // In-memory graph storage
    private readonly Dictionary<string, ASTGraphNode> _nodesById = new();
    private readonly Dictionary<string, List<ASTGraphEdge>> _incomingEdges = new();
    private readonly Dictionary<string, List<ASTGraphEdge>> _outgoingEdges = new();
    private readonly Dictionary<string, List<ASTGraphNode>> _nodesByName = new();
    private readonly Dictionary<string, string> _fileToNodesMap = new(); // filePath -> comma-separated node IDs

    private readonly object _lock = new();

    public GraphIndexService(
        IASTServiceClient astService,
        IndexStateService indexState,
        ILogger<GraphIndexService> logger)
    {
        _astService = astService;
        _indexState = indexState;
        _logger = logger;
    }

    public async Task IndexRepositoryAsync(string repositoryPath)
    {
        _logger.LogInformation("Starting repository indexing: {RepositoryPath}", repositoryPath);

        var codeFiles = Directory.EnumerateFiles(repositoryPath, "*.*", SearchOption.AllDirectories)
            .Where(f => IsCodeFile(f))
            .ToList();

        _indexState.MarkIndexingStarted(codeFiles.Count);

        // Limit concurrency to strict 2 to prevent overloading the single-threaded Node.js AST service
        var parallelOptions = new ParallelOptions 
        { 
            MaxDegreeOfParallelism = 2
        };

        await Parallel.ForEachAsync(codeFiles, parallelOptions, async (file, ct) =>
        {
            try
            {
                await IndexFileAsync(file);
                _indexState.MarkFileIndexed(file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to index file: {FilePath}", file);
            }
        });

        _indexState.MarkIndexingComplete();

        _logger.LogInformation("Repository indexing completed. {NodeCount} nodes, {EdgeCount} edges",
            _nodesById.Count, _incomingEdges.Values.Sum(e => e.Count));
    }

    public async Task UpdateFilesAsync(List<string> filePaths)
    {
        var validFiles = filePaths.Where(IsCodeFile).ToList();
        _logger.LogInformation("Updating {FileCount} files in index", validFiles.Count);

        foreach (var filePath in validFiles)
        {
            try
            {
                // Remove old nodes/edges for this file
                RemoveFileFromIndex(filePath);

                // Re-index the file
                await IndexFileAsync(filePath);
                _indexState.MarkFileIndexed(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update file: {FilePath}", filePath);
            }
        }

        _logger.LogInformation("File update completed");
    }

    private async Task IndexFileAsync(string filePath)
    {
        var code = await File.ReadAllTextAsync(filePath);
        var language = GetLanguageFromExtension(filePath);

        var parseResult = await _astService.ParseCodeAsync(code, language, filePath);
        if (parseResult == null)
        {
            // _logger.LogWarning("Failed to parse file: {FilePath}", filePath);
            return;
        }

        lock (_lock)
        {
            var nodeIds = new List<string>();

            // Index nodes
            foreach (var node in parseResult.DependencyGraph.Nodes)
            {
                _nodesById[node.Id] = node;
                nodeIds.Add(node.Id);

                // Index by name for fast lookup
                if (!_nodesByName.ContainsKey(node.Id))
                {
                    _nodesByName[node.Id] = new List<ASTGraphNode>();
                }
                _nodesByName[node.Id].Add(node);
            }

            // Index edges
            foreach (var edge in parseResult.DependencyGraph.Edges)
            {
                if (!_outgoingEdges.ContainsKey(edge.Source))
                {
                    _outgoingEdges[edge.Source] = new List<ASTGraphEdge>();
                }
                _outgoingEdges[edge.Source].Add(edge);

                if (!_incomingEdges.ContainsKey(edge.Target))
                {
                    _incomingEdges[edge.Target] = new List<ASTGraphEdge>();
                }
                _incomingEdges[edge.Target].Add(edge);
            }

            // Track file -> nodes mapping
            _fileToNodesMap[filePath] = string.Join(",", nodeIds);
        }
    }

    private void RemoveFileFromIndex(string filePath)
    {
        lock (_lock)
        {
            if (!_fileToNodesMap.TryGetValue(filePath, out var nodeIdsStr))
            {
                return; // File not in index
            }

            var nodeIds = nodeIdsStr.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var nodeId in nodeIds)
            {
                // Remove node
                _nodesById.Remove(nodeId);

                // Remove from name index
                if (_nodesByName.TryGetValue(nodeId, out var nodes))
                {
                    nodes.RemoveAll(n => n.Id == nodeId);
                    if (nodes.Count == 0)
                    {
                        _nodesByName.Remove(nodeId);
                    }
                }

                // Remove outgoing edges
                if (_outgoingEdges.TryGetValue(nodeId, out var outEdges))
                {
                    foreach (var edge in outEdges)
                    {
                        // Remove from incoming edges of target
                        if (_incomingEdges.TryGetValue(edge.Target, out var inEdges))
                        {
                            inEdges.RemoveAll(e => e.Source == nodeId);
                        }
                    }
                    _outgoingEdges.Remove(nodeId);
                }

                // Remove incoming edges
                if (_incomingEdges.TryGetValue(nodeId, out var inEdges2))
                {
                    foreach (var edge in inEdges2)
                    {
                        // Remove from outgoing edges of source
                        if (_outgoingEdges.TryGetValue(edge.Source, out var outEdges2))
                        {
                            outEdges2.RemoveAll(e => e.Target == nodeId);
                        }
                    }
                    _incomingEdges.Remove(nodeId);
                }
            }

            _fileToNodesMap.Remove(filePath);
        }
    }

    public List<ASTGraphNode> FindNodesByName(string name)
    {
        lock (_lock)
        {
            return _nodesByName.TryGetValue(name, out var nodes) ? nodes.ToList() : new List<ASTGraphNode>();
        }
    }

    public List<ASTGraphNode> FindNodesByType(string type)
    {
        lock (_lock)
        {
            return _nodesById.Values.Where(n => n.Type.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    public List<ASTGraphEdge> GetIncomingEdges(string nodeId)
    {
        lock (_lock)
        {
            return _incomingEdges.TryGetValue(nodeId, out var edges) ? edges.ToList() : new List<ASTGraphEdge>();
        }
    }

    public List<ASTGraphEdge> GetOutgoingEdges(string nodeId)
    {
        lock (_lock)
        {
            return _outgoingEdges.TryGetValue(nodeId, out var edges) ? edges.ToList() : new List<ASTGraphEdge>();
        }
    }

    public (int nodeCount, int edgeCount) GetStatistics()
    {
        lock (_lock)
        {
            var edgeCount = _outgoingEdges.Values.Sum(e => e.Count);
            return (_nodesById.Count, edgeCount);
        }
    }

    private static bool IsCodeFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;

        // Filter out ignored directories
        if (ShouldIgnorePath(filePath)) return false;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".cs" or ".js" or ".ts" or ".py" or ".go" or ".rs" or ".java" or ".cpp" or ".c" or ".h";
    }

    private static bool ShouldIgnorePath(string filePath)
    {
        var segments = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(s => 
            s.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
            s.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
            s.Equals(".vs", StringComparison.OrdinalIgnoreCase) ||
            s.Equals(".idea", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("dist", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("build", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("coverage", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith("."));
    }

    private static string GetLanguageFromExtension(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".cs" => "C#",
            ".js" => "JavaScript",
            ".ts" => "TypeScript",
            ".py" => "Python",
            ".go" => "Go",
            ".rs" => "Rust",
            ".java" => "Java",
            ".cpp" or ".cc" or ".cxx" => "C++",
            ".c" => "C",
            ".h" or ".hpp" => "C++",
            _ => "Unknown"
        };
    }
}
