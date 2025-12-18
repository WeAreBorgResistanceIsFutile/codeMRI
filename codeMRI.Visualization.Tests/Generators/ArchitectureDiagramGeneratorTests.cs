using codeMRI.Core.Models;
using codeMRI.Visualization.Services;
using Moq;
using codeMRI.Core.Interfaces;

namespace codeMRI.Visualization.Tests.Generators;

[TestFixture]
public class ArchitectureDiagramGeneratorTests
{
    [SetUp]
    public void Setup()
    {
        var mockLlm = new Mock<ILLMClient>();
        _generator = new DiagramGeneratorService(new HttpClient(), mockLlm.Object);
        _testGraph = new EnhancedDependencyGraph();
        _testTree = new ModuleTree();
    }

    private DiagramGeneratorService _generator;
    private EnhancedDependencyGraph _testGraph;
    private ModuleTree _testTree;

    [Test]
    public async Task GenerateArchitectureDiagramAsync_ShouldReturnMermaidGraph_ForSimpleTree()
    {
        // Arrange
        var moduleTree = new ModuleTree();
        var root = new ModuleNode { Id = "root", Name = "Root", Components = new HashSet<string>() };

        var moduleA = new ModuleNode
            { Id = "moduleA", Name = "ModuleA", Parent = root, Components = new HashSet<string> { "Comp1", "Comp2" } };
        root.Children.Add(moduleA);

        var moduleB = new ModuleNode
            { Id = "moduleB", Name = "ModuleB", Parent = root, Components = new HashSet<string> { "Comp3" } };
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
        Assert.That(result, Does.Contain("graph TB"));
        Assert.That(result, Does.Contain("root[Root]"));
        Assert.That(result, Does.Contain("moduleA[ModuleA]"));
        Assert.That(result, Does.Contain("moduleB[ModuleB]"));
        Assert.That(result, Does.Contain("Comp1 --> Comp3"));
    }
}