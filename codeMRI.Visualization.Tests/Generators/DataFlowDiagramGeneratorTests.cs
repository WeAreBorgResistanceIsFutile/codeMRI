using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Visualization.Services;
using NUnit.Framework;

namespace codeMRI.Visualization.Tests.Generators
{
    [TestFixture]
    public class DataFlowDiagramGeneratorTests
    {
        private DiagramGeneratorService _generator;

        [SetUp]
        public void Setup()
        {
            _generator = new DiagramGeneratorService();
        }

        [Test]
        public async Task GenerateDataFlowDiagramAsync_ShouldReturnGraphLR_ForComponent()
        {
            // Arrange
            var graph = new EnhancedDependencyGraph();
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
}