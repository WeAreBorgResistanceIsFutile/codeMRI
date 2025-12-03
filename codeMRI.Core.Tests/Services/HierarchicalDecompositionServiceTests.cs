using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
// For ArchitecturalPattern and ArchitecturalLayerType
// For ModuleTree, ModuleNode, EnhancedDependencyGraph, etc.

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class HierarchicalDecompositionServiceTests
{
    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<HierarchicalDecompositionService>>();
        _graphServiceMock = new Mock<IEnhancedDependencyGraphService>();
        _patternServiceMock = new Mock<IArchitecturalPatternService>();

        _service = new HierarchicalDecompositionService(
            _loggerMock.Object,
            _graphServiceMock.Object,
            _patternServiceMock.Object);
    }

    private Mock<ILogger<HierarchicalDecompositionService>> _loggerMock;
    private Mock<IEnhancedDependencyGraphService> _graphServiceMock;
    private Mock<IArchitecturalPatternService> _patternServiceMock;
    private HierarchicalDecompositionService _service;

    [Test]
    public async Task DecomposeHierarchicallyAsync_ShouldCreateModules_BasedOnArchitectureAndLouvain()
    {
        // Arrange
        var graph = new EnhancedDependencyGraph();
        graph.AddNode("Controller1", new NodeMetadata { Type = "Controller", EstimatedTokens = 100 });
        graph.AddNode("Service1", new NodeMetadata { Type = "Service", EstimatedTokens = 100 });
        graph.AddNode("Helper1", new NodeMetadata { Type = "Helper", EstimatedTokens = 50 });

        // Helper1 depends on nothing, Controller1 -> Service1
        graph.AddEdge("Controller1", "Service1", EdgeType.Dependency, 1.0);

        _graphServiceMock.Setup(x => x.GetComponentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CodeComponent>());
        _graphServiceMock.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);
        _graphServiceMock.Setup(x =>
                x.AnalyzeGraphAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphAnalysisResult());

        _patternServiceMock.Setup(x => x.DetermineLayer(It.Is<GraphNode>(n => n.ComponentId == "Controller1")))
            .Returns(ArchitecturalLayerType.Presentation);
        _patternServiceMock.Setup(x => x.DetermineLayer(It.Is<GraphNode>(n => n.ComponentId == "Service1")))
            .Returns(ArchitecturalLayerType.Application);
        _patternServiceMock.Setup(x => x.DetermineLayer(It.Is<GraphNode>(n => n.ComponentId == "Helper1")))
            .Returns(ArchitecturalLayerType.Unknown); // Will trigger Louvain for Helper1

        _patternServiceMock.Setup(x => x.RecognizePattern(It.IsAny<ModuleNode>(), It.IsAny<EnhancedDependencyGraph>()))
            .Returns(new ArchitecturalPattern { Type = ArchitecturalPatternType.Unknown });

        // Act
        var result = await _service.DecomposeHierarchicallyAsync("repo/path");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Nodes, Is.Not.Empty);

        // We expect modules for Presentation, Application, and a cluster for Helper1
        var hasPresentation = false;
        var hasApplication = false;
        var hasCluster = false;

        foreach (var node in result.Nodes.Values)
        {
            if (node.Name == "Presentation") hasPresentation = true;
            if (node.Name == "Application") hasApplication = true;
            if (node.Name.StartsWith("Component_")) hasCluster = true;
        }

        Assert.That(hasPresentation, Is.True, "Should have Presentation module");
        Assert.That(hasApplication, Is.True, "Should have Application module");
        Assert.That(hasCluster, Is.True, "Should have clustered module for unknown layer");
    }

    [Test]
    public async Task DecomposeHierarchicallyAsync_ShouldCalculateQualityMetrics()
    {
        // Arrange
        var graph = new EnhancedDependencyGraph();
        graph.AddNode("CompA", new NodeMetadata { EstimatedTokens = 100 });
        graph.AddNode("CompB", new NodeMetadata { EstimatedTokens = 100 });
        graph.AddEdge("CompA", "CompB", EdgeType.Dependency, 1.0);

        _graphServiceMock.Setup(x => x.GetComponentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CodeComponent>());
        _graphServiceMock.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);
        _graphServiceMock.Setup(x =>
                x.AnalyzeGraphAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphAnalysisResult());

        _patternServiceMock.Setup(x => x.DetermineLayer(It.IsAny<GraphNode>()))
            .Returns(ArchitecturalLayerType.Unknown); // Force Louvain

        _patternServiceMock.Setup(x => x.RecognizePattern(It.IsAny<ModuleNode>(), It.IsAny<EnhancedDependencyGraph>()))
            .Returns(new ArchitecturalPattern { Type = ArchitecturalPatternType.Unknown });

        // Act
        var result = await _service.DecomposeHierarchicallyAsync("repo/path");

        // Assert
        foreach (var node in result.Nodes.Values)
        {
            if (node.Id == "root") continue;
            Assert.That(node.QualityMetrics, Is.Not.Null);
            // Just check if metrics were touched (default is 0, but we initialize them)
            // Cohesion/Coupling might be 0 or 1 depending on clustering
        }
    }

    [Test]
    public async Task DecomposeHierarchicallyAsync_ShouldCalculateAdvancedQualityMetrics()
    {
        // Arrange
        var graph = new EnhancedDependencyGraph();

        // Module A: Abstract and Stable
        // Interface1 (Abstract) <- Impl1 (Concrete) [Internal]
        // Interface1 used by external
        graph.AddNode("Interface1", new NodeMetadata { Type = "Interface", EstimatedTokens = 10 });
        graph.AddNode("Impl1", new NodeMetadata { Type = "Class", EstimatedTokens = 50 });
        graph.AddEdge("Impl1", "Interface1", EdgeType.Implementation, 1.0);

        // External usage
        graph.AddNode("ExternalClient", new NodeMetadata { Type = "Class", EstimatedTokens = 50 });
        graph.AddEdge("ExternalClient", "Interface1", EdgeType.Dependency, 1.0);

        _graphServiceMock.Setup(x => x.GetComponentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CodeComponent>());
        _graphServiceMock.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);
        _graphServiceMock.Setup(x =>
                x.AnalyzeGraphAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphAnalysisResult());

        // Mock clustering to group Interface1 and Impl1 together
        _patternServiceMock.Setup(x => x.DetermineLayer(It.IsAny<GraphNode>()))
            .Returns(ArchitecturalLayerType.Unknown);

        _patternServiceMock.Setup(x => x.RecognizePattern(It.IsAny<ModuleNode>(), It.IsAny<EnhancedDependencyGraph>()))
            .Returns(new ArchitecturalPattern { Type = ArchitecturalPatternType.Unknown });

        // Mock DetermineLayer to ensure grouping
        _patternServiceMock.Setup(x => x.DetermineLayer(It.Is<GraphNode>(n => n.ComponentId == "Interface1")))
            .Returns(ArchitecturalLayerType.Domain);
        _patternServiceMock.Setup(x => x.DetermineLayer(It.Is<GraphNode>(n => n.ComponentId == "Impl1")))
            .Returns(ArchitecturalLayerType.Domain);
        _patternServiceMock.Setup(x => x.DetermineLayer(It.Is<GraphNode>(n => n.ComponentId == "ExternalClient")))
            .Returns(ArchitecturalLayerType.Presentation);

        // Act
        var result = await _service.DecomposeHierarchicallyAsync("repo/path");

        // Assert
        var domainModule = result.Nodes.Values.FirstOrDefault(n => n.Name == "Domain");
        Assert.That(domainModule, Is.Not.Null);

        // Instability = Ce / (Ce + Ca)
        // Interface1: In from ExternalClient (Ca=1), In from Impl1 (Internal)
        // Impl1: Out to Interface1 (Internal)
        // Module Domain: Ca=1 (from ExternalClient to Interface1), Ce=0
        // Instability = 0 / (0 + 1) = 0.0
        Assert.That(domainModule.QualityMetrics.Instability, Is.EqualTo(0.0).Within(0.1));

        // Abstractness = Na / Nc = 1 / 2 = 0.5
        Assert.That(domainModule.QualityMetrics.Abstractness, Is.EqualTo(0.5).Within(0.1));

        // D = |A + I - 1| = |0.5 + 0 - 1| = 0.5
        Assert.That(domainModule.QualityMetrics.DistanceFromMainSequence, Is.EqualTo(0.5).Within(0.1));
    }

    [Test]
    public async Task DecomposeHierarchicallyAsync_ShouldGroupStronglyConnectedComponents()
    {
        // Arrange
        var graph = new EnhancedDependencyGraph();
        graph.AddNode("A", new NodeMetadata { Type = "Class", EstimatedTokens = 10 });
        graph.AddNode("B", new NodeMetadata { Type = "Class", EstimatedTokens = 10 });
        graph.AddNode("C", new NodeMetadata { Type = "Class", EstimatedTokens = 10 });

        // Cycle: A -> B -> C -> A
        graph.AddEdge("A", "B", EdgeType.Dependency, 1.0);
        graph.AddEdge("B", "C", EdgeType.Dependency, 1.0);
        graph.AddEdge("C", "A", EdgeType.Dependency, 1.0);

        _graphServiceMock.Setup(x => x.GetComponentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CodeComponent>());
        _graphServiceMock.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);

        // Mock Analysis Result
        var analysisResult = new GraphAnalysisResult
        {
            StronglyConnectedComponents = new List<List<string>>
            {
                new() { "A", "B", "C" }
            }
        };
        _graphServiceMock.Setup(x => x.AnalyzeGraphAsync(graph, It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysisResult);

        _patternServiceMock.Setup(x => x.DetermineLayer(It.IsAny<GraphNode>()))
            .Returns(ArchitecturalLayerType.Unknown);
        _patternServiceMock.Setup(x => x.RecognizePattern(It.IsAny<ModuleNode>(), It.IsAny<EnhancedDependencyGraph>()))
            .Returns(new ArchitecturalPattern { Type = ArchitecturalPatternType.Unknown });

        // Act
        var result = await _service.DecomposeHierarchicallyAsync("repo");

        // Assert
        // Check that A, B, C are in the same module
        var leaves = result.GetAllLeaves();
        var moduleWithA = leaves.FirstOrDefault(m => m.Components.Contains("A"));
        Assert.That(moduleWithA, Is.Not.Null);
        Assert.That(moduleWithA.Components, Does.Contain("B"));
        Assert.That(moduleWithA.Components, Does.Contain("C"));

        // Also verify AnalyzeGraphAsync was called
        _graphServiceMock.Verify(x => x.AnalyzeGraphAsync(graph, It.IsAny<CancellationToken>()), Times.Once);
    }
}