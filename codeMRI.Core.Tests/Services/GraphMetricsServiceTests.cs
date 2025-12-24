using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class GraphMetricsServiceTests
{
    private GraphMetricsService _service;
    private Mock<ILogger<GraphMetricsService>> _loggerMock;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<GraphMetricsService>>();
        _service = new GraphMetricsService(_loggerMock.Object);
    }

    [Test]
    public void CalculateModularity_WithPerfectClustering_ShouldReturnPositiveValue()
    {
        // Arrange - Create two well-separated clusters
        var graph = new EnhancedDependencyGraph();
        
        // Cluster 1: A-B (connected)
        graph.AddNode("A", new NodeMetadata { FilePath = "A.cs", EstimatedTokens = 100 });
        graph.AddNode("B", new NodeMetadata { FilePath = "B.cs", EstimatedTokens = 100 });
        graph.AddEdge("A", "B", EdgeType.Dependency, 1.0);
        graph.AddEdge("B", "A", EdgeType.Dependency, 1.0);
        
        // Cluster 2: X-Y (connected)
        graph.AddNode("X", new NodeMetadata { FilePath = "X.cs", EstimatedTokens = 100 });
        graph.AddNode("Y", new NodeMetadata { FilePath = "Y.cs", EstimatedTokens = 100 });
        graph.AddEdge("X", "Y", EdgeType.Dependency, 1.0);
        graph.AddEdge("Y", "X", EdgeType.Dependency, 1.0);
        
        // Communities: perfect separation (no edges between clusters)
        var communities = new Dictionary<string, int>
        {
            { "A", 0 }, { "B", 0 },
            { "X", 1 }, { "Y", 1 }
        };

        // Act
        var modularity = _service.CalculateModularity(graph, communities);

        // Assert - Should be positive for well-separated clusters
        Assert.That(modularity, Is.GreaterThan(0.0), "Perfect clustering should have positive modularity");
    }

    [Test]
    public void CalculateModularity_WithPoorClustering_ShouldReturnLowValue()
    {
        // Arrange - Create connected nodes but cluster them poorly
        var graph = new EnhancedDependencyGraph();
        graph.AddNode("A", new NodeMetadata { FilePath = "A.cs", EstimatedTokens = 100 });
        graph.AddNode("B", new NodeMetadata { FilePath = "B.cs", EstimatedTokens = 100 });
        graph.AddNode("C", new NodeMetadata { FilePath = "C.cs", EstimatedTokens = 100 });
        graph.AddNode("D", new NodeMetadata { FilePath = "D.cs", EstimatedTokens = 100 });
        
        // Create a chain: A -> B -> C -> D
        graph.AddEdge("A", "B", EdgeType.Dependency, 1.0);
        graph.AddEdge("B", "C", EdgeType.Dependency, 1.0);
        graph.AddEdge("C", "D", EdgeType.Dependency, 1.0);
        
        // Poor clustering: alternate nodes in different clusters
        var communities = new Dictionary<string, int>
        {
            { "A", 0 }, { "B", 1 }, { "C", 0 }, { "D", 1 }
        };

        // Act
        var modularity = _service.CalculateModularity(graph, communities);

        // Assert - Should be lower due to poor clustering
        Assert.That(modularity, Is.LessThan(0.3), "Poor clustering should have low modularity");
    }

    [Test]
    public void CalculateConductance_WithTightCluster_ShouldReturnLowValue()
    {
        // Arrange - Create a cluster with internal edges and minimal external ones
        var graph = new EnhancedDependencyGraph();
        // Cluster nodes
        graph.AddNode("A", new NodeMetadata { FilePath = "A.cs", EstimatedTokens = 100 });
        graph.AddNode("B", new NodeMetadata { FilePath = "B.cs", EstimatedTokens = 100 });
        graph.AddNode("C", new NodeMetadata { FilePath = "C.cs", EstimatedTokens = 100 });
        // External node
        graph.AddNode("X", new NodeMetadata { FilePath = "X.cs", EstimatedTokens = 100 });
        
        // Internal edges (tight cluster)
        graph.AddEdge("A", "B", EdgeType.Dependency, 1.0);
        graph.AddEdge("B", "C", EdgeType.Dependency, 1.0);
        graph.AddEdge("C", "A", EdgeType.Dependency, 1.0);
        // One external edge
        graph.AddEdge("A", "X", EdgeType.Dependency, 1.0);

        var cluster = new HashSet<string> { "A", "B", "C" };

        // Act
        var conductance = _service.CalculateConductance(graph, cluster);

        // Assert - Low conductance means good cluster separation
        Assert.That(conductance, Is.LessThan(0.5), "Tight cluster should have low conductance");
    }

    [Test]
    public void CalculateConductance_WithHighlyConnectedCluster_ShouldReturnHighValue()
    {
        // Arrange - Cluster with many external edges
        var graph = new EnhancedDependencyGraph();
        graph.AddNode("A", new NodeMetadata { FilePath = "A.cs", EstimatedTokens = 100 });
        graph.AddNode("B", new NodeMetadata { FilePath = "B.cs", EstimatedTokens = 100 });
        graph.AddNode("X", new NodeMetadata { FilePath = "X.cs", EstimatedTokens = 100 });
        graph.AddNode("Y", new NodeMetadata { FilePath = "Y.cs", EstimatedTokens = 100 });
        
        // Many edges from cluster {A, B} to outside {X, Y}
        graph.AddEdge("A", "X", EdgeType.Dependency, 1.0);
        graph.AddEdge("A", "Y", EdgeType.Dependency, 1.0);
        graph.AddEdge("B", "X", EdgeType.Dependency, 1.0);
        graph.AddEdge("B", "Y", EdgeType.Dependency, 1.0);

        var cluster = new HashSet<string> { "A", "B" };

        // Act
        var conductance = _service.CalculateConductance(graph, cluster);

        // Assert - Should be high (poor cluster separation)
        Assert.That(conductance, Is.GreaterThan(0.5), "Highly connected cluster should have high conductance");
    }

    [Test]
    public void CalculateInternalDensity_WithFullyConnected_ShouldReturnOne()
    {
        // Arrange - Create a fully connected cluster
        var graph = new EnhancedDependencyGraph();
        graph.AddNode("A", new NodeMetadata { FilePath = "A.cs", EstimatedTokens = 100 });
        graph.AddNode("B", new NodeMetadata { FilePath = "B.cs", EstimatedTokens = 100 });
        graph.AddNode("C", new NodeMetadata { FilePath = "C.cs", EstimatedTokens = 100 });
        
        // Fully connected: every node connects to every other
        graph.AddEdge("A", "B", EdgeType.Dependency, 1.0);
        graph.AddEdge("A", "C", EdgeType.Dependency, 1.0);
        graph.AddEdge("B", "A", EdgeType.Dependency, 1.0);
        graph.AddEdge("B", "C", EdgeType.Dependency, 1.0);
        graph.AddEdge("C", "A", EdgeType.Dependency, 1.0);
        graph.AddEdge("C", "B", EdgeType.Dependency, 1.0);

        var cluster = new HashSet<string> { "A", "B", "C" };

        // Act
        var density = _service.CalculateInternalDensity(graph, cluster);

        // Assert - 6 edges / 6 possible = 1.0
        Assert.That(density, Is.EqualTo(1.0), "Fully connected cluster should have density of 1.0");
    }

    [Test]
    public void CalculateInternalDensity_WithNoInternalEdges_ShouldReturnZero()
    {
        // Arrange - Nodes in same cluster but not connected
        var graph = new EnhancedDependencyGraph();
        graph.AddNode("A", new NodeMetadata { FilePath = "A.cs", EstimatedTokens = 100 });
        graph.AddNode("B", new NodeMetadata { FilePath = "B.cs", EstimatedTokens = 100 });
        graph.AddNode("C", new NodeMetadata { FilePath = "C.cs", EstimatedTokens = 100 });
        // No edges between them

        var cluster = new HashSet<string> { "A", "B", "C" };

        // Act
        var density = _service.CalculateInternalDensity(graph, cluster);

        // Assert
        Assert.That(density, Is.EqualTo(0.0), "Disconnected cluster should have zero density");
    }

    [Test]
    public void EvaluateClustering_ShouldReturnCompositeMetrics()
    {
        // Arrange - Good clustering scenario
        var graph = new EnhancedDependencyGraph();
        graph.AddNode("A", new NodeMetadata { FilePath = "A.cs", EstimatedTokens = 100 });
        graph.AddNode("B", new NodeMetadata { FilePath = "B.cs", EstimatedTokens = 100 });
        graph.AddNode("C", new NodeMetadata { FilePath = "C.cs", EstimatedTokens = 100 });
        graph.AddNode("D", new NodeMetadata { FilePath = "D.cs", EstimatedTokens = 100 });
        
        // Cluster 1: A-B
        graph.AddEdge("A", "B", EdgeType.Dependency, 1.0);
        graph.AddEdge("B", "A", EdgeType.Dependency, 1.0);
        
        // Cluster 2: C-D
        graph.AddEdge("C", "D", EdgeType.Dependency, 1.0);
        graph.AddEdge("D", "C", EdgeType.Dependency, 1.0);

        var communities = new Dictionary<string, int>
        {
            { "A", 0 }, { "B", 0 }, { "C", 1 }, { "D", 1 }
        };

        // Act
        var metrics = _service.EvaluateClustering(graph, communities);

        // Assert
        Assert.That(metrics, Is.Not.Null);
        Assert.That(metrics.ClusterCount, Is.EqualTo(2));
        Assert.That(metrics.AverageConductance, Is.GreaterThanOrEqualTo(0.0).And.LessThanOrEqualTo(1.0));
        Assert.That(metrics.AverageInternalDensity, Is.GreaterThanOrEqualTo(0.0).And.LessThanOrEqualTo(1.0));
        Assert.That(metrics.CompositeScore, Is.GreaterThanOrEqualTo(0.0).And.LessThanOrEqualTo(1.0));
    }
}
