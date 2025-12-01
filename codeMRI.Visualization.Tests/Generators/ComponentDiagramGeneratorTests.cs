using codeMRI.Shared.Models;
using codeMRI.Visualization.Interfaces;
using codeMRI.Visualization.Services;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace codeMRI.Visualization.Tests.Generators
{
    [TestFixture]
    public class ComponentDiagramGeneratorTests
    {
        private DiagramGeneratorService _generator;

        [SetUp]
        public void Setup()
        {
            _generator = new DiagramGeneratorService();
        }

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
            Assert.That(result, Does.Contain("classDiagram"));
            Assert.That(result, Does.Contain("class Comp1"));
            Assert.That(result, Does.Contain("class Comp2"));
            // Implementation often shown as ..|> or similar in mermaid, or just --> for now
            Assert.That(result, Does.Contain("Comp1 ..|> Comp2"));
        }
    }
}