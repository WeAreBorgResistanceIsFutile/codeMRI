using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Shared.Models;
using Microsoft.Extensions.Logging;
using ModuleTree = codeMRI.Shared.Models.ModuleTree;
using ModuleNode = codeMRI.Shared.Models.ModuleNode;

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
        private const int MinTokensPerModule = 4000; // Prevent too small fragments

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
        public async Task<ModuleTree> DecomposeHierarchicallyAsync(
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
            
            // Optimize Tree Balance
            await OptimizeTreeStructureAsync(moduleTree, cancellationToken);

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
            var assignedNodes = new HashSet<string>();

            // 0. Analyze Graph for SCCs (Cyclic Dependencies)
            try 
            {
                var analysis = await _graphService.AnalyzeGraphAsync(graph, cancellationToken);
                var sccs = analysis.StronglyConnectedComponents;
                
                int sccIndex = 0;
                foreach(var scc in sccs)
                {
                    if (scc.Count > 1)
                    {
                        // Keep cycles together
                        var newNodes = scc.Where(n => !assignedNodes.Contains(n)).ToList();
                        if (newNodes.Count > 0)
                        {
                            clusters[$"Cycle_{sccIndex}"] = newNodes;
                            foreach(var n in newNodes) assignedNodes.Add(n);
                            sccIndex++;
                        }
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
                foreach(var n in layer.Value) assignedNodes.Add(n);
            }

            var unassignedNodes = graph.GetNodes()
                .Select(n => n.ComponentId)
                .Where(id => !assignedNodes.Contains(id))
                .ToHashSet();

            // 2. For unassigned nodes, use Multi-Pass Louvain Community Detection
            if (unassignedNodes.Any())
            {
                var communities = await PerformLouvainClusteringAsync(graph, unassignedNodes, cancellationToken);
                foreach (var community in communities)
                {
                    clusters[$"Component_{community.Key}"] = community.Value;
                }
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
            int nextCommunityId = 0;
            foreach (var nodeId in nodeIds)
            {
                communities[nodeId] = nextCommunityId;
                communityNodes[nextCommunityId] = new List<string> { nodeId };
                nextCommunityId++;
            }

            bool globalImprovement = true;
            int globalIter = 0;
            int maxGlobalIterations = 5;

            // Calculate total weight (m)
            double m = 0;
            var edgeWeights = new Dictionary<(string, string), double>();
            
            foreach (var nodeId in nodeIds)
            {
                var node = graph.GetNode(nodeId);
                if (node != null)
                {
                    foreach (var neighborId in node.OutEdges)
                    {
                        if (nodeIds.Contains(neighborId)) 
                        {
                            // Weight adjustment: Cross-boundary edges might have lower weight to encourage separation?
                            // Or we treat all dependencies as 1.0 for now.
                            double w = 1.0;
                            m += w;
                            edgeWeights[(nodeId, neighborId)] = w;
                        }
                    }
                }
            }
            m = m > 0 ? m : 1.0; // Avoid division by zero

            while (globalImprovement && globalIter < maxGlobalIterations)
            {
                globalImprovement = false;
                globalIter++;
                
                bool localImprovement = true;
                int localIter = 0;
                int maxLocalIterations = 10;

                while (localImprovement && localIter < maxLocalIterations)
                {
                    localImprovement = false;
                    localIter++;

                    // Randomize node order to avoid bias
                    var randomizedNodes = nodeIds.OrderBy(x => Guid.NewGuid()).ToList();

                    foreach (var nodeId in randomizedNodes)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        int currentComm = communities[nodeId];
                        int bestComm = currentComm;
                        double maxDeltaQ = 0;

                        // Get neighboring communities
                        var neighborCommunities = new Dictionary<int, double>(); // CommID -> Weight to that Comm
                        var nodeObj = graph.GetNode(nodeId);
                        if (nodeObj == null) continue;

                        double k_i = 0; // Sum of weights incident to node i
                        
                        // Outgoing
                        foreach(var target in nodeObj.OutEdges) 
                        {
                            if(nodeIds.Contains(target)) 
                            {
                                double w = edgeWeights.GetValueOrDefault((nodeId, target), 1.0);
                                k_i += w;
                                int c = communities[target];
                                if(!neighborCommunities.ContainsKey(c)) neighborCommunities[c] = 0;
                                neighborCommunities[c] += w;
                            }
                        }
                        // Incoming
                        foreach(var source in nodeObj.InEdges)
                        {
                            if(nodeIds.Contains(source))
                            {
                                double w = edgeWeights.GetValueOrDefault((source, nodeId), 1.0);
                                k_i += w;
                                int c = communities[source];
                                if(!neighborCommunities.ContainsKey(c)) neighborCommunities[c] = 0;
                                neighborCommunities[c] += w;
                            }
                        }

                        // Evaluate moving
                        foreach (var kvp in neighborCommunities)
                        {
                            int targetComm = kvp.Key;
                            if (targetComm == currentComm) continue;
                            
                            double k_i_in = kvp.Value;
                            
                            // Calculate Sigma_tot (Sum of weights incident to nodes in targetComm)
                            // This is expensive to calc exactly, estimating based on node degrees in comm
                            double sigma_tot = 0; 
                            foreach(var peerId in communityNodes[targetComm])
                            {
                                var peer = graph.GetNode(peerId);
                                // Approximate degree
                                if(peer != null) sigma_tot += (peer.InEdges.Count + peer.OutEdges.Count); 
                            }

                            // Simplified Newman modularity gain
                            // Delta Q = [ k_i_in / 2m ] - [ (Sigma_tot * k_i) / (2m^2) ]
                            
                            double term1 = k_i_in / m; // Using m instead of 2m because we summed weights once? Standard formula uses 2m for undirected.
                            // For directed, it's complex. Let's use standard undirected approx.
                            double term2 = (sigma_tot * k_i) / (2 * m * m); 

                            double deltaQ = term1 - term2;

                            if (deltaQ > maxDeltaQ)
                            {
                                maxDeltaQ = deltaQ;
                                bestComm = targetComm;
                            }
                        }

                        if (bestComm != currentComm && maxDeltaQ > 0.0001) // Threshold to prevent jitter
                        {
                            // Move node
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
                
                // If needed, we could aggregate nodes into super-nodes here and recurse.
                // For now, iterative optimization on flat nodes is often sufficient for codebases.
            }

            return Task.FromResult(communityNodes);
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
            
            // Remove the original module (or mark as non-leaf)
            // Strategy: Turn 'module' into a parent node, move components to children
            
            var originalComponents = new HashSet<string>(module.Components);
            module.Components.Clear();
            module.IsLeaf = false;
            module.EstimatedTokens = 0; // Will be sum of children

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

                double childTokens = 0;

                // Add components to child module
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
                
                // Update parent stats
                module.EstimatedTokens += childTokens;

                // Check if new module needs further splitting
                if (childModule.EstimatedTokens > MaxTokensPerModule)
                {
                    modulesToSplit.Enqueue(childModule);
                }
            }
        }

        private async Task OptimizeTreeStructureAsync(ModuleTree tree, CancellationToken cancellationToken)
        {
            // Balance the tree: Merge small siblings if they are strongly related and sum < MaxTokens
            // Iterate bottom-up? 
            // For simplicity, iterate through parents and check children.
            
            foreach(var parent in tree.Nodes.Values.Where(n => !n.IsLeaf).ToList())
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var smallChildren = parent.Children.Where(c => c.EstimatedTokens < MinTokensPerModule).ToList();
                if (smallChildren.Count >= 2)
                {
                    // Try to merge small children
                    // Ideally verify coupling between them before merging
                    // For now, simple merge if size permits
                    
                    var merged = new HashSet<ModuleNode>();
                    
                    for (int i = 0; i < smallChildren.Count; i++)
                    {
                         if (merged.Contains(smallChildren[i])) continue;
                         
                         var c1 = smallChildren[i];
                         for (int j = i + 1; j < smallChildren.Count; j++)
                         {
                             var c2 = smallChildren[j];
                             if (merged.Contains(c2)) continue;

                             if (c1.EstimatedTokens + c2.EstimatedTokens < MaxTokensPerModule)
                             {
                                 // Merge c2 into c1
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
            
            foreach(var comp in source.Components) target.Components.Add(comp);
            foreach(var child in source.Children) target.AddChild(child);
            
            target.EstimatedTokens += source.EstimatedTokens;
            target.ComplexityScore += source.ComplexityScore;
            
            source.Parent?.Children.Remove(source);
            tree.Nodes.Remove(source.Id);
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
            var internalEdges = 0;
            var efferentCoupling = 0; // Ce: Outgoing to other modules
            var afferentCoupling = 0; // Ca: Incoming from other modules
            
            var componentCount = module.Components.Count;
            var abstractComponents = 0;

            foreach (var componentId in module.Components)
            {
                var node = graph.GetNode(componentId);
                if (node == null) continue;

                // Abstractness Check
                if (IsAbstract(node))
                {
                    abstractComponents++;
                }

                // Outgoing edges (Internal vs Efferent)
                foreach (var neighbor in node.OutEdges)
                {
                    if (module.Components.Contains(neighbor))
                    {
                        internalEdges++;
                    }
                    else
                    {
                        efferentCoupling++;
                    }
                }
                
                // Incoming edges (Afferent)
                foreach (var neighbor in node.InEdges)
                {
                    if (!module.Components.Contains(neighbor))
                    {
                        afferentCoupling++;
                    }
                }
            }

            // Cohesion (Relational Cohesion)
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

            // Coupling (Total external connections relative to total)
            double totalEdges = internalEdges + efferentCoupling + afferentCoupling;
            double coupling = 0;
            if (totalEdges > 0)
            {
                coupling = (efferentCoupling + afferentCoupling) / totalEdges;
            }

            // Instability (I = Ce / (Ca + Ce))
            double instability = 0;
            if (efferentCoupling + afferentCoupling > 0)
            {
                instability = (double)efferentCoupling / (efferentCoupling + afferentCoupling);
            }

            // Abstractness (A = Na / Nc)
            double abstractness = 0;
            if (componentCount > 0)
            {
                abstractness = (double)abstractComponents / componentCount;
            }

            // Distance from Main Sequence (D = |A + I - 1|)
            double distance = Math.Abs(abstractness + instability - 1);

            module.QualityMetrics.Cohesion = cohesion;
            module.QualityMetrics.Coupling = coupling;
            module.QualityMetrics.Complexity = module.ComplexityScore;
            module.QualityMetrics.Instability = instability;
            module.QualityMetrics.Abstractness = abstractness;
            module.QualityMetrics.DistanceFromMainSequence = distance;
            
            // Maintainability Index (Simplified)
            module.QualityMetrics.MaintainabilityIndex = Math.Max(0, 100 - (coupling * 20) - (module.ComplexityScore / 10.0));
        }

        private bool IsAbstract(GraphNode node)
        {
            return node.Metadata.Type.Contains("Interface", StringComparison.OrdinalIgnoreCase) ||
                   node.Metadata.Type.Contains("Abstract", StringComparison.OrdinalIgnoreCase);
        }
    }
}