using codeMRI.Core.Models;
using codeMRI.Visualization.Services;
using Moq;
using codeMRI.Core.Interfaces;

namespace codeMRI.Visualization.Tests.Generators;

[TestFixture]
public class SequenceDiagramGeneratorTests
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
    public async Task GenerateSequenceDiagramAsync_ShouldReturnSequence_ForEntryPoint()
    {
        // Arrange
        var graph = new EnhancedDependencyGraph();
        graph.AddNode("Controller", new NodeMetadata { Type = "Controller" });
        graph.AddNode("Service", new NodeMetadata { Type = "Service" });
        graph.AddNode("Repository", new NodeMetadata { Type = "Repository" });

        graph.AddEdge("Controller", "Service", EdgeType.MethodCall, 1.0);
        graph.AddEdge("Service", "Repository", EdgeType.MethodCall, 1.0);

        // Act
        var result = await _generator.GenerateSequenceDiagramAsync(graph, "Controller");

        // Assert
        Assert.That(result, Does.Contain("sequenceDiagram"));
        Assert.That(result, Does.Contain("Controller ->> Service: MethodCall"));
        Assert.That(result, Does.Contain("Service ->> Repository: MethodCall"));
    }
}