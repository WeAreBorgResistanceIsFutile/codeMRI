using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

/// <summary>
/// Service for calculating graph-theoretic quality metrics for module clustering
/// </summary>
public interface IGraphMetricsService
{
    /// <summary>
    /// Calculates modularity Q for a given clustering
    /// Q = Σ(e_ii - a_i²) where e_ii is fraction of edges within cluster i
    /// Higher Q (closer to 1.0) indicates better clustering
    /// </summary>
    double CalculateModularity(EnhancedDependencyGraph graph, Dictionary<string, int> communities);

    /// <summary>
    /// Calculates conductance for a cluster (lower is better)
    /// φ(S) = cut(S, V\S) / min(vol(S), vol(V\S))
    /// Measures how well-separated a cluster is from the rest
    /// </summary>
    double CalculateConductance(EnhancedDependencyGraph graph, HashSet<string> cluster);

    /// <summary>
    /// Calculates internal edge density of a cluster
    /// Higher density means more cohesive cluster
    /// </summary>
    double CalculateInternalDensity(EnhancedDependencyGraph graph, HashSet<string> cluster);

    /// <summary>
    /// Calculates coupling strength between two clusters
    /// Returns normalized edge count between clusters
    /// </summary>
    double CalculateCouplingStrength(
        EnhancedDependencyGraph graph,
        HashSet<string> cluster1,
        HashSet<string> cluster2);

    /// <summary>
    /// Evaluates overall quality of a clustering solution
    /// Returns a composite score combining modularity, conductance, etc.
    /// </summary>
    ClusterQualityMetrics EvaluateClustering(
        EnhancedDependencyGraph graph,
        Dictionary<string, int> communities);
}

/// <summary>
/// Quality metrics for a clustering solution
/// </summary>
public class ClusterQualityMetrics
{
    /// <summary>Modularity score (0 to 1, higher is better)</summary>
    public double Modularity { get; set; }

    /// <summary>Average conductance across clusters (0 to 1, lower is better)</summary>
    public double AverageConductance { get; set; }

    /// <summary>Average internal density (0 to 1, higher is better)</summary>
    public double AverageInternalDensity { get; set; }

    /// <summary>Number of clusters</summary>
    public int ClusterCount { get; set; }

    /// <summary>Composite quality score (0 to 1, higher is better)</summary>
    public double CompositeScore { get; set; }
}
