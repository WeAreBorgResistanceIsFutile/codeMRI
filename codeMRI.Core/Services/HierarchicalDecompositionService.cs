using System.Text.RegularExpressions;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services;

/// <summary>
///     Service for performing semantic hierarchical decomposition of codebases
/// </summary>
public class HierarchicalDecompositionService : IHierarchicalDecompositionService
{
    private const int MaxTokensPerModule = 32768;
    private const int MinTokensPerModule = 4000; // Prevent too small fragments
    private readonly IEnhancedDependencyGraphService _graphService;
    private readonly ILogger<HierarchicalDecompositionService> _logger;
    private readonly IArchitecturalPatternService _patternService;

    public HierarchicalDecompositionService(
        ILogger<HierarchicalDecompositionService> logger,
        IEnhancedDependencyGraphService graphService,
        IArchitecturalPatternService patternService)
    {
        _logger = logger;
        _graphService = graphService;
        _patternService = patternService;
    }

    /// <summary>
    ///     Performs hierarchical decomposition of the repository
    /// </summary>
    public async Task<ModuleTree> DecomposeHierarchicallyAsync(
        string repositoryPath,
        IProgress<ProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting hierarchical decomposition for repository: {Path}", repositoryPath);

        // Sub-progress for Identification (0-20%)
        var identProgress = progress != null ? new ActionProgress<ProgressInfo>(info => {
             progress.Report(new ProgressInfo { Phase = "Identification", Message = info.Message, Percentage = (int)(info.Percentage * 0.2) });
        }) : null;

        // Get components and build dependency graph
        var components = await _graphService.GetComponentsAsync(repositoryPath, identProgress, cancellationToken);
        
         // Sub-progress for Graph Build (20-100%)
        var graphProgress = progress != null ? new ActionProgress<ProgressInfo>(info => {
             progress.Report(new ProgressInfo { Phase = "Graph Construction", Message = info.Message, Percentage = 20 + (int)(info.Percentage * 0.8) });
        }) : null;

        var graph = await _graphService.BuildGraphAsync(components, graphProgress, cancellationToken);

        // Identify Entry Points
        IdentifyEntryPoints(graph);

        // Create initial module tree
        var moduleTree = new ModuleTree();

        // Perform semantic clustering (Cycle -> Layer -> Directory -> Louvain)
        var semanticClusters = await PerformSemanticClusteringAsync(graph, cancellationToken);

        // Build hierarchical structure
        await BuildHierarchyAsync(moduleTree, semanticClusters, graph, cancellationToken);

        // Ensure token limits are respected
        await EnforceTokenLimitsAsync(moduleTree, graph, cancellationToken);

        // Optimize Tree Balance
        await OptimizeTreeStructureAsync(moduleTree, cancellationToken);

        // Calculate Quality Metrics for all modules
        CalculateAllQualityMetrics(moduleTree, graph);

        _logger.LogInformation("Completed hierarchical decomposition. Total modules: {Count}", moduleTree.Nodes.Count);

        return moduleTree;
    }

    private void IdentifyEntryPoints(EnhancedDependencyGraph graph)
    {
        foreach (var node in graph.GetNodes())
        {
            if (IsEntryPoint(node))
            {
                node.Metadata.Properties["Role"] = "EntryPoint";
                _logger.LogDebug("Identified entry point: {Id}", node.ComponentId);
            }
        }
    }

