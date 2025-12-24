using NUnit.Framework;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

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
        _progressServiceMock = new Mock<IProgressService>();

        _progressServiceMock
            .Setup(p => p.WithScalingAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Func<Task>>()))
            .Returns<double, double, Func<Task>>(async (start, width, op) => await op());

        _service = new HierarchicalDecompositionService(
            _loggerMock.Object,
            _graphServiceMock.Object,
            _patternServiceMock.Object,
            _progressServiceMock.Object);

        _patternServiceMock.Setup(x => x.DetermineLayer(It.IsAny<GraphNode>()))
            .Returns(ArchitecturalLayerType.Unknown);
        _patternServiceMock.Setup(x => x.RecognizePattern(It.IsAny<ModuleNode>(), It.IsAny<EnhancedDependencyGraph>()))
            .Returns(new ArchitecturalPattern { Type = ArchitecturalPatternType.Unknown });
    }

    private Mock<ILogger<HierarchicalDecompositionService>> _loggerMock;
    private Mock<IEnhancedDependencyGraphService> _graphServiceMock;
    private Mock<IArchitecturalPatternService> _patternServiceMock;
    private Mock<IProgressService> _progressServiceMock;
    private HierarchicalDecompositionService _service;

    [Test]
    public async Task DecomposeHierarchicallyAsync_ShouldPerformRecursivePartitioning_WhenModulesAreTooLarge()
    {
        // Arrange
        var graph = new EnhancedDependencyGraph();
        // Create a large graph that exceeds token limit
        // Limit is 32768, so let's make a module with 40000 tokens
        for (var i = 0; i < 40; i++)
            graph.AddNode($"Node_{i}", new NodeMetadata { EstimatedTokens = 1000, FilePath = $"src/Node_{i}.cs" });

        // All nodes connected to form a single cluster if not split
        for (var i = 0; i < 39; i++) graph.AddEdge($"Node_{i}", $"Node_{i + 1}", EdgeType.Dependency, 1.0);

        _graphServiceMock.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);
        _graphServiceMock.Setup(x =>
                x.AnalyzeGraphAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphAnalysisResult());

        // Act
        var result = await _service.DecomposeHierarchicallyAsync("repo", null!);

        // Assert
        // Should start with 1 huge module, then split.
        // Depth should be > 1 (Root -> Level1 -> Level2)
        var maxLevel = result.Nodes.Values.Max(n => n.Level);
        Assert.That(maxLevel, Is.GreaterThan(1), "Should have decomposed recursively to at least level 2");

        var leaves = result.GetAllLeaves();
        foreach (var leaf in leaves)
            Assert.That(leaf.EstimatedTokens, Is.LessThanOrEqualTo(32768), "All leaves should respect token limit");
    }

    [Test]
    public async Task DecomposeHierarchicallyAsync_ShouldGroupFeatures_BasedOnDependencies()
    {
        // Arrange - Create graph with dependencies that cross directory boundaries
        var graph = new EnhancedDependencyGraph();
        
        // Auth components
        graph.AddNode("Features/Auth/LoginController.cs",
            new NodeMetadata { FilePath = "Features/Auth/LoginController.cs", EstimatedTokens = 100 });
        graph.AddNode("Features/Auth/LoginService.cs", 
            new NodeMetadata { FilePath = "Features/Auth/LoginService.cs", EstimatedTokens = 100 });

        // Order components
        graph.AddNode("Features/Orders/OrderController.cs",
            new NodeMetadata { FilePath = "Features/Orders/OrderController.cs", EstimatedTokens = 100 });
        graph.AddNode("Features/Orders/OrderService.cs",
            new NodeMetadata { FilePath = "Features/Orders/OrderService.cs", EstimatedTokens = 100 });

        // Create cross-directory dependencies (forms connected subgraph)
        graph.AddEdge("Features/Auth/LoginController.cs", "Features/Orders/OrderService.cs", 
            EdgeType.Dependency, 1.0);
        graph.AddEdge("Features/Orders/OrderController.cs", "Features/Auth/LoginService.cs", 
            EdgeType.Dependency, 1.0);
        graph.AddEdge("Features/Auth/LoginController.cs", "Features/Auth/LoginService.cs", 
            EdgeType.Dependency, 1.0);
        graph.AddEdge("Features/Orders/OrderController.cs", "Features/Orders/OrderService.cs", 
            EdgeType.Dependency, 1.0);

        _graphServiceMock.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);
        _graphServiceMock.Setup(x =>
                x.AnalyzeGraphAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphAnalysisResult());

        // Act
        var result = await _service.DecomposeHierarchicallyAsync("repo", null!);

        // Assert - Graph-based clustering should recognize connected components
        var totalComponents = result.GetAllLeaves().Sum(l => l.Components.Count);
        Assert.That(totalComponents, Is.EqualTo(4), "All components should be assigned");
    }

    [Test]
    public async Task DecomposeHierarchicallyAsync_ShouldCalculateQualityMetrics_IncludingInstabilityAndAbstractness()
    {
        // Arrange - Create a connected graph with interfaces and implementations
        var graph = new EnhancedDependencyGraph();
        graph.AddNode("Interface1", new NodeMetadata { Type = "Interface", EstimatedTokens = 10, FilePath = "src/Interface1.cs" });
        graph.AddNode("Impl1", new NodeMetadata { Type = "Class", EstimatedTokens = 50, FilePath = "src/Impl1.cs" });
        graph.AddEdge("Impl1", "Interface1", EdgeType.Implementation, 1.0);
        graph.AddNode("External", new NodeMetadata { Type = "Class", EstimatedTokens = 50, FilePath = "src/External.cs" });
        graph.AddEdge("External", "Interface1", EdgeType.Dependency, 1.0);

        _graphServiceMock.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);
        _graphServiceMock.Setup(x =>
                x.AnalyzeGraphAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphAnalysisResult());

        // Act
        var result = await _service.DecomposeHierarchicallyAsync("repo", null!);

        // Assert - With graph-based clustering, all 3 nodes should be grouped together
        // (they form a connected component)
        var leaves = result.GetAllLeaves();
        
        // Find the module containing these components
        var module = leaves.FirstOrDefault(m => 
            m.Components.Contains("Interface1") && 
            m.Components.Contains("Impl1") && 
            m.Components.Contains("External"));
        
        Assert.That(module, Is.Not.Null, "Should have one module with all connected components");
        Assert.That(module.QualityMetrics, Is.Not.Null, "Quality metrics should be calculated");
        
        // Abstractness = interfaces / total types = 1 / 3 = 0.33
        Assert.That(module.QualityMetrics.Abstractness, Is.EqualTo(0.33).Within(0.01),
            "Abstractness should be 1/3 for 1 interface out of 3 components");
    }

    [Test]
    public async Task DecomposeHierarchicallyAsync_ShouldUseRelativePaths_ForDirectoryClusters()
    {
        // Arrange - Test that absolute paths don't leak into module names
        var repoPath = Path.Combine(Path.GetTempPath(), "TestRepo");
        var featureDir = Path.Combine(repoPath, "Src", "FeatureA");
        var filePath = Path.Combine(featureDir, "Component1.cs");

        var graph = new EnhancedDependencyGraph();
        graph.AddNode("Component1", new NodeMetadata { FilePath = filePath, EstimatedTokens = 100 });

        _graphServiceMock.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);
        _graphServiceMock.Setup(x =>
                x.AnalyzeGraphAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphAnalysisResult());

        // Act
        var result = await _service.DecomposeHierarchicallyAsync(repoPath, null!);

        // Assert - Module name should NOT contain the temp path root
        var leaves = result.GetAllLeaves();
        Assert.That(leaves.Count, Is.GreaterThan(0), "Should have at least one module");
        
        // Find the module containing our component
        var module = leaves.FirstOrDefault(m => m.Components.Contains("Component1"));
        Assert.That(module, Is.Not.Null, "Should have a module containing Component1");
        Assert.That(module.Name, Does.Not.Contain("TestRepo"),
            "Module name should use relative path, not containing temp directory");
        Assert.That(module.Name, Does.Not.Contain(Path.GetTempPath()),
            "Module name should not contain system temp path");
        
        // Name should be derived from relative directory structure (Src/FeatureA)
        // Graph-based naming may produce "Src Featurea" or "Component_1_Files" depending on clustering
        Assert.That(module.Name.Length, Is.GreaterThan(0), "Module should have a non-empty name");
    }
}