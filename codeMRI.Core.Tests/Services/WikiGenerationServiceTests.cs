using codeMRI.Core.Interfaces;
using codeMRI.Core.Services;
using codeMRI.Shared.Models;
using codeMRI.Visualization.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class WikiGenerationServiceTests
{
    [SetUp]
    public void Setup()
    {
        _mockLlmClient = new Mock<ILLMClient>();
        _mockEmbedder = new Mock<IEmbedder>();
        _mockVectorDb = new Mock<IVectorDatabase>();
        _mockDiagramGenerator = new Mock<IDiagramGenerator>();
        _mockGraphService = new Mock<IEnhancedDependencyGraphService>();
        _mockLogger = new Mock<ILogger<WikiGenerationService>>();

        _service = new WikiGenerationService(
            _mockLlmClient!.Object,
            _mockEmbedder!.Object,
            _mockVectorDb!.Object,
            _mockDiagramGenerator!.Object,
            _mockGraphService!.Object);
    }

    private Mock<ILLMClient>? _mockLlmClient;
    private Mock<IEmbedder>? _mockEmbedder;
    private Mock<IVectorDatabase>? _mockVectorDb;
    private Mock<IDiagramGenerator>? _mockDiagramGenerator;
    private Mock<IEnhancedDependencyGraphService>? _mockGraphService;
    private Mock<ILogger<WikiGenerationService>>? _mockLogger;
    private WikiGenerationService? _service;

    [Test]
    public async Task GeneratePageAsync_ShouldIncludeDiagrams_WhenGraphHasNodes()
    {
        // Arrange
        var pageTitle = "TestController";
        var filePaths = new List<string> { "Controllers/TestController.cs" };
        var fileContents = new Dictionary<string, string>
        {
            { "Controllers/TestController.cs", "public class TestController { }" }
        };

        var mockEmbedding = new[] { 1.0f, 2.0f, 3.0f };
        var mockDocs = new List<Document>
        {
            new() { FilePath = "Controllers/TestController.cs", Content = "public class TestController { }" }
        };

        var graph = new EnhancedDependencyGraph();
        graph.AddNode("TestController", new NodeMetadata { Type = "Class" });
        graph.AddNode("TestService", new NodeMetadata { Type = "Class" });
        graph.AddEdge("TestController", "TestService", EdgeType.Call, 1.0);

        _mockEmbedder.Setup(x => x.EmbedAsync(It.IsAny<string>())).ReturnsAsync(mockEmbedding);
        _mockVectorDb.Setup(x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>())).ReturnsAsync(mockDocs);
        _mockLlmClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
            .ReturnsAsync("# TestController\n\nThis is a test controller.");
        _mockGraphService.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);
        _mockDiagramGenerator.Setup(x =>
                x.GenerateSequenceDiagramAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<string>()))
            .ReturnsAsync("```mermaid\nsequenceDiagram\n    TestController->>TestService: Call\n```");
        _mockDiagramGenerator.Setup(x =>
                x.GenerateComponentDiagramAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<string>()))
            .ReturnsAsync(
                "```mermaid\nclassDiagram\n    class TestController {\n        +Class\n    }\n    TestController --> TestService\n```");

        // Act
        var result = await _service.GeneratePageAsync(pageTitle, filePaths, fileContents);

        // Assert
        Assert.That(result.Content, Does.Contain("## Sequence Diagram"));
        Assert.That(result.Content, Does.Contain("sequenceDiagram"));
        Assert.That(result.Content, Does.Contain("## Component Diagram"));
        Assert.That(result.Content, Does.Contain("TestController->>TestService: Call"));

        _mockDiagramGenerator.Verify(x => x.GenerateSequenceDiagramAsync(graph, It.IsAny<string>()), Times.Once);
        _mockDiagramGenerator.Verify(x => x.GenerateComponentDiagramAsync(graph, It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task GeneratePageAsync_ShouldNotIncludeDiagrams_WhenGraphIsEmpty()
    {
        // Arrange
        var pageTitle = "TestController";
        var filePaths = new List<string> { "Controllers/TestController.cs" };
        var fileContents = new Dictionary<string, string>
        {
            { "Controllers/TestController.cs", "public class TestController { }" }
        };

        var emptyGraph = new EnhancedDependencyGraph();

        _mockLlmClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
            .ReturnsAsync("# TestController\n\nThis is a test controller.");
        _mockGraphService.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyGraph);

        // Act
        var result = await _service.GeneratePageAsync(pageTitle, filePaths, fileContents);

        // Assert
        Assert.That(result.Content, Does.Not.Contain("## Sequence Diagram"));
        Assert.That(result.Content, Does.Not.Contain("## Class Diagram"));

        _mockDiagramGenerator.Verify(
            x => x.GenerateSequenceDiagramAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<string>()), Times.Never);
        _mockDiagramGenerator.Verify(
            x => x.GenerateComponentDiagramAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task GenerateParentPageAsync_ShouldIncludeArchitectureDiagram_WhenGraphHasNodes()
    {
        // Arrange
        var module = new ModuleNode
        {
            Id = "test-module",
            Name = "Test Module",
            Level = 1,
            Description = "A test module"
        };
        var childPages = new List<WikiPage>
        {
            new() { Title = "Child Page 1", Content = "Content for child page 1" },
            new() { Title = "Child Page 2", Content = "Content for child page 2" }
        };

        var graph = new EnhancedDependencyGraph();
        graph.AddNode("Component1", new NodeMetadata { Type = "Class" });
        graph.AddNode("Component2", new NodeMetadata { Type = "Class" });
        graph.AddEdge("Component1", "Component2", EdgeType.Dependency, 1.0);

        _mockLlmClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
            .ReturnsAsync("# Test Module\n\nThis is a test module overview.");
        _mockGraphService.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);
        _mockDiagramGenerator.Setup(x =>
                x.GenerateArchitectureDiagramAsync(It.IsAny<ModuleTree>(), It.IsAny<EnhancedDependencyGraph>()))
            .ReturnsAsync(
                "```mermaid\ngraph TD\n    subgraph test-module[Test Module]\n        Component1[Component1]\n        Component2[Component2]\n    end\n    Component1 --> Component2\n```");

        // Act
        var result = await _service.GenerateParentPageAsync(module, childPages);

        // Assert
        Assert.That(result.Content, Does.Contain("## Architecture Diagram"));
        Assert.That(result.Content, Does.Contain("graph TD"));
        Assert.That(result.Content, Does.Contain("subgraph test-module[Test Module]"));

        _mockDiagramGenerator.Verify(x => x.GenerateArchitectureDiagramAsync(It.IsAny<ModuleTree>(), graph),
            Times.Once);
    }

    [Test]
    public async Task GeneratePageAsync_ShouldHandleDiagramGenerationFailure_Gracefully()
    {
        // Arrange
        var pageTitle = "TestController";
        var filePaths = new List<string> { "Controllers/TestController.cs" };
        var fileContents = new Dictionary<string, string>
        {
            { "Controllers/TestController.cs", "public class TestController { }" }
        };

        var graph = new EnhancedDependencyGraph();
        graph.AddNode("TestController", new NodeMetadata { Type = "Class" });

        _mockLlmClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
            .ReturnsAsync("# TestController\n\nThis is a test controller.");
        _mockGraphService.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);
        _mockDiagramGenerator.Setup(x =>
                x.GenerateSequenceDiagramAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Diagram generation failed"));

        // Act
        var result = await _service.GeneratePageAsync(pageTitle, filePaths, fileContents);

        // Assert
        Assert.That(result.Content, Does.Contain("# TestController"));
        Assert.That(result.Content, Does.Not.Contain("## Sequence Diagram"));

        _mockDiagramGenerator.Verify(x => x.GenerateSequenceDiagramAsync(graph, It.IsAny<string>()), Times.Once);
    }
}