    private bool IsEntryPoint(GraphNode node)
    {
        var content = node.Metadata.ContentSnippet;
        var lang = node.Metadata.Language;

        if (string.IsNullOrEmpty(content)) return false;

        if (lang.Equals("C#", StringComparison.OrdinalIgnoreCase) || lang.Equals("Java", StringComparison.OrdinalIgnoreCase))
        {
            return Regex.IsMatch(content, @"public\s+static\s+void\s+Main\s*\(", RegexOptions.IgnoreCase);
        }
        if (lang.Equals("Python", StringComparison.OrdinalIgnoreCase))
        {
            return content.Contains("if __name__ == \"__main__\":") || content.Contains("if __name__ == '__main__':");
        }
        if (lang.Equals("JavaScript", StringComparison.OrdinalIgnoreCase) || lang.Equals("TypeScript", StringComparison.OrdinalIgnoreCase))
        {
            // Express, React, etc. common patterns
            return Regex.IsMatch(content, @"app\.listen\s*\(") || 
                   Regex.IsMatch(content, @"ReactDOM\.render") ||
                   content.Contains("bootstrap()");
        }
        if (lang.Equals("C", StringComparison.OrdinalIgnoreCase) || lang.Equals("C++", StringComparison.OrdinalIgnoreCase))
        {
            return Regex.IsMatch(content, @"int\s+main\s*\(");
        }

        return false;
    }

    /// <summary>
    ///     Performs semantic clustering of components
    /// </summary>
    private async Task<Dictionary<string, List<string>>> PerformSemanticClusteringAsync(
        EnhancedDependencyGraph graph,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Performing semantic clustering on {NodeCount} components", graph.NodeCount);

        var clusters = new Dictionary<string, List<string>>();
        var assignedNodes = new HashSet<string>();

        // 0. Analyze Graph for SCCs (Cyclic Dependencies)
        try
        {
            var analysis = await _graphService.AnalyzeGraphAsync(graph, cancellationToken);
            var sccs = analysis.StronglyConnectedComponents;

            var sccIndex = 0;
            foreach (var scc in sccs)
                if (scc.Count > 1)
                {
                    // Keep cycles together
                    var newNodes = scc.Where(n => !assignedNodes.Contains(n)).ToList();
                    if (newNodes.Count > 0)
                    {
                        clusters[$"Cycle_{sccIndex}"] = newNodes;
                        foreach (var n in newNodes) assignedNodes.Add(n);
                        sccIndex++;
                    }
                }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Graph analysis failed, proceeding without cycle detection.");
        }

        // 1. Identify Architectural Layers first for remaining nodes
        var layerGroups = graph.GetNodes()
            .Where(n => !assignedNodes.Contains(n.ComponentId))
            .GroupBy(n => _patternService.DetermineLayer(n))
            .Where(g => g.Key != ArchitecturalLayerType.Unknown)
            .ToDictionary(g => g.Key, g => g.Select(n => n.ComponentId).ToList());

        foreach (var layer in layerGroups)
        {
            clusters[layer.Key.ToString()] = layer.Value;
            foreach (var n in layer.Value) assignedNodes.Add(n);
        }

        // 2. Feature/Directory Clustering for remaining nodes
        var unassignedForDir = graph.GetNodes()
             .Where(n => !assignedNodes.Contains(n.ComponentId))
             .ToList();

        var dirClusters = PerformDirectoryClustering(unassignedForDir);
        foreach(var kvp in dirClusters)
        {
            clusters[kvp.Key] = kvp.Value;
            foreach(var n in kvp.Value) assignedNodes.Add(n);
        }

        var unassignedNodes = graph.GetNodes()
            .Select(n => n.ComponentId)
            .Where(id => !assignedNodes.Contains(id))
            .ToHashSet();

        // 3. For remaining unassigned nodes, use Multi-Pass Louvain Community Detection
        if (unassignedNodes.Any())
        {
            var communities = await PerformLouvainClusteringAsync(graph, unassignedNodes, cancellationToken);
            foreach (var community in communities) clusters[$"Component_{community.Key}"] = community.Value;
        }

        return clusters;
    }

