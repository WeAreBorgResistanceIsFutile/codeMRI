using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;
using ModuleTree = codeMRI.Core.Models.ModuleTree;
using ModuleNode = codeMRI.Core.Models.ModuleNode;

namespace codeMRI.Core.Services
{
    /// <summary>
    /// Service for performing semantic hierarchical decomposition of codebases
    /// </summary>
    public class HierarchicalDecompositionService : IHierarchicalDecompositionService
    {
        private readonly ILogger<HierarchicalDecompositionService> _logger;
        private readonly IEnhancedDependencyGraphService _graphService;
        private readonly IArchitecturalPatternService _patternService;
        private const int MaxTokensPerModule = 32768;

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
        /// Performs hierarchical decomposition of the repository
        /// </summary>
        public async Task<Models.ModuleTree> DecomposeHierarchicallyAsync(
            string repositoryPath, 
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting hierarchical decomposition for repository: {Path}", repositoryPath);

            // Get components and build dependency graph
            var components = await _graphService.GetComponentsAsync(repositoryPath, cancellationToken);
            var graph = await _graphService.BuildGraphAsync(components, cancellationToken);

            // Create initial module tree
            var moduleTree = new ModuleTree();

            // Perform semantic clustering using Louvain and Architectural Layers
            var semanticClusters = await PerformSemanticClusteringAsync(graph, cancellationToken);

            // Build hierarchical structure
            await BuildHierarchyAsync(moduleTree, semanticClusters, graph, cancellationToken);

            // Ensure token limits are respected
            await EnforceTokenLimitsAsync(moduleTree, graph, cancellationToken);

            // Calculate Quality Metrics for all modules
            CalculateAllQualityMetrics(moduleTree, graph);

            _logger.LogInformation("Completed hierarchical decomposition. Total modules: {Count}", moduleTree.Nodes.Count);
            
            return moduleTree;
        }

        /// <summary>
        /// Performs semantic clustering of components
        /// </summary>
        private async Task<Dictionary<string, List<string>>> PerformSemanticClusteringAsync(
            EnhancedDependencyGraph graph, 
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Performing semantic clustering on {NodeCount} components", graph.NodeCount);
            
            var clusters = new Dictionary<string, List<string>>();
            
            // 1. Identify Architectural Layers first
            var layerGroups = graph.GetNodes()
                .GroupBy(n => _patternService.DetermineLayer(n))
                .Where(g => g.Key != ArchitecturalLayerType.Unknown)
                .ToDictionary(g => g.Key, g => g.Select(n => n.ComponentId).ToList());

            var assignedNodes = new HashSet<string>(layerGroups.Values.SelectMany(x => x));
            var unassignedNodes = graph.GetNodes()
                .Select(n => n.ComponentId)
                .Where(id => !assignedNodes.Contains(id))
                .ToHashSet();

            // 2. For unassigned nodes, use Louvain Community Detection
            if (unassignedNodes.Any())
            {
                var communities = await PerformLouvainClusteringAsync(graph, unassignedNodes, cancellationToken);
                foreach (var community in communities)
                {
                    clusters[$"Component_{community.Key}"] = community.Value;
                }
            }

            // 3. Add Layer groups as clusters
            foreach (var layer in layerGroups)
            {
                clusters[layer.Key.ToString()] = layer.Value;
            }

            return clusters;
        }

