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
        private const int MaxTokensPerModule = 32768;

        public HierarchicalDecompositionService(
            ILogger<HierarchicalDecompositionService> logger,
            IEnhancedDependencyGraphService graphService)
        {
            _logger = logger;
            _graphService = graphService;
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

            // Perform semantic clustering
            var semanticClusters = await PerformSemanticClusteringAsync(graph, cancellationToken);

            // Build hierarchical structure
            await BuildHierarchyAsync(moduleTree, semanticClusters, graph, cancellationToken);

            // Ensure token limits are respected
            await EnforceTokenLimitsAsync(moduleTree, graph, cancellationToken);

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
            var unassignedNodes = graph.GetNodes().Select(n => n.ComponentId).ToHashSet();

            // Group by architectural role (e.g., Controllers, Services, etc.)
            var roleGroups = graph.GetNodes()
                .GroupBy(n => DetermineArchitecturalRole(n))
                .OrderByDescending(g => g.Count());

            foreach (var group in roleGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var groupNodes = group.Select(n => n.ComponentId).ToList();
                clusters[group.Key] = groupNodes;
                unassignedNodes.ExceptWith(groupNodes);
            }

            // For remaining nodes, perform Louvain-like community detection
            if (unassignedNodes.Any())
            {
                var communityClusters = await DetectCommunitiesAsync(graph, unassignedNodes, cancellationToken);
                foreach (var cluster in communityClusters)
                {
                    clusters[$"Module_{Guid.NewGuid().ToString("N").Substring(0, 6)}"] = cluster.Value;
                }
            }

            return clusters;
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
            var subClusters = await PerformSemanticClusteringAsync(subGraph, cancellationToken);
            
            // Remove the original module
            module.Parent?.Children.Remove(module);
            moduleTree.Nodes.Remove(module.Id);

            // Add new child modules
            foreach (var (clusterName, componentIds) in subClusters)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var childModule = new ModuleNode
                {
                    Id = $"{module.Id}_{clusterName.ToLowerInvariant()}",
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

        /// <summary>
        /// Detects communities in the graph using a Louvain-like algorithm
        /// </summary>
        private Task<Dictionary<string, List<string>>> DetectCommunitiesAsync(
            EnhancedDependencyGraph graph,
            HashSet<string> nodeIds,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Detecting communities in graph with {Count} nodes", nodeIds.Count);
            
            // Simplified community detection - in practice, use a proper implementation
            var communities = new Dictionary<string, List<string>>();
            var visited = new HashSet<string>();
            
            foreach (var nodeId in nodeIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                if (visited.Contains(nodeId)) continue;
                
                var community = new List<string> { nodeId };
                visited.Add(nodeId);
                
                // Simple BFS to find connected components
                var queue = new Queue<string>();
                queue.Enqueue(nodeId);
                
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    var currentNode = graph.GetNode(current);
                    if (currentNode == null) continue;
                    
                    foreach (var neighbor in currentNode.OutEdges.Concat(currentNode.InEdges))
                    {
                        if (nodeIds.Contains(neighbor) && !visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            community.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }
                
                communities[$"Community_{communities.Count + 1}"] = community;
            }

            return Task.FromResult(communities);
        }

        /// <summary>
        /// Determines the architectural role of a component
        /// </summary>
        private string DetermineArchitecturalRole(GraphNode node)
        {
            // Simple heuristic-based role detection
            var name = node.ComponentId.ToLowerInvariant();
            var type = node.Metadata.Type?.ToLowerInvariant() ?? "";

            if (name.Contains("controller") || type.Contains("controller"))
                return "Controllers";
            if (name.Contains("service") || type.Contains("service"))
                return "Services";
            if (name.Contains("repository") || type.Contains("repository"))
                return "Repositories";
            if (name.Contains("model") || type.Contains("model") || type.Contains("dto"))
                return "Models";
            if (name.Contains("view") || name.Contains("page") || name.Contains("component"))
                return "UI Components";
            if (name.Contains("util") || name.Contains("helper"))
                return "Utilities";
            
            return "Other";
        }
    }

}
