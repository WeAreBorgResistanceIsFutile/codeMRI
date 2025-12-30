using NUnit.Framework;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using codeMRI.Core.Services.MessageComposition;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class DocumentationRevisionServiceTests
{
    [SetUp]
    public void Setup()
    {
        _mockLlmFacade = new Mock<ILLMServiceFacade>();
        _mockLogger = new Mock<ILogger<DocumentationRevisionService>>();

        var options = Options.Create(new CodeWikiOptions());
        var markdownRepair = new MarkdownRepairService();
        var mermaidRepair = new MermaidRepairService();
        _service = new DocumentationRevisionService(_mockLlmFacade.Object, _mockLogger.Object, options, markdownRepair, mermaidRepair);
    }

    private Mock<ILLMServiceFacade> _mockLlmFacade;
    private Mock<ILogger<DocumentationRevisionService>> _mockLogger;
    private DocumentationRevisionService _service;

    [Test]
    public async Task ReviseParentDocumentationAsync_ShouldEnrichWithChildDetails()
    {
        // Arrange
        var parentPage = new WikiPage
        {
            Id = "parent-1",
            Title = "DataModule",
            Content = "# DataModule\n\nThis module handles data processing."
        };

        var parentModule = new ModuleNode
        {
            Id = "module-1",
            Name = "DataModule",
            Level = 1
        };

        var childPages = new List<WikiPage>
        {
            new()
            {
                Title = "InputValidator",
                Content = "# InputValidator\n\nValidates incoming data using `ValidationEngine`."
            },
            new()
            {
                Title = "DataTransformer", Content = "# DataTransformer\n\nTransforms data using `TransformPipeline`."
            }
        };

        // Setup LLM to return revised content
        _mockLlmFacade.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.Is<string>(p => p.Contains("DataModule") && p.Contains("Child Module Insights")),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<MessageCompositionOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse
            {
                Content = "# DataModule\n\nThis module orchestrates data processing through InputValidator and DataTransformer components.",
                StrategyUsed = "Simple"
            });

        // Act
        var result = await _service.ReviseParentDocumentationAsync(parentPage, parentModule, childPages);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo("parent-1")); // Preserves original ID
        Assert.That(result.Title, Is.EqualTo("DataModule"));
        Assert.That(result.Content, Does.Contain("InputValidator"));
        Assert.That(result.Content, Does.Contain("DataTransformer"));
        Assert.That(result.Metadata.ContainsKey("IsRevised"), Is.True);
        Assert.That(result.Metadata["IsRevised"], Is.True);
    }

    [Test]
    public async Task ReviseParentDocumentationAsync_ShouldReturnOriginal_WhenNoChildren()
    {
        // Arrange
        var parentPage = new WikiPage
        {
            Id = "parent-1",
            Title = "EmptyModule",
            Content = "# EmptyModule\n\nNo children."
        };

        var parentModule = new ModuleNode { Id = "mod-1", Name = "EmptyModule" };
        var childPages = new List<WikiPage>(); // Empty

        // Act
        var result = await _service.ReviseParentDocumentationAsync(parentPage, parentModule, childPages);

        // Assert
        Assert.That(result, Is.SameAs(parentPage)); // Returns same instance
        _mockLlmFacade.Verify(x => x.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<ChatMessage>>(),
            It.IsAny<MessageCompositionOptions?>(),
            It.IsAny<CancellationToken>()), Times.Never); // Never calls LLM
    }

    [Test]
    public async Task ReviseParentDocumentationAsync_ShouldHandleLLMFailure_Gracefully()
    {
        // Arrange
        var parentPage = new WikiPage
        {
            Id = "parent-1",
            Title = "FailModule",
            Content = "# FailModule\n\nOriginal content."
        };

        var parentModule = new ModuleNode { Id = "mod-1", Name = "FailModule" };
        var childPages = new List<WikiPage>
        {
            new() { Title = "Child", Content = "Child content" }
        };

        // Setup LLM to throw exception
        _mockLlmFacade.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<MessageCompositionOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM service unavailable"));

        // Act
        var result = await _service.ReviseParentDocumentationAsync(parentPage, parentModule, childPages);

        // Assert - Should return original page when revision fails
        Assert.That(result.Id, Is.EqualTo("parent-1"));
        Assert.That(result.Content, Does.Contain("Original content"));
    }

    [Test]
    public async Task ReviseParentDocumentationAsync_ShouldCleanMalformedContent()
    {
        // Arrange
        var parentPage = new WikiPage { Id = "1", Title = "CleanModule", Content = "Original" };
        var parentModule = new ModuleNode { Id = "mod-1", Name = "CleanModule" };
        var childPages = new List<WikiPage> { new() { Title = "Child", Content = "Content" } };

        // LLM returns content wrapped in markdown fences
        _mockLlmFacade.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<MessageCompositionOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse { Content = "```markdown\n# CleanModule\n\nRevised content.\n```", StrategyUsed = "Simple" });

        // Act
        var result = await _service.ReviseParentDocumentationAsync(parentPage, parentModule, childPages);

        // Assert
        Assert.That(result.Content, Does.Contain("# CleanModule"));
        Assert.That(result.Content, Does.Not.Contain("```markdown"));
    }

    [Test]
    public async Task ReviseParentDocumentationAsync_ShouldTrackRevisionChildCount()
    {
        // Arrange
        var parentPage = new WikiPage { Id = "1", Title = "CountModule", Content = "Original" };
        var parentModule = new ModuleNode { Id = "mod-1", Name = "CountModule" };
        var childPages = new List<WikiPage>
        {
            new() { Title = "Child1", Content = "Content 1" },
            new() { Title = "Child2", Content = "Content 2" },
            new() { Title = "Child3", Content = "Content 3" }
        };

        _mockLlmFacade.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<MessageCompositionOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse { Content = "# CountModule\n\nRevised with 3 children.", StrategyUsed = "Simple" });

        // Act
        var result = await _service.ReviseParentDocumentationAsync(parentPage, parentModule, childPages);

        // Assert
        Assert.That(result.Metadata["RevisionChildCount"], Is.EqualTo(3));
    }
}