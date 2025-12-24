using codeMRI.Core.Interfaces;
using codeMRI.Core.Services.MessageComposition;
using codeMRI.Core.Models;
using codeMRI.Visualization.Services;
using Moq;

namespace codeMRI.Visualization.Tests.Services;

[TestFixture]
public class VisualSynthesisServiceTests
{
    [SetUp]
    public void Setup()
    {
        _generatorMock = new Mock<IDiagramGenerator>();
        _service = new VisualSynthesisService(_generatorMock.Object);
    }

    private Mock<IDiagramGenerator> _generatorMock;
    private VisualSynthesisService _service;

    [Test]
    public async Task GenerateArtifactsAsync_ShouldCallGeneratorsAndReturnArtifacts()
    {
        // Arrange
        var moduleTree = new ModuleTree();
        var graph = new EnhancedDependencyGraph();

        _generatorMock.Setup(x => x.GenerateArchitectureDiagramAsync(moduleTree, graph))
            .ReturnsAsync("graph TD");
        _generatorMock.Setup(x => x.GenerateComponentDiagramAsync(graph, It.IsAny<string>()))
            .ReturnsAsync("classDiagram");

        // Act
        var result = await _service.GenerateArtifactsAsync(moduleTree, graph);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ArchitectureDiagram, Is.EqualTo("graph TD"));
        // We haven't defined logic for which components to generate diagrams for yet.
        // Assuming service generates main component diagram.
        // Also sequence diagrams?

        _generatorMock.Verify(x => x.GenerateArchitectureDiagramAsync(moduleTree, graph), Times.Once);
    }
}