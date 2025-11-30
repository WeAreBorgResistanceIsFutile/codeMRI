using Microsoft.Extensions.Logging;
using codeMRI.Core.Interfaces;

namespace codeMRI.Core.Services;

public class HierarchicalDecompositionService : IHierarchicalDecompositionService
{
    private readonly ILogger<HierarchicalDecompositionService> _logger;
    private readonly IEnhancedDependencyGraphService _graphService;

    public HierarchicalDecompositionService(
        ILogger<HierarchicalDecompositionService> logger,
        IEnhancedDependencyGraphService graphService)
    {
        _logger = logger;
        _graphService = graphService;
    }

    public async Task<ModuleTree> DecomposeRepositoryAsync(
        List<CodeComponent> components, 
        int maxTokensPerModule = 32768,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting hierarchical decomposition for {ComponentCount} components", components.Count);

        // Build dependency graph
        var graph = await _graphService.BuildGraphAsync(components, cancellationToken);
        
        // Create module tree
        var moduleTree = await CreateModuleTreeAsync(graph, maxTokensPerModule, cancellationToken);
        
        _logger.LogInformation("Created module tree with {NodeCount} nodes and {LeafCount} leaves", 
            moduleTree.Nodes.Count, moduleTree.GetAllLeaves().Count);

        return moduleTree;
    }

    private async Task<ModuleTree> CreateModuleTreeAsync(
        EnhancedDependencyGraph graph, 
        int maxTokensPerModule, 
        CancellationToken cancellationToken)
    {
        var moduleTree = new ModuleTree();
        
        // Create root node
        moduleTree.Root = new ModuleNode
        {
            Id = "root",
            Name = "Repository",
            Components = new HashSet<string>(),
            Level = 0,
            IsLeaf = false,
            EstimatedTokens = 0,
            ComplexityScore = 0
        };
        
        moduleTree.Nodes["root"] = moduleTree.Root;

        // Get all component IDs
        var allComponentIds = graph.GetNodes().Select(n => n.ComponentId).ToHashSet();
        
        // Decompose using multiple strategies
        var decompositionStrategies = new IPartitioningStrategy[]
        {
            new DirectoryStructurePartitioningStrategy(),
            new SemanticClusteringPartitioningStrategy(),
            new BalancedPartitioningStrategy()
        };

        foreach (var strategy in decompositionStrategies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var partitions = await strategy.PartitionAsync(graph, allComponentIds, cancellationToken);
            
            foreach (var partition in partitions)
            {
                var childNode = await CreateModuleNodeFromPartitionAsync(
                    graph, partition.Key, partition.Value, 1, maxTokensPerModule, cancellationToken);
                
                if (childNode != null)
                {
                    childNode.Parent = moduleTree.Root;
                    moduleTree.Root.Children.Add(childNode);
                    moduleTree.Nodes[childNode.Id] = childNode;
                }
            }
        }

        return moduleTree;
    }

    private async Task<ModuleNode?> CreateModuleNodeFromPartitionAsync(
        EnhancedDependencyGraph graph,
        string name,
        HashSet<string> componentIds,
        int level,
        int maxTokensPerModule,
        CancellationToken cancellationToken)
    {
        if (!componentIds.Any()) return null;

        var estimatedTokens = componentIds.Sum(id => 
            graph.GetNode(id)?.Metadata.EstimatedTokens ?? 0);

        // If under threshold, create leaf node
        if (estimatedTokens <= maxTokensPerModule * 0.8) // 80% threshold
        {
            return new ModuleNode
            {
                Id = $"module_{Guid.NewGuid():N}[..8]",
                Name = name,
                Components = componentIds,
                Level = level,
                IsLeaf = true,
                EstimatedTokens = (int)estimatedTokens,
                ComplexityScore = componentIds.Sum(id => 
                    graph.GetNode(id)?.Metadata.CyclomaticComplexity ?? 0)
            };
        }

        // Otherwise, split further
        return await SplitPartitionAsync(
            graph, name, componentIds, level, maxTokensPerModule, cancellationToken);
    }

    private async Task<ModuleNode> SplitPartitionAsync(
        EnhancedDependencyGraph graph,
        string name,
        HashSet<string> componentIds,
        int level,
        int maxTokensPerModule,
        CancellationToken cancellationToken)
    {
        var parent = new ModuleNode
        {
            Id = $"module_{Guid.NewGuid():N}[..8]",
            Name = name,
            Components = new HashSet<string>(),
            Level = level,
            IsLeaf = false,
            EstimatedTokens = (int)componentIds.Sum(id => 
                graph.GetNode(id)?.Metadata.EstimatedTokens ?? 0),
            ComplexityScore = 0
        };

        // Use balanced partitioning for large modules
        var strategy = new BalancedPartitioningStrategy();
        var subPartitions = await strategy.PartitionAsync(graph, componentIds, cancellationToken);

        foreach (var subPartition in subPartitions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var childNode = await CreateModuleNodeFromPartitionAsync(
                graph, subPartition.Key, subPartition.Value, level + 1, maxTokensPerModule, cancellationToken);

            if (childNode != null)
            {
                childNode.Parent = parent;
                parent.Children.Add(childNode);
                // parent.Nodes doesn't exist - remove this line
            }
        }

        return parent;
    }
}

