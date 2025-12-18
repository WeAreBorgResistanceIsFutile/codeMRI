using codeMRI.Core.Models;
using codeMRI.Visualization.Services;
using Moq;
using codeMRI.Core.Interfaces;
using System.Net.Http; // Added for HttpClient

namespace codeMRI.Visualization.Tests.Generators;

[TestFixture]
public class DataFlowDiagramGeneratorTests
{
    private DiagramGeneratorService _generator;
    private EnhancedDependencyGraph _testGraph; // Added

    [SetUp]
    public void Setup()
    {
        var mockLlm = new Mock<ILLMClient>(); // Added
        _generator = new DiagramGeneratorService(new HttpClient(), mockLlm.Object); // Modified
        _testGraph = new EnhancedDependencyGraph(); // Added
    }

    [Test]
    public async Task GenerateDataFlowDiagramAsync_ShouldReturnGraphLR_ForComponent()
    {
        // Arrange
        var graph = _testGraph; // Modified to use _testGraph
        graph.AddNode("Processor", new NodeMetadata { Type = "Service" });
        graph.AddNode("InputData", new NodeMetadata { Type = "DTO" });
        graph.AddNode("OutputData", new NodeMetadata { Type = "DTO" });

        graph.AddEdge("InputData", "Processor", EdgeType.Dependency, 1.0);
        graph.AddEdge("Processor", "OutputData", EdgeType.Dependency, 1.0);

        // Act
        var result = await _generator.GenerateDataFlowDiagramAsync(graph, "Processor");

        // Assert
        Assert.That(result, Does.Contain("graph LR"));
        Assert.That(result, Does.Contain("InputData --> Processor"));
        Assert.That(result, Does.Contain("Processor --> OutputData"));
    }
}