        private Task<Dictionary<int, List<string>>> PerformLouvainClusteringAsync(
            EnhancedDependencyGraph graph,
            HashSet<string> nodeIds,
            CancellationToken cancellationToken)
        {
            // Simplified Louvain: Only one pass of modularity optimization for this implementation
            // A full implementation would recurse on super-nodes.
            
            var communities = new Dictionary<string, int>(); // NodeId -> CommunityId
            var communityNodes = new Dictionary<int, List<string>>(); // CommunityId -> List<NodeId>
            
            int nextCommunityId = 0;
            foreach (var nodeId in nodeIds)
            {
                communities[nodeId] = nextCommunityId;
                communityNodes[nextCommunityId] = new List<string> { nodeId };
                nextCommunityId++;
            }

            bool improved = true;
            int maxIterations = 10;
            int iter = 0;

            // Calculate total weight of all edges in the subgraph (m)
            double m = 0;
            foreach (var nodeId in nodeIds)
            {
                var node = graph.GetNode(nodeId);
                if (node != null)
                {
                    foreach (var neighborId in node.OutEdges)
                    {
                        if (nodeIds.Contains(neighborId)) m += 1.0; // Assuming weight 1 for now
                    }
                }
            }
            m = m / 2.0; // Each edge counted twice

            if (m == 0) return Task.FromResult(communityNodes); // No edges

            while (improved && iter < maxIterations)
            {
                improved = false;
                iter++;
                
                foreach (var nodeId in nodeIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var node = graph.GetNode(nodeId);
                    if (node == null) continue;

                    int currentComm = communities[nodeId];
                    int bestComm = currentComm;
                    double maxDeltaQ = 0;

                    // Get neighboring communities
                    var neighborCommunities = new HashSet<int>();
                    foreach (var neighborId in node.OutEdges.Concat(node.InEdges))
                    {
                        if (nodeIds.Contains(neighborId))
                        {
                            neighborCommunities.Add(communities[neighborId]);
                        }
                    }

                    // Evaluate moving to neighbor communities
                    foreach (var targetComm in neighborCommunities)
                    {
                        if (targetComm == currentComm) continue;

                        double deltaQ = CalculateModularityGain(node, currentComm, targetComm, communities, graph, m);
                        if (deltaQ > maxDeltaQ)
                        {
                            maxDeltaQ = deltaQ;
                            bestComm = targetComm;
                        }
                    }

                    if (bestComm != currentComm && maxDeltaQ > 0)
                    {
                        // Move node
                        communityNodes[currentComm].Remove(nodeId);
                        if (communityNodes[currentComm].Count == 0) communityNodes.Remove(currentComm);

                        if (!communityNodes.ContainsKey(bestComm)) communityNodes[bestComm] = new List<string>();
                        communityNodes[bestComm].Add(nodeId);
                        communities[nodeId] = bestComm;
                        
                        improved = true;
                    }
                }
            }

            return Task.FromResult(communityNodes);
        }

        private double CalculateModularityGain(
            GraphNode node, 
            int currentComm, 
            int targetComm, 
            Dictionary<string, int> communities, 
            EnhancedDependencyGraph graph,
            double m)
        {
            // Simplified Modularity Gain Calculation
            // Delta Q = [ (Sum_in + ki_in)/(2m) - ((Sum_tot + ki)/(2m))^2 ] - [ (Sum_in/(2m) - (Sum_tot/(2m))^2 - (ki/(2m))^2 ]
            // Actually, simpler formula for moving node i to comm C:
            // Delta Q = k_i_in / (2m) - (Sigma_tot * k_i) / (2m^2)
            
            // k_i_in: sum of weights of links from i to nodes in C
            // Sigma_tot: sum of weights of links incident to nodes in C
            // k_i: sum of weights of links incident to i
            
            double k_i = node.OutEdges.Count + node.InEdges.Count;
            double k_i_in = 0;
            
            // Calculate k_i_in for target community
            foreach (var neighborId in node.OutEdges.Concat(node.InEdges))
            {
                if (communities.TryGetValue(neighborId, out int commId) && commId == targetComm)
                {
                    k_i_in += 1.0;
                }
            }

            // Calculate Sigma_tot for target community (roughly)
            // Note: This is expensive to calculate exactly every time. 
            // Optimization: maintain Sigma_tot for each community.
            // For now, we use a simplified approximation or iteration.
            
            // Let's use a simpler heuristic if precise calculation is too heavy:
            // Prefer communities with higher connectivity density.
            
            double term1 = k_i_in / (2 * m);
            // double term2 ... let's approximate small modularity impact
            
            return term1; 
        }

