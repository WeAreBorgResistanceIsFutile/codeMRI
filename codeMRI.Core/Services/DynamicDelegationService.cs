using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace codeMRI.Core.Services;

/// <summary>
///     Implementation of dynamic delegation for adaptive scalability.
///     When module complexity exceeds single-pass capacity, this service
///     delegates by subdividing the module into smaller sub-modules.
///     Based on CodeWiki paper's "Dynamic Delegation" mechanism.
/// </summary>
public class DynamicDelegationService : IDelegationService
{
    private readonly int _llmContextSize;
    private readonly ILogger<DynamicDelegationService> _logger;
    private readonly DelegationOptions _options;
    private readonly IAgentTelemetryService _telemetryService;

    public DynamicDelegationService(
        ILogger<DynamicDelegationService> logger,
        IAgentTelemetryService telemetryService,
        IOptions<DelegationOptions> options,
        int llmContextSize = 32768)
    {
        _logger = logger;
        _telemetryService = telemetryService;
        _options = options.Value;
        _llmContextSize = llmContextSize;
    }

    /// <summary>
    ///     Evaluates whether a module requires delegation based on:
    ///     1. Token count exceeding context window capacity
    ///     2. Cyclomatic complexity exceeding threshold
    ///     3. High semantic diversity (many distinct subcomponents)
    /// </summary>
    public DelegationDecision EvaluateDelegation(ModuleNode module, EnhancedDependencyGraph graph, int currentDepth)
    {
        if (!_options.EnableDelegation) return DelegationDecision.NoDelegation();

        // Check max depth first
        if (currentDepth >= _options.MaxDelegationDepth)
        {
            _logger.LogDebug(
                "Module {ModuleName} at max delegation depth ({Depth}), cannot subdivide further",
                module.Name, currentDepth);
            return DelegationDecision.MaxDepth(currentDepth);
        }

        // Must have multiple components to subdivide
        if (module.Components.Count <= 1) return DelegationDecision.NoDelegation();

        var effectiveMaxTokens = _options.GetEffectiveMaxTokens(_llmContextSize);

        // 1. Check token limit
        if (module.EstimatedTokens > effectiveMaxTokens)
        {
            _logger.LogInformation(
                "Module {ModuleName} exceeds token limit ({Tokens} > {MaxTokens}), delegation required",
                module.Name, module.EstimatedTokens, effectiveMaxTokens);
            return DelegationDecision.ForTokenLimit(module.EstimatedTokens, effectiveMaxTokens);
        }

        // 2. Check complexity threshold
        if (module.ComplexityScore > _options.MaxComplexityScore)
        {
            _logger.LogInformation(
                "Module {ModuleName} exceeds complexity threshold ({Complexity} > {MaxComplexity}), delegation required",
                module.Name, module.ComplexityScore, _options.MaxComplexityScore);
            return DelegationDecision.ForComplexity(module.ComplexityScore, _options.MaxComplexityScore);
        }

        // 3. Check semantic diversity
        var semanticDiversity = CalculateSemanticDiversity(module, graph);
        if (semanticDiversity > _options.SemanticDiversityThreshold && module.Components.Count >= 4)
        {
            _logger.LogInformation(
                "Module {ModuleName} has high semantic diversity ({Diversity:P0}), delegation recommended",
                module.Name, semanticDiversity);
            return DelegationDecision.ForSemanticDiversity(semanticDiversity, _options.SemanticDiversityThreshold);
        }

        return DelegationDecision.NoDelegation();
    }

    /// <summary>
    ///     Performs delegation by subdividing the module into smaller sub-modules.
    ///     Uses directory structure first, then falls back to dependency clustering.
    /// </summary>
    public async Task<List<ModuleNode>> DelegateModuleAsync(
        ModuleNode module,
        EnhancedDependencyGraph graph,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Delegating module {ModuleName} (Tokens: {Tokens}, Complexity: {Complexity}, Components: {Components})",
            module.Name, module.EstimatedTokens, module.ComplexityScore, module.Components.Count);

        var subModules = new List<ModuleNode>();

        // Strategy 1: Try directory-based subdivision
        var directoryClusters = ClusterByDirectory(module, graph);

