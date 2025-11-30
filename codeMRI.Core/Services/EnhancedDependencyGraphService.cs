using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using codeMRI.Core.Interfaces;

namespace codeMRI.Core.Services;

public class EnhancedDependencyGraphService : IEnhancedDependencyGraphService
{
    private readonly ILogger<EnhancedDependencyGraphService> _logger;
    
    public EnhancedDependencyGraphService(ILogger<EnhancedDependencyGraphService> logger)
    {
        _logger = logger;
    }
    
    public async Task<EnhancedDependencyGraph> BuildGraphAsync(List<CodeComponent> components, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Building dependency graph for {ComponentCount} components", components.Count);
        
        var graph = new EnhancedDependencyGraph();
        
        // Build nodes
        foreach (var component in components)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var metadata = new NodeMetadata
            {
                Id = component.Id,
                Type = component.Type,
                LineCount = component.LineCount,
                CyclomaticComplexity = component.ComplexityScore,
                NestingDepth = EstimateNestingDepth(component),
                FanIn = 0, // Will be calculated later
                FanOut = component.Dependencies.Count,
                IsPublic = IsPublicComponent(component),
                HasDocumentation = !string.IsNullOrEmpty(component.Description),
                FilePath = component.FilePath,
                EstimatedTokens = EstimateComponentTokens(component)
            };
            
            graph.AddNode(component.Id, metadata);
        }
        
