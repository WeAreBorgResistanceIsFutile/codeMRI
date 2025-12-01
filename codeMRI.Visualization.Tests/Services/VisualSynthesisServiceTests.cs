using codeMRI.Shared.Models;
using codeMRI.Visualization.Interfaces;
using codeMRI.Visualization.Services;
using codeMRI.Visualization.Models;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace codeMRI.Visualization.Tests.Services
{
    [TestFixture]
    public class VisualSynthesisServiceTests
    {
        private Mock<IDiagramGenerator> _generatorMock;
        private VisualSynthesisService _service;

        [SetUp]
        public void Setup()
        {
            _generatorMock = new Mock<IDiagramGenerator>();
            _service = new VisualSynthesisService(_generatorMock.Object);
        }

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
}