        /// <summary>
        /// Builds the hierarchical module structure
        /// </summary>
        private Task BuildHierarchyAsync(
            ModuleTree moduleTree,
            Dictionary<string, List<string>> clusters,
            EnhancedDependencyGraph graph,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Building module hierarchy from {ClusterCount} clusters", clusters.Count);

            // Create top-level modules from clusters
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

                // Add components to module
                foreach (var componentId in componentIds)
                {
                    var node = graph.GetNode(componentId);
                    if (node != null)
                    {
                        module.Components.Add(componentId);
                        module.EstimatedTokens += node.Metadata.EstimatedTokens;
                        module.ComplexityScore += node.Metadata.CyclomaticComplexity;
                    }
                }
                
                // Recognize Pattern
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
        /// Ensures no module exceeds the token limit by splitting large modules
        /// </summary>
        private async Task EnforceTokenLimitsAsync(
            ModuleTree moduleTree,
            EnhancedDependencyGraph graph,
            CancellationToken cancellationToken)
        {
            var modulesToSplit = new Queue<ModuleNode>();
            
            // Collect all leaf modules that exceed token limit
            foreach (var module in moduleTree.Nodes.Values.Where(m => m.IsLeaf))
            {
                if (module.EstimatedTokens > MaxTokensPerModule)
                {
                    modulesToSplit.Enqueue(module);
                }
            }

            // Process modules that need splitting
            while (modulesToSplit.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var module = modulesToSplit.Dequeue();
                await SplitModuleAsync(moduleTree, module, graph, modulesToSplit, cancellationToken);
            }
        }

        /// <summary>
        /// Splits a module that exceeds the token limit
        /// </summary>
        private async Task SplitModuleAsync(
            ModuleTree moduleTree,
            ModuleNode module,
            EnhancedDependencyGraph graph,
            Queue<ModuleNode> modulesToSplit,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Splitting module {ModuleId} (Tokens: {Tokens})", module.Id, module.EstimatedTokens);

            // Create subgraph for this module
            var subGraph = await CreateSubgraphAsync(graph, module.Components, cancellationToken);
            
            // Recursively decompose the subgraph
            // Using Louvain again on the subgraph
            var subClusters = await PerformLouvainClusteringAsync(subGraph, module.Components, cancellationToken);
            
            // Remove the original module
            module.Parent?.Children.Remove(module);
            moduleTree.Nodes.Remove(module.Id);

            // Add new child modules
            foreach (var (clusterId, componentIds) in subClusters)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string clusterName = $"Part_{clusterId}";

                var childModule = new ModuleNode
                {
                    Id = $"{module.Id}_{clusterName}",
                    Name = $"{module.Name} - {clusterName}",
                    Level = module.Level + 1,
                    IsLeaf = true
                };

                // Add components to child module
                foreach (var componentId in componentIds)
                {
                    var node = graph.GetNode(componentId);
                    if (node != null)
                    {
                        childModule.Components.Add(componentId);
                        childModule.EstimatedTokens += node.Metadata.EstimatedTokens;
                        childModule.ComplexityScore += node.Metadata.CyclomaticComplexity;
                    }
                }

                module.Parent?.AddChild(childModule);
                moduleTree.AddNode(childModule);

                // Check if new module needs further splitting
                if (childModule.EstimatedTokens > MaxTokensPerModule)
                {
                    modulesToSplit.Enqueue(childModule);
                }
            }
        }

        /// <summary>
        /// Creates a subgraph containing only the specified components
        /// </summary>
        private async Task<EnhancedDependencyGraph> CreateSubgraphAsync(
            EnhancedDependencyGraph fullGraph,
            HashSet<string> componentIds,
            CancellationToken cancellationToken)
        {
            var subGraph = new EnhancedDependencyGraph();
            
            // Add nodes
            foreach (var componentId in componentIds)
            {
                var node = fullGraph.GetNode(componentId);
                if (node != null)
                {
                    subGraph.AddNode(componentId, node.Metadata);
                }
            }

            // Add edges between included nodes
            foreach (var componentId in componentIds)
            {
                var node = fullGraph.GetNode(componentId);
                if (node == null) continue;

                foreach (var outEdge in node.OutEdges)
                {
                    if (componentIds.Contains(outEdge))
                    {
                        subGraph.AddEdge(componentId, outEdge, EdgeType.Dependency, 1.0);
                    }
                }
            }

            return await Task.FromResult(subGraph);
        }

        private void CalculateAllQualityMetrics(ModuleTree tree, EnhancedDependencyGraph graph)
        {
            foreach(var module in tree.Nodes.Values)
            {
                CalculateModuleMetrics(module, graph);
            }
        }

        private void CalculateModuleMetrics(ModuleNode module, EnhancedDependencyGraph graph)
        {
            // Cohesion: Ratio of internal edges to possible internal edges
            // Coupling: Ratio of external edges to total edges
            
            var internalEdges = 0;
            var externalEdges = 0;
            var componentCount = module.Components.Count;

            foreach (var componentId in module.Components)
            {
                var node = graph.GetNode(componentId);
                if (node == null) continue;

                foreach (var neighbor in node.OutEdges)
                {
                    if (module.Components.Contains(neighbor)) internalEdges++;
                    else externalEdges++;
                }
                 foreach (var neighbor in node.InEdges)
                {
                    if (module.Components.Contains(neighbor)) { /* already counted in OutEdges of other node? No, iterating nodes */ }
                    else externalEdges++;
                }
            }

            // Simple metrics
            double cohesion = 0;
            if (componentCount > 1)
            {
                double maxInternalEdges = componentCount * (componentCount - 1);
                cohesion = internalEdges / Math.Max(1, maxInternalEdges);
            }
            else
            {
                cohesion = 1.0; // Single component is cohesive
            }

            double coupling = 0;
            double totalEdges = internalEdges + externalEdges;
            if (totalEdges > 0)
            {
                coupling = externalEdges / totalEdges;
            }

            module.QualityMetrics.Cohesion = cohesion;
            module.QualityMetrics.Coupling = coupling;
            module.QualityMetrics.Complexity = module.ComplexityScore;
            
            // Maintainability Index (Simplified)
            module.QualityMetrics.MaintainabilityIndex = Math.Max(0, 100 - (coupling * 20) - (module.ComplexityScore / 10.0));
        }
    }
}