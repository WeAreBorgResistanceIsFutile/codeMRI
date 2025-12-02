using codeMRI.Shared.Models;
using codeMRI.Visualization.Interfaces;
using codeMRI.Visualization.Services;

namespace codeMRI.Visualization.Tests.Services;

[TestFixture]
public class DiagramGeneratorServiceTests
{
    [SetUp]
    public void Setup()
    {
        _service = new DiagramGeneratorService(new HttpClient());
    }

    private IDiagramGenerator _service;

    [Test]
    public async Task GenerateArchitectureDiagramAsync_ShouldGenerateMermaidGraph()
    {
        // Arrange
        var tree = new ModuleTree();
        var root = new ModuleNode { Id = "root", Name = "Root" };
        var modA = new ModuleNode { Id = "modA", Name = "Module A", Parent = root, Level = 1 };
        modA.Components.Add("CompA1");
        modA.Components.Add("CompA2");
        root.Children.Add(modA);
        tree.Nodes.Add("root", root);
        tree.Nodes.Add("modA", modA);

        var graph = new EnhancedDependencyGraph();
        graph.AddNode("CompA1", new NodeMetadata());
        graph.AddNode("CompA2", new NodeMetadata());
        graph.AddEdge("CompA1", "CompA2", EdgeType.Dependency, 1.0);

        // Act
        var result = await _service.GenerateArchitectureDiagramAsync(tree, graph);

        // Assert
        Assert.That(result, Does.Contain("graph TB"));
        Assert.That(result, Does.Contain("root[Repository]"));
        Assert.That(result, Does.Contain("CompA1 --> CompA2"));
    }

    [Test]
    public async Task GenerateComponentDiagramAsync_ShouldGenerateClassDiagram()
    {
        // Arrange
        var graph = new EnhancedDependencyGraph();
        graph.AddNode("ClassA", new NodeMetadata { Type = "Class" });
        graph.AddNode("ClassB", new NodeMetadata { Type = "Class" });
        graph.AddEdge("ClassA", "ClassB", EdgeType.Dependency, 1.0);

        // Act
        var result = await _service.GenerateComponentDiagramAsync(graph, "ClassA");

        // Assert
        Assert.That(result, Does.Contain("graph LR"));
        Assert.That(result, Does.Contain("ClassA[ClassA]"));
        Assert.That(result, Does.Contain("ClassB[ClassB]"));
        Assert.That(result, Does.Contain("ClassA --> ClassB"));
    }

    [Test]
    public async Task GenerateSequenceDiagramAsync_ShouldTraceCalls()
    {
        // Arrange
        var graph = new EnhancedDependencyGraph();
        graph.AddNode("A", new NodeMetadata());
        graph.AddNode("B", new NodeMetadata());
        graph.AddNode("C", new NodeMetadata());
        graph.AddEdge("A", "B", EdgeType.Call, 1.0);
        graph.AddEdge("B", "C", EdgeType.Call, 1.0);

        // Act
        var result = await _service.GenerateSequenceDiagramAsync(graph, "A");

        // Assert
        Assert.That(result, Does.Contain("sequenceDiagram"));
        Assert.That(result, Does.Contain("A ->> B: Call"));
        Assert.That(result, Does.Contain("B ->> C: Call"));
    }
}