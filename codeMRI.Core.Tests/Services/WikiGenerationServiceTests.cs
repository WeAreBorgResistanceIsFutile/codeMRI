using NUnit.Framework;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using codeMRI.Core.Services.MessageComposition;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

#pragma warning disable CS8602 // Dereference of a possibly null reference.

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class WikiGenerationServiceTests
{
    [SetUp]
    public void Setup()
    {
        _mockLlmFacade = new Mock<ILLMServiceFacade>();
        _mockDiagramGenerator = new Mock<IDiagramGenerator>();
        _mockGraphService = new Mock<IEnhancedDependencyGraphService>();
        _mockLogger = new Mock<ILogger<WikiGenerationService>>();
        _mockSynthesisService = new Mock<IDocumentationSynthesisService>();
        _mockRefService = new Mock<IReferenceManagementService>();


        _mockRefService.Setup(x => x.EnrichContentWithLinks(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((c, id) => c!);

        var options = Options.Create(new CodeWikiOptions());
        var repairService = new MarkdownRepairService();

        _service = new WikiGenerationService(
            _mockLlmFacade!.Object,
            _mockDiagramGenerator!.Object,
            _mockGraphService!.Object,
            _mockSynthesisService!.Object,
            _mockRefService!.Object,
            _mockLogger!.Object,
            options,
            "dummy_model",
            repairService);
    }

    private Mock<ILLMServiceFacade>? _mockLlmFacade;
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

        _mockLlmFacade.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(),
                It.IsAny<MessageCompositionOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse { Content = "# TestController\n\nThis is a test controller.", StrategyUsed = "Simple" });
        _mockGraphService.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph!);
        _mockRefService.Setup(x => x.EnrichContentWithLinks(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((c, id) => c!); // Identity transformation

        // Setup Interactive Diagrams
        _mockDiagramGenerator.Setup(x =>
                x.GenerateInteractiveSequenceDiagramAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<string>(),
                    It.IsAny<DiagramOptions>()))
            .ReturnsAsync(new InteractiveDiagram
            {
                MermaidContent = "sequenceDiagram\n    TestController->>TestService: Call",
                Type = DiagramType.Sequence
            }!);
        _mockDiagramGenerator.Setup(x =>
                x.GenerateInteractiveComponentDiagramAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<string>(),
                    It.IsAny<DiagramOptions>()))
            .ReturnsAsync(new InteractiveDiagram
            {
                MermaidContent =
                    "classDiagram\n    class TestController {\n        +Class\n    }\n    TestController --> TestService",
                Type = DiagramType.Component
            }!);

        // Act
        var result = await _service.GeneratePageAsync(pageTitle, filePaths!, fileContents!);

        // Assert
        Assert.That(result.Content, Does.Contain("## Interactive Sequence Diagram"));
        Assert.That(result.Content, Does.Contain("sequenceDiagram"));
        Assert.That(result.Content, Does.Contain("## Interactive Component Diagram"));
        Assert.That(result.Content, Does.Contain("TestController->>TestService: Call"));

        _mockDiagramGenerator.Verify(
            x => x.GenerateInteractiveSequenceDiagramAsync(graph, It.IsAny<string>(), It.IsAny<DiagramOptions>()),
            Times.Once);
        _mockDiagramGenerator.Verify(
            x => x.GenerateInteractiveComponentDiagramAsync(graph, It.IsAny<string>(), It.IsAny<DiagramOptions>()),
            Times.Once);
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

        _mockLlmFacade.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(),
                It.IsAny<MessageCompositionOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse { Content = "# TestController\n\nThis is a test controller.", StrategyUsed = "Simple" });
        _mockGraphService.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
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
        _mockSynthesisService.Setup(x => x.SynthesizeParentPageAsync(module, childPages, "English",
                AudienceType.Developer, It.IsAny<bool>(), It.IsAny<SynthesisStrategy?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(synthesizedPage);
        _mockRefService.Setup(x => x.EnrichContentWithLinks(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((c, id) => c!);

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

        // Verify delegation to synthesis service
        _mockSynthesisService.Verify(
            x => x.SynthesizeParentPageAsync(module, childPages, "English", AudienceType.Developer, It.IsAny<bool>(),
                It.IsAny<SynthesisStrategy?>(), It.IsAny<CancellationToken>()), Times.Once);
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

        _mockLlmFacade.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(),
                It.IsAny<MessageCompositionOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse { Content = "# TestController\n\nThis is a test controller.", StrategyUsed = "Simple" });
        _mockGraphService.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph!);
        _mockRefService.Setup(x => x.EnrichContentWithLinks(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((c, id) => c!);

        _mockDiagramGenerator.Setup(x =>
                x.GenerateInteractiveSequenceDiagramAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<string>(),
                    It.IsAny<DiagramOptions>()))
            .ThrowsAsync(new Exception("Diagram generation failed"));

        // Act
        var result = await _service.GeneratePageAsync(pageTitle, filePaths!, fileContents!);

        // Assert
        Assert.That(result.Content, Does.Contain("# TestController"));
        Assert.That(result.Content, Does.Not.Contain("## Interactive Sequence Diagram"));

        _mockDiagramGenerator.Verify(
            x => x.GenerateInteractiveSequenceDiagramAsync(graph, It.IsAny<string>(), It.IsAny<DiagramOptions>()),
            Times.Once);
    }

    [Test]
    public async Task GeneratePageAsync_ShouldEnrichContentWithLinks()
    {
        // Arrange
        var pageTitle = "TestController";
        var filePaths = new List<string> { "TestController.cs" };
        var fileContents = new Dictionary<string, string>
        {
            { "TestController.cs", "public class TestController { }" }
        };
        var content = "TestController uses TestService.";
        var enrichedContent = "[TestController](...) uses [TestService](...).";
        _mockLlmFacade.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(),
                It.IsAny<MessageCompositionOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse { Content = content, StrategyUsed = "Simple" });
        _mockGraphService.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnhancedDependencyGraph());

        _mockRefService.Setup(x => x.EnrichContentWithLinks(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(enrichedContent);

        // Act
        var result = await _service.GeneratePageAsync(pageTitle, filePaths, fileContents);

        // Assert
        Assert.That(result.Content, Does.Contain(enrichedContent));
        _mockRefService.Verify(x => x.EnrichContentWithLinks(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task GeneratePageAsync_ShouldFindFileViaGraph_WhenPathsEmpty()
    {
        // Arrange
        var pageTitle = "TestService";
        var expectedPath = "Services/TestService.cs";
        var expectedContent = "public class TestService { }";

        // Create a temp file to simulate the file being found
        var tempDir = Path.Combine(Path.GetTempPath(), "test-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        var servicesDir = Path.Combine(tempDir, "Services");
        Directory.CreateDirectory(servicesDir);
        var tempFile = Path.Combine(servicesDir, "TestService.cs");
        File.WriteAllText(tempFile, expectedContent);

        try
        {
            var graph = new EnhancedDependencyGraph();
            graph.AddNode(pageTitle, new NodeMetadata { FilePath = expectedPath });

            _mockGraphService.Setup(x => x.GetComponentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CodeComponent>());
            _mockGraphService.Setup(x =>
                    x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(graph);

            _mockLlmFacade.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(),
                    It.IsAny<MessageCompositionOptions?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LLMResponse { Content = "# Wiki Page", StrategyUsed = "Simple" });

            _mockRefService.Setup(x => x.EnrichContentWithLinks(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((c, id) => c);

            // Act - Use tempDir as repoPath so the file can be found
            var result = await _service.GeneratePageAsync(pageTitle, new List<string>(),
                new Dictionary<string, string>(), "English", tempDir);

            // Assert
            Assert.That(result.RelevantFiles, Contains.Item(expectedPath));
            _mockGraphService.Verify(x => x.GetComponentsAsync(tempDir, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task GeneratePageAsync_ShouldReloadContent_WhenMissingOrEmpty()
    {
        // Arrange
        var pageTitle = "TestPage";
        var repoPath = "/tmp/test-repo";
        var fileName = "TestFile.cs";
        var filePath = Path.Combine(repoPath, fileName);
        var expectedContent = "public class TestFile {}";

        // Mock File.Exists and ReadAllTextAsync using System.IO.Abstractions isn't available here, 
        // so we rely on the logic that falls back to File.Exists.
        // Since we can't easily mock static File methods without a wrapper, 
        // we will assume the integration test environment or use a real temp file.
        // Given the constraints, let's create a real temp file.

        Directory.CreateDirectory(repoPath);
        await File.WriteAllTextAsync(filePath, expectedContent);

        try
        {
            var filePaths = new List<string> { fileName };
            var fileContents = new Dictionary<string, string>
            {
                { fileName, "" } // Simulating empty content passed from Orchestrator
            };

            // Setup mocks
            _mockLlmFacade.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(),
                    It.IsAny<MessageCompositionOptions?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LLMResponse { Content = "# Wiki Page", StrategyUsed = "Simple" });
            _mockRefService.Setup(x => x.EnrichContentWithLinks(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((c, id) => c);

            // Act
            // We pass repoPath so it can find the file
            var result = await _service.GeneratePageAsync(pageTitle, filePaths, fileContents, "English", repoPath);

            // Assert
            // To verify it read the file, we check if the LLM prompt (which we can capture via Verify) contained the code.
            _mockLlmFacade.Verify(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.Is<string>(prompt => prompt.Contains(expectedContent)), // The crucial assertion
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<MessageCompositionOptions?>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (Directory.Exists(repoPath)) Directory.Delete(repoPath, true);
        }
    }

    [Test]
    public async Task GeneratePageAsync_ShouldStripMarkdownCodeFences_FromLLMOutput()
    {
        // Arrange: LLM returns content wrapped in ```markdown ... ```
        var pageTitle = "TestPage";
        var filePaths = new List<string> { "TestFile.cs" };
        var fileContents = new Dictionary<string, string>
        {
            { "TestFile.cs", "public class TestFile { }" }
        };

        var llmOutput = "```markdown\n\n# TestPage\n\nSome test content here.\n\n```";

        _mockLlmFacade.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(),
                It.IsAny<MessageCompositionOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse { Content = llmOutput, StrategyUsed = "Simple" });
        _mockGraphService.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnhancedDependencyGraph());
        _mockRefService.Setup(x => x.EnrichContentWithLinks(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((c, id) => c);

        // Act
        var result = await _service.GeneratePageAsync(pageTitle, filePaths, fileContents);

        // Assert: The cleaned content should not contain code fences
        Assert.That(result.Content, Does.Not.Contain("```markdown"));
        Assert.That(result.Content, Does.Not.Contain("```\n</details>"),
            "Should not have closing fence before details block");
        Assert.That(result.Content, Does.StartWith("# TestPage"));
        Assert.That(result.Content, Does.Contain("Some test content here"));
    }

    [Test]
    public async Task GeneratePageAsync_ShouldReturnPlaceholder_WhenNoContentAvailable()
    {
        // Arrange: Empty file paths and contents
        var pageTitle = "EmptyPage";
        var filePaths = new List<string>();
        var fileContents = new Dictionary<string, string>();

        // Mock graph service (should not be called in this scenario)
        _mockGraphService.Setup(x => x.GetComponentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CodeComponent>());
        _mockGraphService.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnhancedDependencyGraph());

        // Act
        var result = await _service.GeneratePageAsync(pageTitle, filePaths, fileContents);

        // Assert: Should return placeholder, not call LLM
        Assert.That(result.Title, Is.EqualTo(pageTitle));
        Assert.That(result.Content, Does.Contain("Documentation pending"));
        Assert.That(result.Content, Does.Contain("no source files available"));
        Assert.That(result.RelevantFiles, Is.Empty);

        // Verify LLM was never called with empty content
        _mockLlmFacade.Verify(x => x.ExecuteAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<ChatMessage>>(), It.IsAny<MessageCompositionOptions?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GeneratePageAsync_ShouldReturnPlaceholder_WhenFileContentsAreAllEmpty()
    {
        // Arrange: Files provided but all contents are empty/whitespace
        var pageTitle = "EmptyContentPage";
        var filePaths = new List<string> { "File1.cs", "File2.cs" };
        var fileContents = new Dictionary<string, string>
        {
            { "File1.cs", "" },
            { "File2.cs", "   " } // Only whitespace
        };

        // Act
        var result = await _service.GeneratePageAsync(pageTitle, filePaths, fileContents);

        // Assert: Should return placeholder, not call LLM
        Assert.That(result.Content, Does.Contain("Documentation pending"));
        _mockLlmFacade.Verify(x => x.ExecuteAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<ChatMessage>>(), It.IsAny<MessageCompositionOptions?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GeneratePageAsync_ShouldUseExecuteAsync_WhenContentIsLarge()
    {
        // Arrange
        var pageTitle = "LargeController";
        var fileName = "LargeController.cs";
        var filePaths = new List<string> { fileName };

        // Create large content
        var largeContent = new string('a', 16000);
        var fileContents = new Dictionary<string, string> { { fileName, largeContent } };

        _mockLlmFacade.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(),
                It.IsAny<MessageCompositionOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse { Content = "# LargeController\n\nDocumentation for large controller.", StrategyUsed = "Chunking" });

        _mockGraphService.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnhancedDependencyGraph());
        _mockRefService.Setup(x => x.EnrichContentWithLinks(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((c, id) => c);

        // Act
        var result = await _service.GeneratePageAsync(pageTitle, filePaths, fileContents);

        // Assert
        Assert.That(result.Title, Is.EqualTo(pageTitle));
        _mockLlmFacade.Verify(x => x.ExecuteAsync(
            It.IsAny<string>(),
            It.Is<string>(c => c.Contains(largeContent)),
            It.IsAny<List<ChatMessage>>(),
            It.IsAny<MessageCompositionOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}