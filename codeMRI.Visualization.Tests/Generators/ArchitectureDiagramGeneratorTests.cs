using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Visualization.Interfaces;
using codeMRI.Visualization.Services;
using Moq;
using NUnit.Framework;

namespace codeMRI.Visualization.Tests.Generators
{
    [TestFixture]
    public class ArchitectureDiagramGeneratorTests
    {
        private DiagramGeneratorService _generator;

        [SetUp]
        public void Setup()
        {
            _generator = new DiagramGeneratorService();
        }

        [Test]
        public async Task GenerateArchitectureDiagramAsync_ShouldReturnMermaidGraph_ForSimpleTree()
        {
            // Arrange
            var moduleTree = new ModuleTree();
            var root = new ModuleNode { Id = "root", Name = "Root", Components = new HashSet<string>() };
            
            var moduleA = new ModuleNode { Id = "moduleA", Name = "ModuleA", Parent = root, Components = new HashSet<string> { "Comp1", "Comp2" } };
            root.Children.Add(moduleA);
            
            var moduleB = new ModuleNode { Id = "moduleB", Name = "ModuleB", Parent = root, Components = new HashSet<string> { "Comp3" } };
            root.Children.Add(moduleB);

            moduleTree.Root = root;

            var graph = new EnhancedDependencyGraph();
            graph.AddNode("Comp1", new NodeMetadata { Type = "Class" });
            graph.AddNode("Comp2", new NodeMetadata { Type = "Class" });
            graph.AddNode("Comp3", new NodeMetadata { Type = "Class" });
            
            graph.AddEdge("Comp1", "Comp3", EdgeType.Dependency, 1.0);

            // Act
            var result = await _generator.GenerateArchitectureDiagramAsync(moduleTree, graph);

            // Assert
            Assert.That(result, Does.Contain("graph TD"));
            Assert.That(result, Does.Contain("subgraph moduleA"));
            Assert.That(result, Does.Contain("Comp1"));
            Assert.That(result, Does.Contain("Comp3"));
            Assert.That(result, Does.Contain("Comp1 --> Comp3"));
        }
    }
}