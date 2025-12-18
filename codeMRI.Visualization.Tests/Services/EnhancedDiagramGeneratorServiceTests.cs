using codeMRI.Core.Models;
using codeMRI.Core.Models;
using codeMRI.Core.Interfaces;
using codeMRI.Visualization.Services;
using Moq;

namespace codeMRI.Visualization.Tests.Services;

[TestFixture]
public class EnhancedDiagramGeneratorServiceTests
{
    [SetUp]
    public void Setup()
    {
        var mockLlm = new Mock<ILLMClient>();
        _generator = new DiagramGeneratorService(new HttpClient(), mockLlm.Object);
        _testGraph = CreateTestGraph();
    }

    private DiagramGeneratorService _generator;
    private EnhancedDependencyGraph _testGraph;

    private EnhancedDependencyGraph CreateTestGraph()
    {
        var graph = new EnhancedDependencyGraph();

        // Add nodes with different types and layers
        graph.AddNode("Controller", new NodeMetadata
        {
            Type = "Controller",
            Layer = "Presentation",
            Complexity = 0.3,
            Visibility = "public"
        });

        graph.AddNode("Service", new NodeMetadata
        {
            Type = "Service",
            Layer = "Business",
            Complexity = 0.7,
            Visibility = "public"
        });

        graph.AddNode("Repository", new NodeMetadata
        {
            Type = "Repository",
            Layer = "Data",
            Complexity = 0.5,
            Visibility = "internal"
        });

        graph.AddNode("Helper", new NodeMetadata
        {
            Type = "Helper",
            Layer = "Utility",
            Complexity = 0.2,
            Visibility = "private"
        });

        // Add edges
        graph.AddEdge("Controller", "Service", EdgeType.MethodCall, 1.0);
        graph.AddEdge("Service", "Repository", EdgeType.MethodCall, 1.0);
        graph.AddEdge("Service", "Helper", EdgeType.Dependency, 0.5);

        return graph;
    }

    [Test]
    public async Task GenerateInteractiveComponentDiagramAsync_ShouldReturnValidInteractiveDiagram()
    {
        // Act
        var result = await _generator.GenerateInteractiveComponentDiagramAsync(_testGraph, "Controller");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Type, Is.EqualTo(DiagramType.Component));
        Assert.That(result.MermaidContent, Is.Not.Empty);
        Assert.That(result.Components, Is.Not.Empty);
        Assert.That(result.Relationships, Is.Not.Empty);
    }

    [Test]
    public async Task GenerateInteractiveSequenceDiagramAsync_ShouldReturnValidInteractiveDiagram()
    {
        // Act
        var result = await _generator.GenerateInteractiveSequenceDiagramAsync(_testGraph, "Controller");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Type, Is.EqualTo(DiagramType.Sequence));
        Assert.That(result.MermaidContent, Is.Not.Empty);
        Assert.That(result.Components, Is.Not.Empty);
        Assert.That(result.Relationships, Is.Not.Empty);
    }

    [Test]
    public void FilterComponents_ShouldApplyComplexityFilter()
    {
        // Arrange
        var components = new List<DiagramComponent>
        {
            new() { Id = "Simple", Complexity = (int)(0.2 * 100) },
            new() { Id = "Complex", Complexity = (int)(0.8 * 100) },
            new() { Id = "Medium", Complexity = (int)(0.5 * 100) }
        };
        var filter = new FilterOptions { MinComplexity = (int)(0.4 * 100), MaxComplexity = (int)(0.6 * 100) };

        // Act
        var result = _generator.FilterComponents(components, filter);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo("Medium"));
    }

    [Test]
    public void FilterComponents_ShouldApplyVisibilityFilter()
    {
        // Arrange
        var components = new List<DiagramComponent>
        {
            new() { Id = "Public", IsPublic = true },
            new() { Id = "Private", IsPublic = false },
            new() { Id = "Internal", IsPublic = false }
        };
        var filter = new FilterOptions { ShowOnlyPublic = true };

        // Act
        var result = _generator.FilterComponents(components, filter);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo("Public"));
    }

    [Test]
    public void FilterComponents_ShouldApplyLayerFilter()
    {
        // Arrange
        var components = new List<DiagramComponent>
        {
            new() { Id = "Controller", Layer = "Presentation" },
            new() { Id = "Service", Layer = "Business" },
            new() { Id = "Repository", Layer = "Data" }
        };
        var filter = new FilterOptions { IncludedLayers = new List<string> { "Presentation", "Business" } };

        // Act
        var result = _generator.FilterComponents(components, filter);

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Select(c => c.Id), Does.Contain("Controller"));
        Assert.That(result.Select(c => c.Id), Does.Contain("Service"));
    }

    [Test]
    public void FilterRelationships_ShouldApplyTypeFilter()
    {
        // Arrange
        var relationships = new List<DiagramRelationship>
        {
            new() { Type = EdgeType.MethodCall },
            new() { Type = EdgeType.Dependency },
            new() { Type = EdgeType.Inheritance }
        };
        var filter = new FilterOptions
            { IncludedRelationshipTypes = new List<EdgeType> { EdgeType.MethodCall, EdgeType.Dependency } };

        // Act
        var result = _generator.FilterRelationships(relationships, filter);

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Select(r => r.Type), Does.Contain(EdgeType.MethodCall));
        Assert.That(result.Select(r => r.Type), Does.Contain(EdgeType.Dependency));
    }

    [Test]
    public async Task GenerateInteractiveComponentDiagramAsync_ShouldHandleEmptyGraph()
    {
        // Arrange
        var emptyGraph = new EnhancedDependencyGraph();

        // Act
        var result = await _generator.GenerateInteractiveComponentDiagramAsync(emptyGraph, "NonExistent");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.MermaidContent, Is.Not.Empty);
        Assert.That(result.Components, Is.Empty);
        Assert.That(result.Relationships, Is.Empty);
    }
}