    private Dictionary<string, List<string>> PerformDirectoryClustering(List<GraphNode> nodes)
    {
        var clusters = new Dictionary<string, List<string>>();
        
        // Group by directory
        var groups = nodes
            .GroupBy(n => {
                var dir = Path.GetDirectoryName(n.Metadata.FilePath);
                return string.IsNullOrEmpty(dir) ? "Root" : dir;
            })
            .ToList();

        foreach(var group in groups)
        {
            // If a directory has significant content, make it a cluster.
            // We can use a heuristic: at least 2 files or > 1000 tokens?
            // For now, purely directory based is a strong signal for Feature grouping.
            
            // Clean up name
            var name = group.Key.Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_');
            if (string.IsNullOrEmpty(name) || name == ".") name = "Root";
            
            clusters[$"Dir_{name}"] = group.Select(n => n.ComponentId).ToList();
        }

        return clusters;
    }

    private Task<Dictionary<int, List<string>>> PerformLouvainClusteringAsync(
        EnhancedDependencyGraph graph,
        HashSet<string> nodeIds,
        CancellationToken cancellationToken)
    {
        // Iterative Louvain Algorithm
        var communities = new Dictionary<string, int>(); // NodeId -> CommunityId
        var communityNodes = new Dictionary<int, List<string>>(); // CommunityId -> List<NodeId>

        // Initialize each node in its own community
        var nextCommunityId = 0;
        foreach (var nodeId in nodeIds)
        {
            communities[nodeId] = nextCommunityId;
            communityNodes[nextCommunityId] = new List<string> { nodeId };
            nextCommunityId++;
        }

        var globalImprovement = true;
        var globalIter = 0;
        var maxGlobalIterations = 5;

        // Calculate total weight (m)
        double m = 0;
        var edgeWeights = new Dictionary<(string, string), double>();

        foreach (var nodeId in nodeIds)
        {
            var node = graph.GetNode(nodeId);
            if (node != null)
                foreach (var neighborId in node.OutEdges)
                    if (nodeIds.Contains(neighborId))
                    {
                        var w = 1.0;
                        m += w;
                        edgeWeights[(nodeId, neighborId)] = w;
                    }
        }

        m = m > 0 ? m : 1.0;

        while (globalImprovement && globalIter < maxGlobalIterations)
        {
            globalImprovement = false;
            globalIter++;

            var localImprovement = true;
            var localIter = 0;
            var maxLocalIterations = 10;

            while (localImprovement && localIter < maxLocalIterations)
            {
                localImprovement = false;
                localIter++;

                var randomizedNodes = nodeIds.OrderBy(x => Guid.NewGuid()).ToList();

                foreach (var nodeId in randomizedNodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var currentComm = communities[nodeId];
                    var bestComm = currentComm;
                    double maxDeltaQ = 0;

                    var neighborCommunities = new Dictionary<int, double>(); 
                    var nodeObj = graph.GetNode(nodeId);
                    if (nodeObj == null) continue;

                    double k_i = 0; 

                    // Outgoing
                    foreach (var target in nodeObj.OutEdges)
                        if (nodeIds.Contains(target))
                        {
                            var w = edgeWeights.GetValueOrDefault((nodeId, target), 1.0);
                            k_i += w;
                            var c = communities[target];
                            if (!neighborCommunities.ContainsKey(c)) neighborCommunities[c] = 0;
                            neighborCommunities[c] += w;
                        }

                    // Incoming
                    foreach (var source in nodeObj.InEdges)
                        if (nodeIds.Contains(source))
                        {
                            var w = edgeWeights.GetValueOrDefault((source, nodeId), 1.0);
                            k_i += w;
                            var c = communities[source];
                            if (!neighborCommunities.ContainsKey(c)) neighborCommunities[c] = 0;
                            neighborCommunities[c] += w;
                        }

                    foreach (var kvp in neighborCommunities)
                    {
                        var targetComm = kvp.Key;
                        if (targetComm == currentComm) continue;

                        var k_i_in = kvp.Value;
                        double sigma_tot = 0;
                        foreach (var peerId in communityNodes[targetComm])
                        {
                            var peer = graph.GetNode(peerId);
                            if (peer != null) sigma_tot += peer.InEdges.Count + peer.OutEdges.Count;
                        }

                        var term1 = k_i_in / m; 
                        var term2 = sigma_tot * k_i / (2 * m * m);
                        var deltaQ = term1 - term2;

                        if (deltaQ > maxDeltaQ)
                        {
                            maxDeltaQ = deltaQ;
                            bestComm = targetComm;
                        }
                    }

                    if (bestComm != currentComm && maxDeltaQ > 0.0001)
                    {
                        communityNodes[currentComm].Remove(nodeId);
                        if (communityNodes[currentComm].Count == 0) communityNodes.Remove(currentComm);

                        if (!communityNodes.ContainsKey(bestComm)) communityNodes[bestComm] = new List<string>();
                        communityNodes[bestComm].Add(nodeId);
                        communities[nodeId] = bestComm;

                        localImprovement = true;
                        globalImprovement = true;
                    }
                }
            }
        }

        return Task.FromResult(communityNodes);
    }