        if (directoryClusters.Count > 1)
        {
            _logger.LogDebug("Using directory-based subdivision for {ModuleName}: {Count} clusters",
                module.Name, directoryClusters.Count);
            subModules = CreateSubModules(module, directoryClusters, graph);
        }
        else
        {
            // Strategy 2: Use dependency-based clustering
            var dependencyClusters = await ClusterByDependenciesAsync(module, graph, cancellationToken);

            if (dependencyClusters.Count > 1)
            {
                _logger.LogDebug("Using dependency-based subdivision for {ModuleName}: {Count} clusters",
                    module.Name, dependencyClusters.Count);
                subModules = CreateSubModules(module, dependencyClusters, graph);
            }
            else
            {
                // Strategy 3: Simple split by component count
                var simpleClusters = SplitBySize(module);
                _logger.LogDebug("Using size-based subdivision for {ModuleName}: {Count} clusters",
                    module.Name, simpleClusters.Count);
                subModules = CreateSubModules(module, simpleClusters, graph);
            }
        }

        // Update the parent module
        module.Components.Clear();
        module.IsLeaf = false;

        // Track delegation
        foreach (var subModule in subModules)
            _telemetryService.TrackDelegation(
                module.Id,
                subModule.Id,
                $"Subdivided for scalability ({subModule.Components.Count} components)",
                module.Id);

        _logger.LogInformation(
            "Delegation complete for {ModuleName}: created {Count} sub-modules",
            module.Name, subModules.Count);

