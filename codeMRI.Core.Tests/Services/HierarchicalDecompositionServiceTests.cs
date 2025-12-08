using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class HierarchicalDecompositionServiceTests
{
    private Mock<ILogger<HierarchicalDecompositionService>> _loggerMock;
    private Mock<IEnhancedDependencyGraphService> _graphServiceMock;
    private Mock<IArchitecturalPatternService> _patternServiceMock;
    private HierarchicalDecompositionService _service;

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
            
        _patternServiceMock.Setup(x => x.DetermineLayer(It.IsAny<GraphNode>()))
            .Returns(ArchitecturalLayerType.Unknown);
        _patternServiceMock.Setup(x => x.RecognizePattern(It.IsAny<ModuleNode>(), It.IsAny<EnhancedDependencyGraph>()))
            .Returns(new ArchitecturalPattern { Type = ArchitecturalPatternType.Unknown });
    }

    [Test]
    public async Task DecomposeHierarchicallyAsync_ShouldPerformRecursivePartitioning_WhenModulesAreTooLarge()
    {
        // Arrange
        var graph = new EnhancedDependencyGraph();
        // Create a large graph that exceeds token limit
        // Limit is 32768, so let's make a module with 40000 tokens
        for (int i = 0; i < 40; i++)
        {
            graph.AddNode($"Node_{i}", new NodeMetadata { EstimatedTokens = 1000, FilePath = $"src/Node_{i}.cs" });
        }
        
        // All nodes connected to form a single cluster if not split
        for (int i = 0; i < 39; i++)
        {
            graph.AddEdge($"Node_{i}", $"Node_{i+1}", EdgeType.Dependency, 1.0);
        }

        _graphServiceMock.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<IProgress<ProgressInfo>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);
        _graphServiceMock.Setup(x => x.AnalyzeGraphAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphAnalysisResult());

        // Act
        var result = await _service.DecomposeHierarchicallyAsync("repo");

        // Assert
        // Should start with 1 huge module, then split.
        // Depth should be > 1 (Root -> Level1 -> Level2)
        var maxLevel = result.Nodes.Values.Max(n => n.Level);
        Assert.That(maxLevel, Is.GreaterThan(1), "Should have decomposed recursively to at least level 2");
        
        var leaves = result.GetAllLeaves();
        foreach(var leaf in leaves)
        {
            Assert.That(leaf.EstimatedTokens, Is.LessThanOrEqualTo(32768), "All leaves should respect token limit");
        }
    }

    [Test]
    public async Task DecomposeHierarchicallyAsync_ShouldGroupFeatures_BasedOnDirectoryStructure()
    {
        // Arrange
        var graph = new EnhancedDependencyGraph();
        // Feature A
        graph.AddNode("Features/Auth/LoginController.cs", new NodeMetadata { FilePath = "Features/Auth/LoginController.cs" });
        graph.AddNode("Features/Auth/LoginService.cs", new NodeMetadata { FilePath = "Features/Auth/LoginService.cs" });
        
        // Feature B
        graph.AddNode("Features/Orders/OrderController.cs", new NodeMetadata { FilePath = "Features/Orders/OrderController.cs" });
        graph.AddNode("Features/Orders/OrderService.cs", new NodeMetadata { FilePath = "Features/Orders/OrderService.cs" });

        _graphServiceMock.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<IProgress<ProgressInfo>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);
        _graphServiceMock.Setup(x => x.AnalyzeGraphAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphAnalysisResult());

        // Act
        var result = await _service.DecomposeHierarchicallyAsync("repo");

        // Assert
        // We expect modules named "Auth" and "Orders" or similar, even if they have no edges, 
        // because the directory structure implies cohesion (Feature-Oriented Decomposition).
        
        var authModule = result.Nodes.Values.FirstOrDefault(n => n.Name.Contains("Auth") || n.Components.Any(c => c.Contains("Auth")));
        var ordersModule = result.Nodes.Values.FirstOrDefault(n => n.Name.Contains("Orders") || n.Components.Any(c => c.Contains("Orders")));
        
        Assert.That(authModule, Is.Not.Null);
        Assert.That(ordersModule, Is.Not.Null);
        Assert.That(authModule != ordersModule, Is.True, "Features should be separated");
    }
    
    [Test]
    public async Task DecomposeHierarchicallyAsync_ShouldCalculateQualityMetrics_IncludingInstabilityAndAbstractness()
    {
         // Arrange
        var graph = new EnhancedDependencyGraph();
        graph.AddNode("Interface1", new NodeMetadata { Type = "Interface", EstimatedTokens = 10 });
        graph.AddNode("Impl1", new NodeMetadata { Type = "Class", EstimatedTokens = 50 });
        graph.AddEdge("Impl1", "Interface1", EdgeType.Implementation, 1.0);
        graph.AddNode("External", new NodeMetadata { Type = "Class", EstimatedTokens = 50 });
        graph.AddEdge("External", "Interface1", EdgeType.Dependency, 1.0);

        _graphServiceMock.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<IProgress<ProgressInfo>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);
        _graphServiceMock.Setup(x => x.AnalyzeGraphAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphAnalysisResult());
            
        // Mock strict grouping
        _patternServiceMock.Setup(x => x.DetermineLayer(It.Is<GraphNode>(n => n.ComponentId == "Interface1")))
            .Returns(ArchitecturalLayerType.Domain);
        _patternServiceMock.Setup(x => x.DetermineLayer(It.Is<GraphNode>(n => n.ComponentId == "Impl1")))
            .Returns(ArchitecturalLayerType.Domain);
         _patternServiceMock.Setup(x => x.DetermineLayer(It.Is<GraphNode>(n => n.ComponentId == "External")))
            .Returns(ArchitecturalLayerType.Presentation);

        // Act
        var result = await _service.DecomposeHierarchicallyAsync("repo");

        // Assert
        var domain = result.Nodes.Values.First(n => n.Name == "Domain");
        // Instability = Ce / (Ce + Ca) = 0 / 1 = 0
        Assert.That(domain.QualityMetrics.Instability, Is.EqualTo(0.0).Within(0.01));
        // Abstractness = 1 / 2 = 0.5
        Assert.That(domain.QualityMetrics.Abstractness, Is.EqualTo(0.5).Within(0.01));
    }
}
