using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services;

/// <summary>
/// Implementation of graph-theoretic quality metrics for evaluating clustering solutions
/// </summary>
public class GraphMetricsService : IGraphMetricsService
{
    private readonly ILogger<GraphMetricsService> _logger;

    public GraphMetricsService(ILogger<GraphMetricsService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calculates Newman's modularity Q
    /// Q = (1/2m) * Σ[A_ij - (k_i * k_j)/2m] * δ(c_i, c_j)
    /// where m = total edges, A_ij = adjacency matrix, k_i = degree of node i
    /// </summary>
    public double CalculateModularity(EnhancedDependencyGraph graph, Dictionary<string, int> communities)
    {
        var nodes = graph.GetNodes().ToList();
        if (nodes.Count == 0) return 0.0;

        // Calculate total edge weight (2m in the formula)
        double totalEdgeWeight = 0;
        var edgeWeights = new Dictionary<(string, string), double>();

        foreach (var node in nodes)
        {
            foreach (var neighbor in node.OutEdges)
            {
                var weight = 1.0; // Can be enhanced with actual edge weights later
                edgeWeights[(node.ComponentId, neighbor)] = weight;
                totalEdgeWeight += weight;
            }
        }

        if (totalEdgeWeight == 0) return 0.0;

        // Calculate node degrees
        var degrees = new Dictionary<string, double>();
        foreach (var node in nodes)
        {
            degrees[node.ComponentId] = node.OutEdges.Count + node.InEdges.Count;
        }

        // Calculate modularity
        double modularity = 0.0;

        foreach (var node1 in nodes)
        {
            if (!communities.ContainsKey(node1.ComponentId)) continue;

            foreach (var node2 in nodes)
            {
                if (!communities.ContainsKey(node2.ComponentId)) continue;

                // Check if nodes are in same community
                if (communities[node1.ComponentId] != communities[node2.ComponentId])
                    continue;

                // A_ij (actual edge)
                double aij = edgeWeights.ContainsKey((node1.ComponentId, node2.ComponentId)) ? 1.0 : 0.0;

                // Expected edges: (k_i * k_j) / 2m
                double ki = degrees.GetValueOrDefault(node1.ComponentId, 0);
                double kj = degrees.GetValueOrDefault(node2.ComponentId, 0);
                double expected = (ki * kj) / (2.0 * totalEdgeWeight);

                modularity += (aij - expected);
            }
        }

        modularity /= (2.0 * totalEdgeWeight);

        _logger.LogDebug("Calculated modularity: {Modularity:F4} for {Communities} communities",
            modularity, communities.Values.Distinct().Count());

        return modularity;
    }

    /// <summary>
    /// Calculates conductance: ratio of edges leaving cluster to total edges touching cluster
    /// φ(S) = cut(S, V\S) / min(vol(S), vol(V\S))
    /// </summary>
    public double CalculateConductance(EnhancedDependencyGraph graph, HashSet<string> cluster)
    {
        if (cluster.Count == 0) return 1.0; // Worst conductance for empty cluster

        int cutEdges = 0;      // Edges from S to V\S
        int volumeS = 0;       // Total degree of nodes in S
        int totalVolume = 0;   // Total degree of all nodes

        var allNodes = graph.GetNodes().Select(n => n.ComponentId).ToHashSet();
        var complement = new HashSet<string>(allNodes.Except(cluster));

        foreach (var nodeId in cluster)
        {
            var node = graph.GetNode(nodeId);
            if (node == null) continue;

            int nodeDegree = node.OutEdges.Count + node.InEdges.Count;
            volumeS += nodeDegree;
            totalVolume += nodeDegree;

            // Count edges leaving the cluster
            foreach (var neighbor in node.OutEdges.Concat(node.InEdges))
            {
                if (complement.Contains(neighbor))
                    cutEdges++;
            }
        }

        // Add degree of nodes outside cluster to total volume
        foreach (var nodeId in complement)
        {
            var node = graph.GetNode(nodeId);
            if (node != null)
                totalVolume += node.OutEdges.Count + node.InEdges.Count;
        }

        int volumeComplement = totalVolume - volumeS;

        if (volumeS == 0 || volumeComplement == 0) return 1.0;

        double conductance = (double)cutEdges / Math.Min(volumeS, volumeComplement);

        return Math.Min(conductance, 1.0); // Cap at 1.0
    }

    /// <summary>
    /// Calculates internal edge density: actual internal edges / possible internal edges
    /// </summary>
    public double CalculateInternalDensity(EnhancedDependencyGraph graph, HashSet<string> cluster)
    {
        if (cluster.Count <= 1) return 1.0;

        int internalEdges = 0;

        foreach (var nodeId in cluster)
        {
            var node = graph.GetNode(nodeId);
            if (node == null) continue;

            foreach (var neighbor in node.OutEdges)
            {
                if (cluster.Contains(neighbor))
                    internalEdges++;
            }
        }

        // Maximum possible edges in directed graph: n * (n - 1)
        int maxPossibleEdges = cluster.Count * (cluster.Count - 1);

        if (maxPossibleEdges == 0) return 1.0;

        return (double)internalEdges / maxPossibleEdges;
    }

    /// <summary>
    /// Calculates coupling strength as normalized edge count between two clusters
    /// </summary>
    public double CalculateCouplingStrength(
        EnhancedDependencyGraph graph,
        HashSet<string> cluster1,
        HashSet<string> cluster2)
    {
        if (cluster1.Count == 0 || cluster2.Count == 0) return 0.0;

        int edgesBetween = 0;

        foreach (var nodeId in cluster1)
        {
            var node = graph.GetNode(nodeId);
            if (node == null) continue;

            foreach (var neighbor in node.OutEdges.Concat(node.InEdges))
            {
                if (cluster2.Contains(neighbor))
                    edgesBetween++;
            }
        }

        // Normalize by maximum possible edges between clusters
        int maxPossible = cluster1.Count * cluster2.Count * 2; // Both directions

        return maxPossible > 0 ? (double)edgesBetween / maxPossible : 0.0;
    }

    /// <summary>
    /// Evaluates overall clustering quality using composite metrics
    /// </summary>
    public ClusterQualityMetrics EvaluateClustering(
        EnhancedDependencyGraph graph,
        Dictionary<string, int> communities)
    {
        var modularity = CalculateModularity(graph, communities);

        // Group nodes by community
        var clusterMap = new Dictionary<int, HashSet<string>>();
        foreach (var kvp in communities)
        {
            if (!clusterMap.ContainsKey(kvp.Value))
                clusterMap[kvp.Value] = new HashSet<string>();
            clusterMap[kvp.Value].Add(kvp.Key);
        }

        // Calculate average conductance and density
        double totalConductance = 0;
        double totalDensity = 0;
        int clusterCount = clusterMap.Count;

        foreach (var cluster in clusterMap.Values)
        {
            totalConductance += CalculateConductance(graph, cluster);
            totalDensity += CalculateInternalDensity(graph, cluster);
        }

        double avgConductance = clusterCount > 0 ? totalConductance / clusterCount : 1.0;
        double avgDensity = clusterCount > 0 ? totalDensity / clusterCount : 0.0;

        // Composite score: weighted combination
        // Higher modularity and density are good, lower conductance is good
        double compositeScore = (modularity * 0.5) + (avgDensity * 0.3) + ((1.0 - avgConductance) * 0.2);

        _logger.LogInformation(
            "Clustering quality - Modularity: {Mod:F3}, Conductance: {Cond:F3}, Density: {Dens:F3}, Composite: {Comp:F3}",
            modularity, avgConductance, avgDensity, compositeScore);

        return new ClusterQualityMetrics
        {
            Modularity = modularity,
            AverageConductance = avgConductance,
            AverageInternalDensity = avgDensity,
            ClusterCount = clusterCount,
            CompositeScore = compositeScore
        };
    }
}