        return subModules;
    }

    /// <summary>
    ///     Calculates semantic diversity based on:
    ///     - Number of distinct directories
    ///     - Variation in component types
    ///     - Weak internal coupling
    /// </summary>
    private double CalculateSemanticDiversity(ModuleNode module, EnhancedDependencyGraph graph)
    {
        if (module.Components.Count == 0) return 0;

        var components = module.Components
            .Select(id => graph.GetNode(id))
            .Where(n => n != null)
            .ToList();

        if (components.Count <= 1) return 0;

        // Factor 1: Directory diversity (0-1)
        var directories = components
            .Select(c => Path.GetDirectoryName(c!.Metadata.FilePath) ?? "")
            .Distinct()
            .Count();
        var dirDiversity = Math.Min(1.0, (double)directories / Math.Max(1, components.Count / 2));

        // Factor 2: Type diversity (0-1)
        var types = components
            .Select(c => c!.Metadata.Type)
            .Distinct()
            .Count();
        var typeDiversity = Math.Min(1.0, (double)types / Math.Max(1, components.Count / 3));

        // Factor 3: Internal coupling (inverse - low coupling = high diversity)
        var internalEdges = 0;
        var totalPossibleEdges = components.Count * (components.Count - 1);

        foreach (var comp in components)
        foreach (var outEdge in comp!.OutEdges)
            if (module.Components.Contains(outEdge))
                internalEdges++;

        var couplingRatio = totalPossibleEdges > 0
            ? (double)internalEdges / totalPossibleEdges
            : 0;
        var couplingDiversity = 1 - couplingRatio; // Low coupling = high diversity

        // Weighted average
        return dirDiversity * 0.4 + typeDiversity * 0.3 + couplingDiversity * 0.3;
    }

    /// <summary>
    ///     Clusters components by their directory structure
    /// </summary>
    private Dictionary<string, List<string>> ClusterByDirectory(ModuleNode module, EnhancedDependencyGraph graph)
    {
        var clusters = new Dictionary<string, List<string>>();

        var components = module.Components
            .Select(id => (Id: id, Node: graph.GetNode(id)))
            .Where(x => x.Node != null)
            .ToList();

        if (components.Count == 0) return clusters;

        // Find common path
        var paths = components
            .Select(c => Path.GetDirectoryName(c.Node!.Metadata.FilePath) ?? "")
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        var commonPath = GetCommonPath(paths);

        // Group by first subdirectory relative to common path
        foreach (var (id, node) in components)
        {
            var dir = Path.GetDirectoryName(node!.Metadata.FilePath) ?? "";
            var clusterKey = "Root";

            if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(commonPath))
                try
                {
                    var relativePath = Path.GetRelativePath(commonPath, dir);
                    var parts = relativePath.Split(Path.DirectorySeparatorChar);
                    clusterKey = parts.Length > 0 && parts[0] != "." ? parts[0] : "Root";
                }
                catch
                {
                    clusterKey = Path.GetFileName(dir) ?? "Root";
                }

            if (!clusters.ContainsKey(clusterKey))
                clusters[clusterKey] = new List<string>();

            clusters[clusterKey].Add(id);
        }

        return clusters;
    }

    /// <summary>
    ///     Clusters components by their dependency relationships using a simple algorithm
    /// </summary>
    private Task<Dictionary<string, List<string>>> ClusterByDependenciesAsync(
        ModuleNode module,
        EnhancedDependencyGraph graph,
        CancellationToken cancellationToken)
    {
        var clusters = new Dictionary<string, List<string>>();
        var assigned = new HashSet<string>();

        // Simple connected components approach
        var clusterIndex = 0;
        var componentList = module.Components.ToList();

        foreach (var componentId in componentList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (assigned.Contains(componentId)) continue;

            var cluster = new List<string>();
            var queue = new Queue<string>();
            queue.Enqueue(componentId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (assigned.Contains(current)) continue;

                assigned.Add(current);
                cluster.Add(current);

                var node = graph.GetNode(current);
                if (node == null) continue;

                // Add connected components that are within this module
                foreach (var neighbor in node.OutEdges.Concat(node.InEdges))
                    if (module.Components.Contains(neighbor) && !assigned.Contains(neighbor))
                        queue.Enqueue(neighbor);
            }

            if (cluster.Count > 0)
            {
                clusters[$"Cluster_{clusterIndex}"] = cluster;
                clusterIndex++;
            }
        }

        return Task.FromResult(clusters);
    }

    /// <summary>
    ///     Simple split by size when other strategies fail
    /// </summary>
    private Dictionary<string, List<string>> SplitBySize(ModuleNode module)
    {
        var clusters = new Dictionary<string, List<string>>();
        var components = module.Components.ToList();

        // Target ~2-4 sub-modules
        var targetSize = Math.Max(2, components.Count / 3);
        var clusterIndex = 0;

        for (var i = 0; i < components.Count; i += targetSize)
        {
            var chunk = components.Skip(i).Take(targetSize).ToList();
            clusters[$"Part_{clusterIndex}"] = chunk;
            clusterIndex++;
        }

        return clusters;
    }

    /// <summary>
    ///     Creates sub-modules from clusters and attaches them to the parent
    /// </summary>
    private List<ModuleNode> CreateSubModules(
        ModuleNode parent,
        Dictionary<string, List<string>> clusters,
        EnhancedDependencyGraph graph)
    {
        var subModules = new List<ModuleNode>();

        foreach (var (clusterName, componentIds) in clusters)
        {
            var subModule = new ClusterModuleNode
            {
                Id = $"{parent.Id}_{SanitizeId(clusterName)}",
                Name = $"{parent.Name} - {clusterName}",
                Level = parent.Level + 1,
                IsLeaf = true
            };

            foreach (var componentId in componentIds)
            {
                var node = graph.GetNode(componentId);
                if (node != null)
                {
                    subModule.Components.Add(componentId);
                    subModule.EstimatedTokens += node.Metadata.EstimatedTokens;
                    subModule.ComplexityScore += node.Metadata.CyclomaticComplexity;
                }
            }

            parent.AddChild(subModule);
            subModules.Add(subModule);
        }

        return subModules;
    }

    private static string GetCommonPath(IEnumerable<string> paths)
    {
        var list = paths.Where(p => !string.IsNullOrEmpty(p)).ToList();
        if (list.Count == 0) return string.Empty;

        var common = list[0];
        foreach (var path in list.Skip(1))
            while (!string.IsNullOrEmpty(common) && !path.StartsWith(common))
                common = Path.GetDirectoryName(common) ?? "";

        return common;
    }

    private static string SanitizeId(string input)
    {
        return input
            .Replace(" ", "_")
            .Replace("/", "_")
            .Replace("\\", "_")
            .Replace(".", "_")
            .ToLowerInvariant();
    }
}