    /// <summary>
    ///     Builds the hierarchical module structure
    /// </summary>
    private Task BuildHierarchyAsync(
        ModuleTree moduleTree,
        Dictionary<string, List<string>> clusters,
        EnhancedDependencyGraph graph,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Building module hierarchy from {ClusterCount} clusters", clusters.Count);

        foreach (var (clusterName, componentIds) in clusters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var module = new ModuleNode
            {
                Id = $"module_{clusterName.ToLowerInvariant()}",
                Name = clusterName,
                Level = 1,
                IsLeaf = true
            };

            foreach (var componentId in componentIds)
            {
                var node = graph.GetNode(componentId);
                if (node != null)
                {
                    module.Components.Add(componentId);
                    module.EstimatedTokens += node.Metadata.EstimatedTokens;
                    module.ComplexityScore += node.Metadata.CyclomaticComplexity;
                    
                    if (node.Metadata.Properties.ContainsKey("Role") && 
                        node.Metadata.Properties["Role"].ToString() == "EntryPoint")
                    {
                        module.Metadata["HasEntryPoint"] = "true";
                    }
                }
            }

            var pattern = _patternService.RecognizePattern(module, graph);
            if (pattern.Type != ArchitecturalPatternType.Unknown)
            {
                module.Metadata["ArchitecturalPattern"] = pattern.Name;
                module.Metadata["PatternConfidence"] = pattern.Confidence.ToString("F2");
            }

            moduleTree.Root.AddChild(module);
            moduleTree.AddNode(module);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Ensures no module exceeds the token limit by splitting large modules
    /// </summary>
    private async Task EnforceTokenLimitsAsync(
        ModuleTree moduleTree,
        EnhancedDependencyGraph graph,
        CancellationToken cancellationToken)
    {
        var modulesToSplit = new Queue<ModuleNode>();

        foreach (var module in moduleTree.Nodes.Values.Where(m => m.IsLeaf))
            if (module.EstimatedTokens > MaxTokensPerModule)
                modulesToSplit.Enqueue(module);

        while (modulesToSplit.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var module = modulesToSplit.Dequeue();
            await SplitModuleAsync(moduleTree, module, graph, modulesToSplit, cancellationToken);
        }
    }

    /// <summary>
    ///     Splits a module that exceeds the token limit
    /// </summary>
    private async Task SplitModuleAsync(
        ModuleTree moduleTree,
        ModuleNode module,
        EnhancedDependencyGraph graph,
        Queue<ModuleNode> modulesToSplit,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Splitting module {ModuleId} (Tokens: {Tokens})", module.Id, module.EstimatedTokens);

        var subGraph = await CreateSubgraphAsync(graph, module.Components, cancellationToken);
        
        // Strategy: 
        // 1. Try Directory Splitting first (refine directory grouping if they were grouped by top level)
        // 2. If single directory, use Louvain.
        
        Dictionary<string, List<string>> subClusters = new();
        
        // Check if components are in different subdirectories relative to common root
        var components = module.Components.Select(c => graph.GetNode(c)).OfType<GraphNode>().ToList();
        var commonPath = GetCommonPath(components.Select(c => c.Metadata.FilePath));
        
        var bySubDir = components.GroupBy(c => {
            var rel = Path.GetRelativePath(commonPath, c.Metadata.FilePath);
            var parts = rel.Split(Path.DirectorySeparatorChar);
            return parts.Length > 1 ? parts[0] : "Root";
        }).ToList();
        
        if (bySubDir.Count > 1)
        {
            subClusters = bySubDir.ToDictionary(g => g.Key, g => g.Select(n => n.ComponentId).ToList());
        }
        else
        {
             // Fallback to Louvain
             var louvainResults = await PerformLouvainClusteringAsync(subGraph, module.Components, cancellationToken);
             subClusters = louvainResults.ToDictionary(k => k.Key.ToString(), v => v.Value);
        }

        // Logic to split logic...
        module.Components.Clear();
        module.IsLeaf = false;
        module.EstimatedTokens = 0; 

        foreach (var (clusterKey, componentIds) in subClusters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var clusterName = $"Part_{clusterKey}";

            var childModule = new ModuleNode
            {
                Id = $"{module.Id}_{clusterKey}",
                Name = $"{module.Name} - {clusterKey}",
                Level = module.Level + 1,
                IsLeaf = true
            };

            double childTokens = 0;

            foreach (var componentId in componentIds)
            {
                var node = graph.GetNode(componentId);
                if (node != null)
                {
                    childModule.Components.Add(componentId);
                    childTokens += node.Metadata.EstimatedTokens;
                    childModule.ComplexityScore += node.Metadata.CyclomaticComplexity;
                }
            }

            childModule.EstimatedTokens = childTokens;

            module.AddChild(childModule);
            moduleTree.AddNode(childModule);

            module.EstimatedTokens += childTokens;

            if (childModule.EstimatedTokens > MaxTokensPerModule) modulesToSplit.Enqueue(childModule);
        }
    }
    
    private string GetCommonPath(IEnumerable<string> paths)
    {
        var list = paths.Where(p => !string.IsNullOrEmpty(p)).ToList();
        if (!list.Any()) return string.Empty;
        
        var common = Path.GetDirectoryName(list[0]);
        while (!string.IsNullOrEmpty(common) && list.Any(p => !p.StartsWith(common)))
        {
            common = Path.GetDirectoryName(common);
        }
        return common ?? string.Empty;
    }

    private async Task OptimizeTreeStructureAsync(ModuleTree tree, CancellationToken cancellationToken)
    {
        foreach (var parent in tree.Nodes.Values.Where(n => !n.IsLeaf).ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var smallChildren = parent.Children.Where(c => c.EstimatedTokens < MinTokensPerModule).ToList();
            if (smallChildren.Count >= 2)
            {
                var merged = new HashSet<ModuleNode>();

                for (var i = 0; i < smallChildren.Count; i++)
                {
                    if (merged.Contains(smallChildren[i])) continue;

                    var c1 = smallChildren[i];
                    for (var j = i + 1; j < smallChildren.Count; j++)
                    {
                        var c2 = smallChildren[j];
                        if (merged.Contains(c2)) continue;

                        if (c1.EstimatedTokens + c2.EstimatedTokens < MaxTokensPerModule)
                        {
                            MergeModules(tree, c1, c2);
                            merged.Add(c2);
                        }
                    }
                }
            }
        }
    }

    private void MergeModules(ModuleTree tree, ModuleNode target, ModuleNode source)
    {
        _logger.LogInformation("Merging module {Source} into {Target}", source.Name, target.Name);

        foreach (var comp in source.Components) target.Components.Add(comp);
        foreach (var child in source.Children) target.AddChild(child);

        target.EstimatedTokens += source.EstimatedTokens;
        target.ComplexityScore += source.ComplexityScore;

        source.Parent?.Children.Remove(source);
        tree.Nodes.Remove(source.Id);
    }

    /// <summary>
    ///     Creates a subgraph containing only the specified components
    /// </summary>
    private async Task<EnhancedDependencyGraph> CreateSubgraphAsync(
        EnhancedDependencyGraph fullGraph,
        HashSet<string> componentIds,
        CancellationToken cancellationToken)
    {
        var subGraph = new EnhancedDependencyGraph();

        foreach (var componentId in componentIds)
        {
            var node = fullGraph.GetNode(componentId);
            if (node != null) subGraph.AddNode(componentId, node.Metadata);
        }

        foreach (var componentId in componentIds)
        {
            var node = fullGraph.GetNode(componentId);
            if (node == null) continue;

            foreach (var outEdge in node.OutEdges)
                if (componentIds.Contains(outEdge))
                    subGraph.AddEdge(componentId, outEdge, EdgeType.Dependency, 1.0);
        }

        return await Task.FromResult(subGraph);
    }

    private void CalculateAllQualityMetrics(ModuleTree tree, EnhancedDependencyGraph graph)
    {
        foreach (var module in tree.Nodes.Values) CalculateModuleMetrics(module, graph);
    }

    private void CalculateModuleMetrics(ModuleNode module, EnhancedDependencyGraph graph)
    {
        var internalEdges = 0;
        var efferentCoupling = 0; 
        var afferentCoupling = 0; 

        var componentCount = module.Components.Count;
        var abstractComponents = 0;

        foreach (var componentId in module.Components)
        {
            var node = graph.GetNode(componentId);
            if (node == null) continue;

            if (IsAbstract(node)) abstractComponents++;

            foreach (var neighbor in node.OutEdges)
                if (module.Components.Contains(neighbor))
                    internalEdges++;
                else
                    efferentCoupling++;

            foreach (var neighbor in node.InEdges)
                if (!module.Components.Contains(neighbor))
                    afferentCoupling++;
        }

        double cohesion = 0;
        if (componentCount > 1)
        {
            double maxInternalEdges = componentCount * (componentCount - 1);
            cohesion = internalEdges / Math.Max(1, maxInternalEdges);
        }
        else
        {
            cohesion = 1.0;
        }

        double totalEdges = internalEdges + efferentCoupling + afferentCoupling;
        double coupling = 0;
        if (totalEdges > 0) coupling = (efferentCoupling + afferentCoupling) / totalEdges;

        double instability = 0;
        if (efferentCoupling + afferentCoupling > 0)
            instability = (double)efferentCoupling / (efferentCoupling + afferentCoupling);

        double abstractness = 0;
        if (componentCount > 0) abstractness = (double)abstractComponents / componentCount;

        var distance = Math.Abs(abstractness + instability - 1);

        module.QualityMetrics.Cohesion = cohesion;
        module.QualityMetrics.Coupling = coupling;
        module.QualityMetrics.Complexity = module.ComplexityScore;
        module.QualityMetrics.Instability = instability;
        module.QualityMetrics.Abstractness = abstractness;
        module.QualityMetrics.DistanceFromMainSequence = distance;

        module.QualityMetrics.MaintainabilityIndex = Math.Max(0, 100 - coupling * 20 - module.ComplexityScore / 10.0);
    }

    private bool IsAbstract(GraphNode node)
    {
        return node.Metadata.Type.Contains("Interface", StringComparison.OrdinalIgnoreCase) ||
               node.Metadata.Type.Contains("Abstract", StringComparison.OrdinalIgnoreCase);
    }

    private class ActionProgress<T> : IProgress<T>
    {
        private readonly Action<T> _action;
        public ActionProgress(Action<T> action) => _action = action;
        public void Report(T value) => _action(value);
    }
}
