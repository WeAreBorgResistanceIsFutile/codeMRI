using System.Globalization;
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
    private readonly IProgressService _progressService;

    public HierarchicalDecompositionService(
        ILogger<HierarchicalDecompositionService> logger,
        IEnhancedDependencyGraphService graphService,
        IArchitecturalPatternService patternService,
        IProgressService progressService)
    {
        _logger = logger;
        _graphService = graphService;
        _patternService = patternService;
        _progressService = progressService;
    }

    /// <summary>
    ///     Performs hierarchical decomposition of the repository
    /// </summary>
    public async Task<ModuleTree> DecomposeHierarchicallyAsync(
        string repositoryPath, EnhancedDependencyGraph graph,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting hierarchical decomposition for repository: {Path}", repositoryPath);

        // Sub-progress for Identification (0-20%)
        List<CodeComponent> components = null!;
        await _progressService.WithScalingAsync(0, 20,
            async () => { components = await _graphService.GetComponentsAsync(repositoryPath, cancellationToken); });

        // Sub-progress for Graph Build (20-100%)
        await _progressService.WithScalingAsync(20, 80,
            async () => { graph = await _graphService.BuildGraphAsync(components, cancellationToken); });

        // Identify Entry Points
        IdentifyEntryPoints(graph);

        // Create initial module tree
        var moduleTree = new ModuleTree();

        // Perform semantic clustering (Cycle -> Layer -> Directory -> Louvain)
        var semanticClusters = await PerformSemanticClusteringAsync(graph, repositoryPath, cancellationToken);

        // Build hierarchical structure
        await BuildHierarchyAsync(moduleTree, semanticClusters, graph, cancellationToken);

        // Ensure token limits are respected
        await EnforceTokenLimitsAsync(moduleTree, graph, cancellationToken);

        // Optimize Tree Balance
        await OptimizeTreeStructureAsync(moduleTree, cancellationToken);

        // Calculate Quality Metrics for all modules
        CalculateAllQualityMetrics(moduleTree, graph);

        // SYNC: Mirror the tree structure into the unified graph
        SyncTreeToGraph(moduleTree, graph);

        _logger.LogInformation("Completed hierarchical decomposition. Total modules: {Count}", moduleTree.Nodes.Count);

        return moduleTree;
    }

    private void SyncTreeToGraph(ModuleTree tree, EnhancedDependencyGraph graph)
    {
        _logger.LogInformation("Syncing ModuleTree to Unified Graph...");
        
        foreach (var module in tree.Nodes.Values.Concat(new[] { tree.Root }))
        {
            var metadata = new NodeMetadata
            {
                Id = module.Id,
                Type = "Module",
                Language = "None",
                EstimatedTokens = module.EstimatedTokens,
                CyclomaticComplexity = module.ComplexityScore,
                Properties = new Dictionary<string, object>
                {
                    ["Level"] = module.Level,
                    ["IsLeaf"] = module.IsLeaf,
                    ["Cohesion"] = module.QualityMetrics.Cohesion,
                    ["Coupling"] = module.QualityMetrics.Coupling,
                    ["MaintainabilityIndex"] = module.QualityMetrics.MaintainabilityIndex
                }
            };
            
            if (!string.IsNullOrEmpty(module.Description))
                metadata.Properties["Description"] = module.Description;

            graph.AddNode(module.Id, metadata);

            // Add ParentOf relationships (using ChildOf edge type)
            if (module.Parent != null)
            {
                graph.AddEdge(module.Id, module.Parent.Id, EdgeType.ChildOf, 1.0);
            }

            // Add Contains relationships for components
            foreach (var componentId in module.Components)
            {
                graph.AddEdge(module.Id, componentId, EdgeType.Contains, 1.0);
            }
        }
    }

    private void IdentifyEntryPoints(EnhancedDependencyGraph graph)
    {
        foreach (var node in graph.GetNodes())
            if (IsEntryPoint(node))
            {
                node.Metadata.Properties["Role"] = "EntryPoint";
                _logger.LogDebug("Identified entry point: {Id}", node.ComponentId);
            }
    }

    private bool IsEntryPoint(GraphNode node)
    {
        var content = node.Metadata.ContentSnippet;
        var lang = node.Metadata.Language;

        if (string.IsNullOrEmpty(content)) return false;

        if (lang.Equals("C#", StringComparison.OrdinalIgnoreCase) ||
            lang.Equals("Java", StringComparison.OrdinalIgnoreCase))
            return Regex.IsMatch(content, @"public\s+static\s+void\s+Main\s*\(", RegexOptions.IgnoreCase);
        if (lang.Equals("Python", StringComparison.OrdinalIgnoreCase))
            return content.Contains("if __name__ == \"__main__\":") || content.Contains("if __name__ == '__main__':");
        if (lang.Equals("JavaScript", StringComparison.OrdinalIgnoreCase) ||
            lang.Equals("TypeScript", StringComparison.OrdinalIgnoreCase))
            // Express, React, etc. common patterns
            return Regex.IsMatch(content, @"app\.listen\s*\(") ||
                   Regex.IsMatch(content, @"ReactDOM\.render") ||
                   content.Contains("bootstrap()");
        if (lang.Equals("C", StringComparison.OrdinalIgnoreCase) ||
            lang.Equals("C++", StringComparison.OrdinalIgnoreCase)) return Regex.IsMatch(content, @"int\s+main\s*\(");

        return false;
    }

    /// <summary>
    ///     Performs semantic clustering of components
    ///     NEW APPROACH: Graph-centric clustering with metadata for naming
    /// </summary>
    private async Task<Dictionary<string, List<string>>> PerformSemanticClusteringAsync(
        EnhancedDependencyGraph graph,
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Performing GRAPH-CENTRIC semantic clustering on {NodeCount} components", graph.NodeCount);

        var clusters = new Dictionary<string, List<string>>();
        var assignedNodes = new HashSet<string>();

        // STEP 1: Separate infrastructure/configuration files (these are special)
        var infraNodes = graph.GetNodes()
            .Where(n => n.Metadata.Type == "Configuration")
            .Select(n => n.ComponentId)
            .ToList();

        if (infraNodes.Any())
        {
            clusters["Project Infrastructure"] = infraNodes;
            foreach (var n in infraNodes) assignedNodes.Add(n);
            _logger.LogInformation("Separated {Count} infrastructure files", infraNodes.Count);
        }

        // STEP 2: Detect strongly connected components (cycles must stay together)
        try
        {
            var analysis = await _graphService.AnalyzeGraphAsync(graph, cancellationToken);
            var sccs = analysis.StronglyConnectedComponents;

            var sccIndex = 0;
            foreach (var scc in sccs)
                if (scc.Count > 1)
                {
                    var newNodes = scc.Where(n => !assignedNodes.Contains(n)).ToList();
                    if (newNodes.Count > 0)
                    {
                        clusters[$"Cycle_{sccIndex}"] = newNodes;
                        foreach (var n in newNodes) assignedNodes.Add(n);
                        sccIndex++;
                    }
                }

            if (sccIndex > 0)
                _logger.LogInformation("Detected {Count} strongly connected components (cycles)", sccIndex);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Graph analysis failed, proceeding without cycle detection");
        }

        // STEP 3: PRIMARY CLUSTERING - Graph-based Leiden community detection
        var unassignedNodes = graph.GetNodes()
            .Select(n => n.ComponentId)
            .Where(id => !assignedNodes.Contains(id))
            .ToHashSet();

        if (unassignedNodes.Any())
        {
            _logger.LogInformation("Running Leiden clustering on {Count} remaining nodes", unassignedNodes.Count);

            var communities = await PerformLeidenClusteringAsync(graph, unassignedNodes, cancellationToken);

            _logger.LogInformation("Leiden produced {Count} communities", communities.Count);

            // STEP 4: Name communities using metadata (directory, layer, patterns)
            foreach (var (communityId, members) in communities)
            {
                var name = GenerateSemanticClusterName(graph, members, repositoryPath);
                
                // Merge components if this cluster name already exists
                if (clusters.ContainsKey(name))
                {
                    clusters[name].AddRange(members);
                    _logger.LogDebug("Merged {Count} components into existing cluster '{Name}'", 
                        members.Count, name);
                }
                else
                {
                    clusters[name] = members;
                }
                
                foreach (var n in members) assignedNodes.Add(n);
            }
        }

        // Verify all nodes are assigned
        var totalNodes = graph.NodeCount;
        var missingNodes = graph.GetNodes()
            .Select(n => n.ComponentId)
            .Where(id => !assignedNodes.Contains(id))
            .ToList();

        if (missingNodes.Any())
        {
            _logger.LogWarning("WARNING: {Count} nodes were not assigned to any cluster!", missingNodes.Count);
            foreach (var missing in missingNodes.Take(10))
            {
                var node = graph.GetNode(missing);
                _logger.LogWarning("  - Unassigned: {Id} ({File})",
                    missing, node?.Metadata.FilePath ?? "unknown");
            }
        }

        _logger.LogInformation(
            "Semantic clustering complete: {ClusterCount} clusters created, {Assigned}/{Total} nodes assigned",
            clusters.Count, assignedNodes.Count, totalNodes);

        return clusters;
    }

    /// <summary>
    /// Generates a semantic name for a cluster based on metadata analysis
    /// Uses directory structure, architectural layers, and patterns as hints
    /// </summary>
    private string GenerateSemanticClusterName(
        EnhancedDependencyGraph graph,
        List<string> members,
        string repositoryPath)
    {
        if (members.Count == 0) return "Empty Cluster";

        // Strategy 1: Check if all members share a common directory
        var nodes = members.Select(m => graph.GetNode(m)).Where(n => n != null).ToList();

        var directories = nodes
            .Select(n => Path.GetDirectoryName(n!.Metadata.FilePath))
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct()
            .ToList();

        if (directories.Count == 1 && !string.IsNullOrEmpty(directories[0]))
        {
            // Single directory - use it as name
            var dir = directories[0];
            var relativePage = Path.GetRelativePath(repositoryPath, dir);
            if (relativePage != ".")
            {
                return SanitizeDirectoryName(relativePage);
            }
        }

        // Strategy 2: Check for dominant architectural layer
        var layerCounts = nodes
            .Select(n => _patternService.DetermineLayer(n!))
            .Where(l => l != ArchitecturalLayerType.Unknown)
            .GroupBy(l => l)
            .ToDictionary(g => g.Key, g => g.Count());

        if (layerCounts.Any())
        {
            var dominantLayer = layerCounts.OrderByDescending(kvp => kvp.Value).First();
            if ((double)dominantLayer.Value / nodes.Count >= 0.6) // 60% threshold
            {
                return dominantLayer.Key.ToString();
            }
        }

        // Strategy 3: Use common directory prefix if exists
        if (directories.Count > 1)
        {
            var commonPrefix = FindCommonDirectoryPrefix(directories, repositoryPath);
            if (!string.IsNullOrEmpty(commonPrefix))
            {
                return SanitizeDirectoryName(commonPrefix);
            }
        }

        // Strategy 4: Fallback to generic component name
        return $"Component_{members.Count}_Files";
    }

    /// <summary>
    /// Sanitizes directory name for use as cluster name
    /// </summary>
    private string SanitizeDirectoryName(string dirPath)
    {
        return dirPath
            .Replace(Path.DirectorySeparatorChar, ' ')
            .Replace(Path.AltDirectorySeparatorChar, ' ')
            .Replace('_', ' ')
            .Trim();
    }

    /// <summary>
    /// Finds common directory prefix among a set of directories
    /// </summary>
    private string FindCommonDirectoryPrefix(List<string> directories, string repositoryPath)
    {
        if (!directories.Any()) return string.Empty;

        var relativePaths = directories
            .Select(d => Path.GetRelativePath(repositoryPath, d))
            .Where(p => p != ".")
            .ToList();

        if (!relativePaths.Any()) return string.Empty;

        // Find common prefix path segments
        var pathSegments = relativePaths
            .Select(p => p.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .ToList();

        var commonSegments = new List<string>();
        var minLength = pathSegments.Min(p => p.Length);

        for (int i = 0; i < minLength; i++)
        {
            var segment = pathSegments[0][i];
            if (pathSegments.All(p => p[i] == segment))
            {
                commonSegments.Add(segment);
            }
            else
            {
                break;
            }
        }

        return commonSegments.Any() ? string.Join(" ", commonSegments) : string.Empty;
    }

    private Dictionary<string, List<string>> PerformDirectoryClustering(List<GraphNode> nodes, string repositoryPath)
    {
        var clusters = new Dictionary<string, List<string>>();

        // Group by directory
        var groups = nodes
            .GroupBy(n =>
            {
                var dir = Path.GetDirectoryName(n.Metadata.FilePath);
                if (string.IsNullOrEmpty(dir)) return "Root";

                // Use relative path for grouping to avoid full system paths in names
                var relativeDir = Path.GetRelativePath(repositoryPath, dir);
                return relativeDir == "." ? "Root" : relativeDir;
            })
            .ToList();

        foreach (var group in groups)
        {
            // If a directory has significant content, make it a cluster.
            // We can use a heuristic: at least 2 files or > 1000 tokens?
            // For now, purely directory based is a strong signal for Feature grouping.

            // Clean up name
            var rawName = group.Key;
            if (string.IsNullOrEmpty(rawName) || rawName == "." || rawName == "Root")
            {
                rawName = "Root";
            }
            else
            {
                // Replace path separators and underscores with spaces, then capitalize
                rawName = rawName.Replace(Path.DirectorySeparatorChar, ' ')
                    .Replace(Path.AltDirectorySeparatorChar, ' ')
                    .Replace('_', ' ');

                // Title Case / Capitalization
                rawName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(rawName.ToLower());
            }

            clusters[rawName] = group.Select(n => n.ComponentId).ToList();
        }

        return clusters;
    }

    /// <summary>
    /// Performs Leiden community detection - improved version of Louvain
    /// Ensures well-connected communities through refinement phase
    /// </summary>
    private Task<Dictionary<int, List<string>>> PerformLeidenClusteringAsync(
        EnhancedDependencyGraph graph,
        HashSet<string> nodeIds,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Leiden clustering on {NodeCount} nodes", nodeIds.Count);

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

        // Build edge weights
        var edgeWeights = new Dictionary<(string, string), double>();
        double totalWeight = 0;

        foreach (var nodeId in nodeIds)
        {
            var node = graph.GetNode(nodeId);
            if (node != null)
                foreach (var neighborId in node.OutEdges)
                    if (nodeIds.Contains(neighborId))
                    {
                        var w = 1.0;
                        totalWeight += w;
                        edgeWeights[(nodeId, neighborId)] = w;
                    }
        }

        if (totalWeight == 0) totalWeight = 1.0;

        var maxIterations = 10;
        var iteration = 0;
        bool improved = true;

        while (improved && iteration < maxIterations)
        {
            iteration++;
            improved = false;

            // Phase 1: Move nodes (like Louvain)
            var movePhaseImproved = MoveNodesPhase(
                graph, nodeIds, communities, communityNodes,
                edgeWeights, totalWeight, cancellationToken);

            if (movePhaseImproved)
                improved = true;

            // Phase 2: Refine communities (unique to Leiden)
            // Split communities that are not well-connected
            var refineImproved = RefineCommunitiesPhase(
                graph, nodeIds, communities, communityNodes,
                edgeWeights, ref nextCommunityId, cancellationToken);

            if (refineImproved)
                improved = true;

            // Phase 3: Aggregate (like Louvain)
            // For simplicity, we skip aggregation in this implementation
            // In full Leiden, you'd create a super-graph and recurse

            _logger.LogDebug("Leiden iteration {Iter}: {Communities} communities",
                iteration, communityNodes.Count);
        }

        _logger.LogInformation("Leiden clustering complete: {Communities} communities after {Iterations} iterations",
            communityNodes.Count, iteration);

        return Task.FromResult(communityNodes);
    }

    /// <summary>
    /// Move nodes to improve modularity (Louvain-like phase)
    /// </summary>
    private bool MoveNodesPhase(
        EnhancedDependencyGraph graph,
        HashSet<string> nodeIds,
        Dictionary<string, int> communities,
        Dictionary<int, List<string>> communityNodes,
        Dictionary<(string, string), double> edgeWeights,
        double totalWeight,
        CancellationToken cancellationToken)
    {
        bool improved = false;
        var randomizedNodes = nodeIds.OrderBy(x => Guid.NewGuid()).ToList();

        foreach (var nodeId in randomizedNodes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentComm = communities[nodeId];
            var bestComm = currentComm;
            double maxDeltaQ = 0;

            var nodeObj = graph.GetNode(nodeId);
            if (nodeObj == null) continue;

            // Calculate degree
            double k_i = 0;
            var neighborCommunities = new Dictionary<int, double>();

            foreach (var target in nodeObj.OutEdges)
                if (nodeIds.Contains(target))
                {
                    var w = edgeWeights.GetValueOrDefault((nodeId, target), 1.0);
                    k_i += w;
                    var c = communities[target];
                    neighborCommunities[c] = neighborCommunities.GetValueOrDefault(c, 0) + w;
                }

            foreach (var source in nodeObj.InEdges)
                if (nodeIds.Contains(source))
                {
                    var w = edgeWeights.GetValueOrDefault((source, nodeId), 1.0);
                    k_i += w;
                    var c = communities[source];
                    neighborCommunities[c] = neighborCommunities.GetValueOrDefault(c, 0) + w;
                }

            // Try moving to each neighbor community
            foreach (var (targetComm, edgesToComm) in neighborCommunities)
            {
                if (targetComm == currentComm) continue;

                // Calculate modularity gain
                double sigma_tot = 0;
                foreach (var peerId in communityNodes[targetComm])
                {
                    var peer = graph.GetNode(peerId);
                    if (peer != null) sigma_tot += peer.InEdges.Count + peer.OutEdges.Count;
                }

                var deltaQ = (edgesToComm / totalWeight) - (sigma_tot * k_i / (2 * totalWeight * totalWeight));

                if (deltaQ > maxDeltaQ)
                {
                    maxDeltaQ = deltaQ;
                    bestComm = targetComm;
                }
            }

            // Move node if improvement found
            if (bestComm != currentComm && maxDeltaQ > 0.0001)
            {
                communityNodes[currentComm].Remove(nodeId);
                if (communityNodes[currentComm].Count == 0)
                    communityNodes.Remove(currentComm);

                if (!communityNodes.ContainsKey(bestComm))
                    communityNodes[bestComm] = new List<string>();
                communityNodes[bestComm].Add(nodeId);
                communities[nodeId] = bestComm;

                improved = true;
            }
        }

        return improved;
    }

    /// <summary>
    /// Refine communities by splitting poorly connected ones (unique to Leiden)
    /// </summary>
    private bool RefineCommunitiesPhase(
        EnhancedDependencyGraph graph,
        HashSet<string> nodeIds,
        Dictionary<string, int> communities,
        Dictionary<int, List<string>> communityNodes,
        Dictionary<(string, string), double> edgeWeights,
        ref int nextCommunityId,
        CancellationToken cancellationToken)
    {
        bool refined = false;
        var communitiesToRefine = communityNodes.Keys.ToList();

        foreach (var commId in communitiesToRefine)
        {
            if (!communityNodes.ContainsKey(commId)) continue;
            var members = communityNodes[commId];

            // Check if community is well-connected
            if (members.Count <= 1) continue;

            // Find weakly connected nodes using BFS
            var visited = new HashSet<string>();
            var components = new List<HashSet<string>>();

            foreach (var startNode in members)
            {
                if (visited.Contains(startNode)) continue;

                var component = new HashSet<string>();
                var queue = new Queue<string>();
                queue.Enqueue(startNode);
                visited.Add(startNode);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    component.Add(current);

                    var node = graph.GetNode(current);
                    if (node == null) continue;

                    foreach (var neighbor in node.OutEdges.Concat(node.InEdges))
                    {
                        if (members.Contains(neighbor) && !visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                components.Add(component);
            }

            // If community has multiple disconnected components, split it
            if (components.Count > 1)
            {
                _logger.LogDebug("Splitting community {CommId} into {Count} components",
                    commId, components.Count);

                // Keep the largest component in the original community
                var largest = components.OrderByDescending(c => c.Count).First();
                communityNodes[commId] = largest.ToList();

                // Create new communities for other components
                foreach (var component in components.Where(c => c != largest))
                {
                    var newCommId = nextCommunityId++;
                    communityNodes[newCommId] = component.ToList();

                    foreach (var nodeId in component)
                    {
                        communities[nodeId] = newCommId;
                    }
                }

                refined = true;
            }
        }

        return refined;
    }

    /// <summary>
    /// Legacy Louvain implementation - kept for backwards compatibility
    /// Use PerformLeidenClusteringAsync for better results
    /// </summary>
    [Obsolete("Use PerformLeidenClusteringAsync instead")]
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

        int totalComponentsProcessed = 0;
        double totalTokensCounted = 0;
        int missingNodesCount = 0;

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
                    totalTokensCounted += node.Metadata.EstimatedTokens;
                    totalComponentsProcessed++;

                    if (node.Metadata.Properties.ContainsKey("Role") &&
                        node.Metadata.Properties["Role"].ToString() == "EntryPoint")
                        module.Metadata["HasEntryPoint"] = "true";
                }
                else
                {
                    missingNodesCount++;
                    _logger.LogWarning("Component {ComponentId} not found in graph during hierarchy build", componentId);
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

        _logger.LogInformation(
            "Hierarchy built: {ModuleCount} modules, {ComponentCount} components, {TokenCount} tokens, {MissingCount} missing nodes",
            clusters.Count, totalComponentsProcessed, totalTokensCounted, missingNodesCount);

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

        var bySubDir = components.GroupBy(c =>
        {
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
            // Fallback to Leiden
            var leidenResults = await PerformLeidenClusteringAsync(subGraph, module.Components, cancellationToken);
            subClusters = leidenResults.ToDictionary(k => k.Key.ToString(), v => v.Value);
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
            common = Path.GetDirectoryName(common);
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
}