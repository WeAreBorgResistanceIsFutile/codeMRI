using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace codeMRI.MCP.Services;

public class GraphIndexService
{
    private readonly IEnhancedDependencyGraphService _graphService;
    private readonly IndexStateService _indexState;
    private readonly ILogger<GraphIndexService> _logger;

    private EnhancedDependencyGraph _graph = new();

    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };
    private const string IndexFileName = "graph_index.json";
    private const string IndexDirName = ".codemri";

    private readonly object _lock = new();

    public GraphIndexService(
        IEnhancedDependencyGraphService graphService,
        IndexStateService indexState,
        ILogger<GraphIndexService> logger)
    {
        _graphService = graphService;
        _indexState = indexState;
        _logger = logger;
    }

    public async Task IndexRepositoryAsync(string repositoryPath)
    {
        _logger.LogInformation("Starting repository indexing: {RepositoryPath}", repositoryPath);

        // Try to load existing index
        var loaded = await LoadIndexAsync(repositoryPath);
        if (loaded)
        {
            _logger.LogInformation("Loaded existing index from disk");
            _indexState.MarkIndexingComplete();
            _ = Task.Run(() => UpdateIndexAsync(repositoryPath)); // Fire and forget update
            return;
        }

        var codeFiles = Directory.EnumerateFiles(repositoryPath, "*.*", SearchOption.AllDirectories)
            .Where(f => IsCodeFile(f))
            .ToList();

        _indexState.MarkIndexingStarted(codeFiles.Count);

        // Delegate indexing to the Core service
        await _graphService.IndexRepositoryAsync(repositoryPath, _graph);

        _indexState.MarkIndexingComplete();
        await SaveIndexAsync(repositoryPath);

        _logger.LogInformation("Repository indexing completed. {NodeCount} nodes, {EdgeCount} edges",
            _graph.NodeCount, _graph.EdgeCount);
    }

    public async Task UpdateIndexAsync(string repositoryPath)
    {
        _logger.LogInformation("Checking for updates in repository: {RepositoryPath}", repositoryPath);
        var codeFiles = Directory.EnumerateFiles(repositoryPath, "*.*", SearchOption.AllDirectories)
            .Where(f => IsCodeFile(f))
            .ToList();
        
        var filesInIndex = _graph.GetNodes().Select(n => n.Metadata.FilePath).Distinct().ToHashSet();
        var filesOnDisk = codeFiles.ToHashSet();

        var newFiles = filesOnDisk.Except(filesInIndex).ToList();
        var deletedFiles = filesInIndex.Except(filesOnDisk).ToList();

        if (newFiles.Any() || deletedFiles.Any())
        {
            _logger.LogInformation("Found {New} new files and {Deleted} deleted files", newFiles.Count, deletedFiles.Count);
            
            foreach (var file in deletedFiles) 
            {
                if (file != null) _graph.RemoveNodesByFilePath(file);
            }

            if (newFiles.Any())
            {
                await UpdateFilesAsync(newFiles);
            }
            
            await SaveIndexAsync(repositoryPath);
        }
        else 
        {
            _logger.LogInformation("Index is up to date.");
        }
    }

    private async Task SaveIndexAsync(string repositoryPath)
    {
        try 
        {
            var indexDir = Path.Combine(repositoryPath, IndexDirName);
            if (!Directory.Exists(indexDir)) Directory.CreateDirectory(indexDir);

            var indexPath = Path.Combine(indexDir, IndexFileName);
            
            // Map EnhancedDependencyGraph to GraphIndexData DTO for serialization
            var data = new GraphIndexData
            {
                Nodes = _graph.GetNodes().Select(MapToASTNode).ToList(),
                OutgoingEdges = _graph.GetEdges()
                    .GroupBy(e => e.From)
                    .ToDictionary(g => g.Key, g => g.Select(MapToASTEdge).ToList()),
                IncomingEdges = _graph.GetEdges()
                    .GroupBy(e => e.To)
                    .ToDictionary(g => g.Key, g => g.Select(MapToASTEdge).ToList())
            };

            var json = JsonSerializer.Serialize(data, _jsonOptions);
            await File.WriteAllTextAsync(indexPath, json);
            _logger.LogInformation("Index saved to {Path}", indexPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save index to disk");
        }
    }

    public async Task<bool> LoadIndexAsync(string repositoryPath)
    {
        try
        {
            var indexPath = Path.Combine(repositoryPath, IndexDirName, IndexFileName);
            if (!File.Exists(indexPath)) return false;

            var json = await File.ReadAllTextAsync(indexPath);
            var data = JsonSerializer.Deserialize<GraphIndexData>(json, _jsonOptions);

            if (data == null) return false;

            var newGraph = new EnhancedDependencyGraph();
            foreach (var node in data.Nodes)
            {
                newGraph.AddNode(node.Id, new NodeMetadata 
                { 
                    Id = node.Id, 
                    Type = node.Type, 
                    Language = node.Language,
                    FilePath = node.FilePath
                });
            }

            foreach (var edges in data.OutgoingEdges.Values)
            {
                foreach (var edge in edges)
                {
                    if (Enum.TryParse<EdgeType>(edge.Type, true, out var type))
                        newGraph.AddEdge(edge.Source, edge.Target, type, 1.0);
                    else
                        newGraph.AddEdge(edge.Source, edge.Target, EdgeType.Dependency, 1.0);
                }
            }

            lock (_lock)
            {
                _graph = newGraph;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load index from disk");
            return false;
        }
    }

    private ASTGraphNode MapToASTNode(GraphNode node)
    {
        return new ASTGraphNode
        {
            Id = node.ComponentId,
            Type = node.Metadata.Type,
            Language = node.Metadata.Language,
            FilePath = node.Metadata.FilePath,
            Properties = new ASTNodeProperties
            {
                Annotations = (List<string>)node.Metadata.Properties.GetValueOrDefault("Annotations", new List<string>()),
                Decorators = (List<string>)node.Metadata.Properties.GetValueOrDefault("Decorators", new List<string>())
            }
        };
    }

    private ASTGraphEdge MapToASTEdge(GraphEdge edge)
    {
        return new ASTGraphEdge
        {
            Source = edge.From,
            Target = edge.To,
            Type = edge.Type.ToString()
        };
    }

    private class GraphIndexData
    {
        public List<ASTGraphNode> Nodes { get; set; } = new();
        public Dictionary<string, List<ASTGraphEdge>> IncomingEdges { get; set; } = new();
        public Dictionary<string, List<ASTGraphEdge>> OutgoingEdges { get; set; } = new();
    }

    public async Task UpdateFilesAsync(List<string> filePaths)
    {
        _logger.LogInformation("Updating {FileCount} files in index", filePaths.Count);

        foreach (var filePath in filePaths)
        {
            try
            {
                await _graphService.IndexFileAsync(filePath, _graph);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update file: {FilePath}", filePath);
            }
        }

        _logger.LogInformation("File update completed");
    }

    public List<ASTGraphNode> FindNodesByName(string name)
    {
        return _graph.GetNodes()
            .Where(n => 
                n.ComponentId.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                n.ComponentId.EndsWith($"::{name}", StringComparison.OrdinalIgnoreCase) ||
                n.ComponentId.EndsWith($".{name}", StringComparison.OrdinalIgnoreCase))
            .Select(MapToASTNode)
            .ToList();
    }

    public List<ASTGraphNode> FindNodesByType(string type)
    {
        return _graph.GetNodes()
            .Where(n => n.Metadata.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
            .Select(MapToASTNode)
            .ToList();
    }

    public List<ASTGraphEdge> GetIncomingEdges(string nodeId)
    {
        var node = _graph.GetNode(nodeId);
        if (node == null) return new List<ASTGraphEdge>();

        return node.InEdges.Select(sourceId => new ASTGraphEdge
        {
            Source = sourceId,
            Target = nodeId,
            Type = "Dependency" // Simplified for now
        }).ToList();
    }

    public List<ASTGraphEdge> GetOutgoingEdges(string nodeId)
    {
        var node = _graph.GetNode(nodeId);
        if (node == null) return new List<ASTGraphEdge>();

        return node.OutEdges.Select(targetId => new ASTGraphEdge
        {
            Source = nodeId,
            Target = targetId,
            Type = "Dependency" // Simplified for now
        }).ToList();
    }

    public (int nodeCount, int edgeCount) GetStatistics()
    {
        return (_graph.NodeCount, _graph.EdgeCount);
    }

    private static bool IsCodeFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".cs" or ".js" or ".ts" or ".py" or ".go" or ".rs" or ".java" or ".cpp" or ".c" or ".h";
    }
}
