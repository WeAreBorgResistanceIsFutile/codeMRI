using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Visualization.Services;
using Moq;

namespace codeMRI.Visualization.Tests.Generators;

[TestFixture]
public class ComponentDiagramGeneratorTests
{
    [SetUp]
    public void Setup()
    {
        var mockLlm = new Mock<ILLMClient>();
        _generator = new DiagramGeneratorService(new HttpClient(), mockLlm.Object);
        _testGraph = new EnhancedDependencyGraph();
    }

    private DiagramGeneratorService _generator;
    private EnhancedDependencyGraph _testGraph;

    [Test]
    public async Task GenerateComponentDiagramAsync_ShouldReturnClassDiagram_ForFocusedComponent()
    {
        // Arrange
        var graph = new EnhancedDependencyGraph();
        graph.AddNode("Comp1", new NodeMetadata { Type = "Class" });
        graph.AddNode("Comp2", new NodeMetadata { Type = "Interface" });

        graph.AddEdge("Comp1", "Comp2", EdgeType.Implementation, 1.0);

        // Act
        var result = await _generator.GenerateComponentDiagramAsync(graph, "Comp1");

        // Assert
        Assert.That(result, Does.Contain("graph LR"));
        Assert.That(result, Does.Contain("Comp1[Comp1]"));
        Assert.That(result, Does.Contain("Comp2[Comp2]"));
        Assert.That(result, Does.Contain("Comp1 --> Comp2"));
    }
}