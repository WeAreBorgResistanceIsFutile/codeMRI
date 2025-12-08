using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

#pragma warning disable CS8602 // Dereference of a possibly null reference.

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class WikiGenerationServiceTests
{
    [SetUp]
    public void Setup()
    {
        _mockLlmClient = new Mock<ILLMClient>();
        _mockDiagramGenerator = new Mock<IDiagramGenerator>();
        _mockGraphService = new Mock<IEnhancedDependencyGraphService>();
        _mockLogger = new Mock<ILogger<WikiGenerationService>> ();
        _mockSynthesisService = new Mock<IDocumentationSynthesisService>();
        _mockRefService = new Mock<IReferenceManagementService>();

        _service = new WikiGenerationService(
            _mockLlmClient!.Object,
            _mockDiagramGenerator!.Object,
            _mockGraphService!.Object,
            _mockSynthesisService!.Object,
            _mockRefService!.Object);
    }

    private Mock<ILLMClient>? _mockLlmClient;
    private Mock<IDiagramGenerator>? _mockDiagramGenerator;
    private Mock<IEnhancedDependencyGraphService>? _mockGraphService;
    private Mock<IDocumentationSynthesisService>? _mockSynthesisService;
    private Mock<IReferenceManagementService>? _mockRefService;
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

        var graph = new EnhancedDependencyGraph();
        graph.AddNode("TestController", new NodeMetadata { Type = "Class" });
        graph.AddNode("TestService", new NodeMetadata { Type = "Class" });
        graph.AddEdge("TestController", "TestService", EdgeType.Call, 1.0);

        _mockLlmClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>()))
            .ReturnsAsync("# TestController\n\nThis is a test controller.");
        _mockGraphService.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<IProgress<ProgressInfo>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph!);
        _mockRefService.Setup(x => x.EnrichContentWithLinks(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((c, id) => c!); // Identity transformation

        // Setup Interactive Diagrams
        _mockDiagramGenerator.Setup(x =>
                x.GenerateInteractiveSequenceDiagramAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<string>(), It.IsAny<DiagramOptions>()))
            .ReturnsAsync(new InteractiveDiagram 
            {
                MermaidContent = "sequenceDiagram\n    TestController->>TestService: Call",
                Type = DiagramType.Sequence 
            }!);
        _mockDiagramGenerator.Setup(x =>
                x.GenerateInteractiveComponentDiagramAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<string>(), It.IsAny<DiagramOptions>()))
            .ReturnsAsync(new InteractiveDiagram 
            {
                MermaidContent = "classDiagram\n    class TestController {\n        +Class\n    }\n    TestController --> TestService",
                Type = DiagramType.Component
            }!);

        // Act
        var result = await _service.GeneratePageAsync(pageTitle, filePaths!, fileContents!);

        // Assert
        Assert.That(result.Content, Does.Contain("## Interactive Sequence Diagram"));
        Assert.That(result.Content, Does.Contain("sequenceDiagram"));
        Assert.That(result.Content, Does.Contain("## Interactive Component Diagram"));
        Assert.That(result.Content, Does.Contain("TestController->>TestService: Call"));

        _mockDiagramGenerator.Verify(x => x.GenerateInteractiveSequenceDiagramAsync(graph, It.IsAny<string>(), It.IsAny<DiagramOptions>()), Times.Once);
        _mockDiagramGenerator.Verify(x => x.GenerateInteractiveComponentDiagramAsync(graph, It.IsAny<string>(), It.IsAny<DiagramOptions>()), Times.Once);
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

        _mockLlmClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>()))
            .ReturnsAsync("# TestController\n\nThis is a test controller.");
        _mockGraphService.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<IProgress<ProgressInfo>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyGraph!);
        _mockRefService.Setup(x => x.EnrichContentWithLinks(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((c, id) => c!);

        // Act
        var result = await _service.GeneratePageAsync(pageTitle, filePaths!, fileContents!);

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

        // Setup Synthesis Service to return the base page
        var synthesizedPage = new WikiPage 
        { 
             Title = module.Name,
             Content = "# Test Module\n\nThis is a test module overview."
        };
        _mockSynthesisService.Setup(x => x.SynthesizeParentPageAsync(module, childPages, "English"))
            .ReturnsAsync(synthesizedPage);
        _mockRefService.Setup(x => x.EnrichContentWithLinks(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((c, id) => c!);

        _mockGraphService.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<IProgress<ProgressInfo>?>(), It.IsAny<CancellationToken>()))
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
        
        // Verify delegation to synthesis service
        _mockSynthesisService.Verify(x => x.SynthesizeParentPageAsync(module, childPages, "English"), Times.Once);
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

        _mockLlmClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>()))
            .ReturnsAsync("# TestController\n\nThis is a test controller.");
        _mockGraphService.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<IProgress<ProgressInfo>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph!);
        _mockRefService.Setup(x => x.EnrichContentWithLinks(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((c, id) => c!);

        _mockDiagramGenerator.Setup(x =>
                x.GenerateInteractiveSequenceDiagramAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<string>(), It.IsAny<DiagramOptions>()))
            .ThrowsAsync(new Exception("Diagram generation failed"));

        // Act
        var result = await _service.GeneratePageAsync(pageTitle, filePaths!, fileContents!);

        // Assert
        Assert.That(result.Content, Does.Contain("# TestController"));
        Assert.That(result.Content, Does.Not.Contain("## Interactive Sequence Diagram"));

        _mockDiagramGenerator.Verify(x => x.GenerateInteractiveSequenceDiagramAsync(graph, It.IsAny<string>(), It.IsAny<DiagramOptions>()), Times.Once);
    }

    [Test]
    public async Task GeneratePageAsync_ShouldEnrichContentWithLinks()
    {
        // Arrange
        var pageTitle = "TestController";
        var content = "TestController uses TestService.";
        var enrichedContent = "[TestController](...) uses [TestService](...).";

        _mockLlmClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>()))
            .ReturnsAsync(content);
        _mockGraphService.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<IProgress<ProgressInfo>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnhancedDependencyGraph());
        
        _mockRefService.Setup(x => x.EnrichContentWithLinks(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(enrichedContent);

        // Act
        var result = await _service.GeneratePageAsync(pageTitle, new List<string>(), new Dictionary<string, string>());

        // Assert
        Assert.That(result.Content, Does.Contain(enrichedContent));
        _mockRefService.Verify(x => x.EnrichContentWithLinks(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
    [Test]
    public async Task GeneratePageAsync_ShouldFindFileViaGraph_WhenPathsEmpty()
    {
        // Arrange
        var pageTitle = "TestService";
        var repoPath = "/src/repo";
        var expectedPath = "Services/TestService.cs";
        
        var graph = new EnhancedDependencyGraph();
        graph.AddNode(pageTitle, new NodeMetadata { FilePath = expectedPath });

        _mockGraphService.Setup(x => x.GetComponentsAsync(repoPath, It.IsAny<IProgress<ProgressInfo>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CodeComponent>());
        _mockGraphService.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<IProgress<ProgressInfo>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);
            
        _mockLlmClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>()))
            .ReturnsAsync("# Wiki Page");
            
        _mockRefService.Setup(x => x.EnrichContentWithLinks(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((c, id) => c);

        // Act
        var result = await _service.GeneratePageAsync(pageTitle, new List<string>(), new Dictionary<string, string>(), "English", repoPath);

        // Assert
        Assert.That(result.RelevantFiles, Contains.Item(expectedPath));
        _mockGraphService.Verify(x => x.GetComponentsAsync(repoPath, It.IsAny<IProgress<ProgressInfo>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}