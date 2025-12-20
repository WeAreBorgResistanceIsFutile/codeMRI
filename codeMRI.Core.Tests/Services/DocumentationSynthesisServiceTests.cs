using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class DocumentationSynthesisServiceTests
{
    [SetUp]
    public void Setup()
    {
        _mockLlmClient = new Mock<ILLMClient>();
        _mockLlmClient.Setup(x => x.ContextSize).Returns(4096);
        _mockLogger = new Mock<ILogger<DocumentationSynthesisService>>();

        var options = Options.Create(new CodeWikiOptions());
        _service = new DocumentationSynthesisService(_mockLlmClient.Object, _mockLogger.Object, options);
    }

    private Mock<ILLMClient> _mockLlmClient;
    private Mock<ILogger<DocumentationSynthesisService>> _mockLogger;
    private DocumentationSynthesisService _service;

    [Test]
    public async Task SynthesizeParentPageAsync_ShouldUseParentPageSynthesisPrompt()
    {
        // Arrange
        var module = new ModuleNode
        {
            Id = "module-1",
            Name = "PaymentSystem",
            Level = 1,
            Description = "Handles payment processing"
        };
        module.Metadata["ArchitecturalPattern"] = "Repository";

        var childPages = new List<WikiPage>
        {
            new() { Title = "PaymentGateway", Content = "Handles stripe integration." },
            new() { Title = "TransactionLogger", Content = "Logs all transactions to DB." }
        };

        // Mock the LLM response for the ParentPageSynthesisPrompt
        _mockLlmClient.Setup(x => x.ChatAsync(
                It.Is<string>(s => s.Contains("master software architect")),
                It.Is<string>(s => s.Contains("PaymentSystem") && s.Contains("Child Modules")),
                It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# PaymentSystem\n\nHigh level overview of the payment system architecture...");

        // Act
        var result = await _service.SynthesizeParentPageAsync(module, childPages);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Title, Is.EqualTo("PaymentSystem"));
        Assert.That(result.Content, Does.Contain("# PaymentSystem"));
        Assert.That(result.Content, Does.Contain("High level overview"));

        // Verify single LLM call was made (using ParentPageSynthesisPrompt)
        _mockLlmClient.Verify(x => x.ChatAsync(
            It.IsAny<string>(),
            It.Is<string>(s => s.Contains("Child Modules")),
            It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SynthesizeParentPageAsync_ShouldHandleEmptyChildren()
    {
        // Arrange
        var module = new ModuleNode { Id = "mod-empty", Name = "EmptyModule" };
        var childPages = new List<WikiPage>();

        _mockLlmClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# EmptyModule\n\nNo children detected for this module.");

        // Act
        var result = await _service.SynthesizeParentPageAsync(module, childPages);

        // Assert
        Assert.That(result.Content, Does.Contain("EmptyModule"));
    }

    [Test]
    public async Task SynthesizeParentPageAsync_ShouldIncludeArchitecturalPatterns()
    {
        // Arrange
        var module = new ModuleNode { Id = "mod-1", Name = "Core" };
        module.Metadata["ArchitecturalPattern"] = "MVC";

        var childPages = new List<WikiPage> { new() { Title = "Child", Content = "Content" } };

        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.Is<string>(p => p.Contains("Detected Pattern") && p.Contains("MVC")),
                It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# Core\n\nThis module implements the MVC Pattern for clean separation of concerns.");

        // Act
        var result = await _service.SynthesizeParentPageAsync(module, childPages);

        // Assert
        Assert.That(result.Content, Does.Contain("MVC Pattern"));
    }

    [Test]
    public async Task SynthesizeParentPageAsync_ShouldIncludeQualityMetricsContext()
    {
        // Arrange
        var module = new ModuleNode { Id = "mod-metrics", Name = "MetricsModule", Level = 2 };
        module.QualityMetrics.Cohesion = 0.85;
        module.QualityMetrics.Coupling = 0.15;
        module.ComplexityScore = 42;


        var childPages = new List<WikiPage> { new() { Title = "Component", Content = "Component content" } };

        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.Is<string>(p => p.Contains("Cohesion") && p.Contains("Coupling")),
                It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# MetricsModule\n\nThis is a well-designed module with high cohesion.");

        // Act
        var result = await _service.SynthesizeParentPageAsync(module, childPages);

        // Assert
        Assert.That(result.Content, Does.Contain("MetricsModule"));
    }

    [Test]
    public async Task SynthesizeParentPageAsync_ShouldCleanMalformedContent()
    {
        // Arrange
        var module = new ModuleNode { Id = "mod-clean", Name = "CleanModule" };
        var childPages = new List<WikiPage> { new() { Title = "Child", Content = "Content" } };

        // Simulate LLM returning content wrapped in markdown fences
        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("```markdown\n# CleanModule\n\nSome content here.\n```");

        // Act
        var result = await _service.SynthesizeParentPageAsync(module, childPages);

        // Assert
        Assert.That(result.Content, Does.Contain("# CleanModule"));
        Assert.That(result.Content, Does.Not.Contain("```markdown"));
    }
}