        // Build edges from dependencies
        foreach (var component in components)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            foreach (var dependencyName in component.Dependencies)
            {
                var targetComponent = components.FirstOrDefault(c => c.Name == dependencyName);
                if (targetComponent != null)
                {
                    graph.AddEdge(component.Id, targetComponent.Id, EdgeType.Dependency, 1.0);
                }
            }
        }
        
        // Update fan-in/fan-out metrics after all edges are built
        foreach (var node in graph.GetNodes())
        {
            node.Metadata.FanIn = node.InDegree;
            node.Metadata.FanOut = node.OutDegree;
        }
        
        _logger.LogInformation("Built graph with {NodeCount} nodes and {EdgeCount} edges", 
            graph.NodeCount, graph.EdgeCount);
        
        return await Task.FromResult(graph);
    }
    
    public async Task<GraphAnalysisResult> AnalyzeGraphAsync(EnhancedDependencyGraph graph, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing graph with {NodeCount} nodes", graph.NodeCount);
        
        var result = new GraphAnalysisResult();
        
        // Calculate PageRank
        result.PageRankScores = await CalculatePageRankAsync(graph, cancellationToken);
        
        // Find strongly connected components
        result.StronglyConnectedComponents = FindStronglyConnectedComponents(graph);
        
        // Find entry points (zero in-degree nodes)
        result.ZeroInDegreeNodes = graph.GetZeroInDegreeNodes().ToList();
        
        // Calculate node degrees
        foreach (var node in graph.GetNodes())
        {
            result.NodeDegrees[node.ComponentId] = node.InDegree;
        }
        
        // Calculate graph statistics
        result.Statistics = CalculateGraphStatistics(graph);
        
        _logger.LogInformation("Graph analysis completed");
        
        return await Task.FromResult(result);
    }
    
    public async Task<List<string>> IdentifyEntryPointsAsync(EnhancedDependencyGraph graph, CancellationToken cancellationToken = default)
    {
        var entryPoints = graph.GetZeroInDegreeNodes().ToList();
        
        // Additional filtering based on component type and naming patterns
        var filteredEntryPoints = entryPoints
            .Where(id => IsLikelyEntryPoint(graph.GetNode(id)))
            .ToList();
        
        return await Task.FromResult(filteredEntryPoints);
    }
    
    public async Task<Dictionary<string, double>> CalculateImportanceScoresAsync(EnhancedDependencyGraph graph, CancellationToken cancellationToken = default)
    {
        var analysis = await AnalyzeGraphAsync(graph, cancellationToken);
        var importanceScores = new Dictionary<string, double>();
        
        foreach (var node in graph.GetNodes())
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Combine PageRank with degree centrality and other factors
            var pageRankScore = analysis.PageRankScores.GetValueOrDefault(node.ComponentId, 0.0);
            var degreeScore = (node.InDegree + node.OutDegree) / (double)(graph.NodeCount * 2);
            var complexityScore = node.Metadata.CyclomaticComplexity / 100.0; // Normalize
            
            // Weighted combination
            var importanceScore = (pageRankScore * 0.5) + (degreeScore * 0.3) + (complexityScore * 0.2);
            importanceScores[node.ComponentId] = importanceScore;
        }
        
        return importanceScores;
    }
    
    private async Task<Dictionary<string, double>> CalculatePageRankAsync(EnhancedDependencyGraph graph, CancellationToken cancellationToken)
    {
        const double dampingFactor = 0.85;
        const int maxIterations = 100;
        const double tolerance = 1e-6;
        
        var pageRank = new Dictionary<string, double>();
        var nodes = graph.GetNodes().ToList();
        var n = nodes.Count;
        
        if (n == 0) return pageRank;
        
        // Initialize PageRank values
        foreach (var node in nodes)
        {
            pageRank[node.ComponentId] = 1.0 / n;
        }
        
        // Iterative calculation
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var newPageRank = new Dictionary<string, double>();
            var delta = 0.0;
            
            foreach (var node in nodes)
            {
                var rankSum = 0.0;
                
                // Sum PageRank from incoming edges
                foreach (var incomingNodeId in node.InEdges)
                {
                    var incomingNode = graph.GetNode(incomingNodeId);
                    if (incomingNode != null && incomingNode.OutEdges.Count > 0)
                    {
                        rankSum += pageRank[incomingNodeId] / incomingNode.OutEdges.Count;
                    }
                }
                
                newPageRank[node.ComponentId] = (1 - dampingFactor) / n + dampingFactor * rankSum;
                delta += Math.Abs(newPageRank[node.ComponentId] - pageRank[node.ComponentId]);
            }
            
            pageRank = newPageRank;
            
            if (delta < tolerance)
            {
                _logger.LogInformation("PageRank converged after {Iteration} iterations", iteration + 1);
                break;
            }
        }
        
        return pageRank;
    }

    public async Task<Dictionary<string, double>> CalculateBetweennessCentralityAsync(EnhancedDependencyGraph graph, CancellationToken cancellationToken = default)
    {
        var centrality = new Dictionary<string, double>();
        var nodes = graph.GetNodes().ToList();
        
        foreach (var node in nodes)
        {
            centrality[node.ComponentId] = 0.0;
        }
        
        // Calculate betweenness for each pair of nodes
        for (int i = 0; i < nodes.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            for (int j = i + 1; j < nodes.Count; j++)
            {
                var source = nodes[i];
                var target = nodes[j];
                
                // Find shortest paths from source to target
                var paths = FindAllShortestPaths(graph, source.ComponentId, target.ComponentId);
                
                // Count how many times each node appears on these paths
                foreach (var path in paths)
                {
                    for (int k = 1; k < path.Count - 1; k++) // Exclude source and target
                    {
                        centrality[path[k]] += 1.0 / paths.Count;
                    }
                }
            }
        }
        
        return await Task.FromResult(centrality);
    }

    public async Task<Dictionary<string, double>> CalculateClosenessCentralityAsync(EnhancedDependencyGraph graph, CancellationToken cancellationToken = default)
    {
        var centrality = new Dictionary<string, double>();
        var nodes = graph.GetNodes().ToList();
        
        foreach (var node in nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var distances = CalculateShortestDistances(graph, node.ComponentId);
            var sumDistance = distances.Values.Where(d => d > 0).Sum();
            
            if (sumDistance > 0)
            {
                centrality[node.ComponentId] = (nodes.Count - 1) / sumDistance;
            }
            else
            {
                centrality[node.ComponentId] = 0.0;
            }
        }
        
        return await Task.FromResult(centrality);
    }

    private List<List<string>> FindAllShortestPaths(EnhancedDependencyGraph graph, string source, string target)
    {
        var paths = new List<List<string>>();
        var queue = new Queue<List<string>>();
        var visited = new HashSet<string>();
        
        queue.Enqueue(new List<string> { source });
        visited.Add(source);
        
        while (queue.Count > 0)
        {
            var currentPath = queue.Dequeue();
            var lastNode = currentPath.Last();
            
            if (lastNode == target)
            {
                paths.Add(currentPath);
                continue;
            }
            
            var node = graph.GetNode(lastNode);
            if (node == null) continue;
            
            foreach (var neighbor in node.OutEdges)
            {
                if (!currentPath.Contains(neighbor))
                {
                    var newPath = new List<string>(currentPath) { neighbor };
                    queue.Enqueue(newPath);
                }
            }
        }
        
        // Filter to only shortest paths
        if (paths.Count == 0) return paths;
        
        var minLength = paths.Min(p => p.Count);
        return paths.Where(p => p.Count == minLength).ToList();
    }

    private Dictionary<string, double> CalculateShortestDistances(EnhancedDependencyGraph graph, string source)
    {
        var distances = new Dictionary<string, double>();
        var visited = new HashSet<string>();
        var queue = new Queue<(string Node, double Distance)>();
        
        foreach (var node in graph.GetNodes())
        {
            distances[node.ComponentId] = double.PositiveInfinity;
        }
        
        distances[source] = 0;
        queue.Enqueue((source, 0));
        
        while (queue.Count > 0)
        {
            var (currentNode, currentDistance) = queue.Dequeue();
            
            if (visited.Contains(currentNode)) continue;
            visited.Add(currentNode);
            
            var node = graph.GetNode(currentNode);
            if (node == null) continue;
            
            foreach (var neighbor in node.OutEdges)
            {
                var newDistance = currentDistance + 1;
                if (newDistance < distances[neighbor])
                {
                    distances[neighbor] = newDistance;
                    queue.Enqueue((neighbor, newDistance));
                }
            }
        }
        
        return distances;
    }
    
    private List<List<string>> FindStronglyConnectedComponents(EnhancedDependencyGraph graph)
    {
        var sccs = new List<List<string>>();
        var visited = new HashSet<string>();
        var stack = new Stack<string>();
        
        foreach (var node in graph.GetNodes())
        {
            if (!visited.Contains(node.ComponentId))
            {
                FindSCCRecursive(node.ComponentId, graph, visited, stack, sccs);
            }
        }
        
        return sccs.Where(scc => scc.Count > 1).ToList(); // Only return cycles (size > 1)
    }
    
    private void FindSCCRecursive(string nodeId, EnhancedDependencyGraph graph, HashSet<string> visited, 
        Stack<string> stack, List<List<string>> sccs)
    {
        visited.Add(nodeId);
        
        var node = graph.GetNode(nodeId);
        if (node != null)
        {
            foreach (var neighborId in node.OutEdges)
            {
                if (!visited.Contains(neighborId))
                {
                    FindSCCRecursive(neighborId, graph, visited, stack, sccs);
                }
            }
        }
        
        stack.Push(nodeId);
        
        if (stack.Count > 1 && stack.Peek() == nodeId)
        {
            // Found a strongly connected component
            var scc = new List<string>();
            var temp = new Stack<string>(stack);
            
            while (temp.Count > 0 && temp.Peek() != nodeId)
            {
                scc.Add(temp.Pop());
            }
            
            if (temp.Count > 0)
            {
                scc.Add(temp.Pop());
                sccs.Add(scc);
            }
        }
    }
    
    private GraphStatistics CalculateGraphStatistics(EnhancedDependencyGraph graph)
    {
        var nodes = graph.GetNodes().ToList();
        var edges = graph.GetEdges().ToList();
        
        return new GraphStatistics
        {
            TotalNodes = nodes.Count,
            TotalEdges = edges.Count,
            AverageDegree = edges.Count * 2.0 / Math.Max(1, nodes.Count),
            MaxInDegree = nodes.Any() ? nodes.Max(n => n.InDegree) : 0,
            MaxOutDegree = nodes.Any() ? nodes.Max(n => n.OutDegree) : 0,
            Density = nodes.Count > 1 ? edges.Count / (double)(nodes.Count * (nodes.Count - 1)) : 0,
            ConnectedComponents = CountConnectedComponents(graph)
        };
    }
    
    private int CountConnectedComponents(EnhancedDependencyGraph graph)
    {
        var visited = new HashSet<string>();
        var components = 0;
        
        foreach (var node in graph.GetNodes())
        {
            if (!visited.Contains(node.ComponentId))
            {
                components++;
                DFSVisit(node.ComponentId, graph, visited);
            }
        }
        
        return components;
    }
    
    private void DFSVisit(string nodeId, EnhancedDependencyGraph graph, HashSet<string> visited)
    {
        visited.Add(nodeId);
        var node = graph.GetNode(nodeId);
        
        if (node != null)
        {
            foreach (var neighborId in node.OutEdges.Concat(node.InEdges))
            {
                if (!visited.Contains(neighborId))
                {
                    DFSVisit(neighborId, graph, visited);
                }
            }
        }
    }
    
    private bool IsPublicComponent(CodeComponent component)
    {
        return component.Type.Equals("Controller", StringComparison.OrdinalIgnoreCase) ||
               component.Type.Equals("Service", StringComparison.OrdinalIgnoreCase) ||
               component.Name.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ||
               component.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ||
               component.Name.EndsWith("Manager", StringComparison.OrdinalIgnoreCase);
    }
    
    private bool IsLikelyEntryPoint(GraphNode? node)
    {
        if (node?.Metadata == null) return false;
        
        // Entry points are typically public APIs or main components
        return node.Metadata.IsPublic || 
               node.Metadata.Type.Equals("Controller", StringComparison.OrdinalIgnoreCase) ||
               node.ComponentId.Contains("Main") ||
               node.ComponentId.Contains("Entry");
    }
    
    private int EstimateNestingDepth(CodeComponent component)
    {
        // Simple heuristic based on component type and complexity
        if (component.ComplexityScore <= 5) return 1;
        if (component.ComplexityScore <= 10) return 2;
        if (component.ComplexityScore <= 20) return 3;
        return 4;
    }
    
    private double EstimateComponentTokens(CodeComponent component)
    {
        // Rough estimation: ~1.3 tokens per line of code + overhead
        var baseTokens = component.LineCount * 1.3;
        var complexityOverhead = component.ComplexityScore * 10;
        return baseTokens + complexityOverhead;
    }
    
    public async Task<Dictionary<string, double>> EstimateTokensAsync(EnhancedDependencyGraph graph, CancellationToken cancellationToken = default)
    {
        var tokenEstimates = new Dictionary<string, double>();
        
        foreach (var node in graph.GetNodes())
        {
            cancellationToken.ThrowIfCancellationRequested();
            tokenEstimates[node.ComponentId] = node.Metadata.EstimatedTokens;
        }
        
        return await Task.FromResult(tokenEstimates);
    }
    
    public async Task<ModuleTree> DecomposeHierarchicallyAsync(EnhancedDependencyGraph graph, int maxTokensPerModule = 32768, CancellationToken cancellationToken = default)
    {
        var moduleTree = new ModuleTree();
        var allComponentIds = graph.GetNodes().Select(n => n.ComponentId).ToHashSet();
        
        moduleTree.Root = new ModuleNode
        {
            Id = "root",
            Name = "Repository",
            Components = new HashSet<string>(), // Root doesn't contain components directly
            Level = 0,
            IsLeaf = false,
            EstimatedTokens = 0,
            ComplexityScore = 0
        };
        
        moduleTree.Nodes["root"] = moduleTree.Root;
        
        // Decompose by directory structure and check token thresholds
        var partitions = await PartitionByDirectoryStructureAsync(graph, cancellationToken);
        
        foreach (var partition in partitions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var partitionTokens = partition.Value.Sum(id => graph.GetNode(id)?.Metadata.EstimatedTokens ?? 0);
            
            // If partition is too large, split it further
            if (partitionTokens > maxTokensPerModule)
            {
                var subPartitions = await SplitPartitionBySize(graph, partition.Value, maxTokensPerModule, cancellationToken);
                foreach (var subPartition in subPartitions)
                {
                    var childNode = CreateModuleNode(graph, subPartition.Key, subPartition.Value, 1);
                    childNode.Parent = moduleTree.Root;
                    moduleTree.Root.Children.Add(childNode);
                    moduleTree.Nodes[childNode.Id] = childNode;
                }
            }
            else
            {
                var childNode = CreateModuleNode(graph, partition.Key, partition.Value, 1);
                childNode.Parent = moduleTree.Root;
                moduleTree.Root.Children.Add(childNode);
                moduleTree.Nodes[childNode.Id] = childNode;
            }
        }
        
        return await Task.FromResult(moduleTree);
    }
    
    private ModuleNode CreateModuleNode(EnhancedDependencyGraph graph, string name, HashSet<string> componentIds, int level)
    {
        return new ModuleNode
        {
            Id = $"module_{name}_{Guid.NewGuid().ToString("N").Substring(0, 8)}",
            Name = name,
            Components = componentIds,
            Level = level,
            IsLeaf = true,
            EstimatedTokens = (int)componentIds.Sum(id => graph.GetNode(id)?.Metadata.EstimatedTokens ?? 0),
            ComplexityScore = componentIds.Sum(id => graph.GetNode(id)?.Metadata.CyclomaticComplexity ?? 0)
        };
    }
    
    private async Task<Dictionary<string, HashSet<string>>> SplitPartitionBySize(
        EnhancedDependencyGraph graph, 
        HashSet<string> componentIds, 
        int maxTokensPerModule, 
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, HashSet<string>>();
        var componentsList = componentIds.ToList();
        var currentPartition = new HashSet<string>();
        var currentTokens = 0.0;
        var partitionIndex = 0;
        
        foreach (var componentId in componentsList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var componentTokens = graph.GetNode(componentId)?.Metadata.EstimatedTokens ?? 0;
            
            if (currentTokens + componentTokens > maxTokensPerModule && currentPartition.Count > 0)
            {
                // Start new partition
                result[$"partition_{partitionIndex}"] = new HashSet<string>(currentPartition);
                partitionIndex++;
                currentPartition.Clear();
                currentTokens = 0;
            }
            
            currentPartition.Add(componentId);
            currentTokens += componentTokens;
        }
        
        // Add final partition
        if (currentPartition.Count > 0)
        {
            result[$"partition_{partitionIndex}"] = new HashSet<string>(currentPartition);
        }
        
        return await Task.FromResult(result);
    }
    
    public async Task<Dictionary<string, HashSet<string>>> PartitionByDirectoryStructureAsync(EnhancedDependencyGraph graph, CancellationToken cancellationToken = default)
    {
        var partitions = new Dictionary<string, HashSet<string>>();
        
        foreach (var node in graph.GetNodes())
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var directory = ExtractDirectoryName(node.Metadata.FilePath);
            if (!partitions.ContainsKey(directory))
            {
                partitions[directory] = new HashSet<string>();
            }
            partitions[directory].Add(node.ComponentId);
        }
        
        return await Task.FromResult(partitions);
    }
    
    private string ExtractDirectoryName(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return "Root";
        
        var parts = filePath.Split('/', '\\');
        if (parts.Length > 1)
        {
            return parts[parts.Length - 2]; // Parent directory
        }
        return "Root";
    }
}