public interface IHierarchicalDecompositionService
{
    Task<ModuleTree> DecomposeRepositoryAsync(
        List<CodeComponent> components, 
        int maxTokensPerModule = 32768,
        CancellationToken cancellationToken = default);
}

public interface IPartitioningStrategy
{
    Task<Dictionary<string, HashSet<string>>> PartitionAsync(
        EnhancedDependencyGraph graph, 
        HashSet<string> componentIds, 
        CancellationToken cancellationToken);
}

public class DirectoryStructurePartitioningStrategy : IPartitioningStrategy
{
    public async Task<Dictionary<string, HashSet<string>>> PartitionAsync(
        EnhancedDependencyGraph graph, 
        HashSet<string> componentIds, 
        CancellationToken cancellationToken)
    {
        var partitions = new Dictionary<string, HashSet<string>>();

        foreach (var componentId in componentIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var node = graph.GetNode(componentId);
            if (node?.Metadata.FilePath == null) continue;

            var directory = ExtractDirectoryName(node.Metadata.FilePath);
            
            if (!partitions.ContainsKey(directory))
            {
                partitions[directory] = new HashSet<string>();
            }
            
            partitions[directory].Add(componentId);
        }

        return await Task.FromResult(partitions);
    }

    private string ExtractDirectoryName(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return "Root";

        var parts = filePath.Split('/', '\\');
        return parts.Length > 1 ? parts[^2] : "Root";
    }
}

public class SemanticClusteringPartitioningStrategy : IPartitioningStrategy
{
    public async Task<Dictionary<string, HashSet<string>>> PartitionAsync(
        EnhancedDependencyGraph graph, 
        HashSet<string> componentIds, 
        CancellationToken cancellationToken)
    {
        var partitions = new Dictionary<string, HashSet<string>>();
        var componentNames = new Dictionary<string, string>();

        // Extract component names for semantic analysis
        foreach (var componentId in componentIds)
        {
            var node = graph.GetNode(componentId);
            componentNames[componentId] = node?.Metadata.Type ?? "Unknown";
        }

        // Group by component type (simple semantic clustering)
        var typeGroups = componentIds.GroupBy(id => componentNames[id]);

        foreach (var group in typeGroups)
        {
            var partitionName = $"{group.Key}_Components";
            partitions[partitionName] = group.ToHashSet();
        }

        return await Task.FromResult(partitions);
    }
}

public class BalancedPartitioningStrategy : IPartitioningStrategy
{
    public async Task<Dictionary<string, HashSet<string>>> PartitionAsync(
        EnhancedDependencyGraph graph, 
        HashSet<string> componentIds, 
        CancellationToken cancellationToken)
    {
        var partitions = new Dictionary<string, HashSet<string>>();
        var componentSizes = new Dictionary<string, double>();

        // Calculate component sizes
        foreach (var componentId in componentIds)
        {
            var node = graph.GetNode(componentId);
            componentSizes[componentId] = node?.Metadata.EstimatedTokens ?? 0;
        }

        // Sort components by size (descending)
        var sortedComponents = componentIds
            .OrderByDescending(id => componentSizes[id])
            .ToList();

        // Use First-Fit-Decreasing bin packing
        const int targetSize = 16000; // Half of max tokens
        var bins = new List<HashSet<string>>();
        var binSizes = new List<double>();

        foreach (var componentId in sortedComponents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var componentSize = componentSizes[componentId];
            var placed = false;

            // Try to place in existing bin
            for (int i = 0; i < bins.Count; i++)
            {
                if (binSizes[i] + componentSize <= targetSize)
                {
                    bins[i].Add(componentId);
                    binSizes[i] += componentSize;
                    placed = true;
                    break;
                }
            }

            // Create new bin if couldn't place
            if (!placed)
            {
                bins.Add(new HashSet<string> { componentId });
                binSizes.Add(componentSize);
            }
        }

        // Convert to dictionary
        for (int i = 0; i < bins.Count; i++)
        {
            partitions[$"partition_{i}"] = bins[i];
        }

        return await Task.FromResult(partitions);
    }
}