using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class DocumentationSynthesisServiceTests
{
    private Mock<ILLMClient> _mockLlmClient;
    private DocumentationSynthesisService _service;

    [SetUp]
    public void Setup()
    {
        _mockLlmClient = new Mock<ILLMClient>();
        _service = new DocumentationSynthesisService(_mockLlmClient.Object);
    }

    [Test]
    public async Task SynthesizeParentPageAsync_ShouldPerformMultiStageSynthesis()
    {
        // Arrange
        var module = new ModuleNode
        {
            Id = "module-1",
            Name = "PaymentSystem",
            Level = 1,
            Description = "Handles payment processing"
        };

        var childPages = new List<WikiPage>
        {
            new() { Title = "PaymentGateway", Content = "Handles stripe integration." },
            new() { Title = "TransactionLogger", Content = "Logs all transactions to DB." }
        };

        // Mock Theme Analysis response
        _mockLlmClient.Setup(x => x.ChatAsync(
                It.Is<string>(s => s.Contains("architect")), // system prompt
                It.Is<string>(s => s.Contains("theme") && s.Contains("pattern")), // user prompt asking for themes
                It.IsAny<List<ChatMessage>>(), It.IsAny<string?>()))
            .ReturnsAsync("Theme: Secure Transactions\nPattern: Repository Pattern");

        // Mock Overview Synthesis response
        _mockLlmClient.Setup(x => x.ChatAsync(
                It.Is<string>(s => s.Contains("expert")),
                It.Is<string>(s => s.Contains("PaymentSystem") && s.Contains("Secure Transactions")), // prompt using module name and identified themes
                It.IsAny<List<ChatMessage>>(), It.IsAny<string?>()))
            .ReturnsAsync("# PaymentSystem\n\nHigh level overview...");

        // Act
        var result = await _service.SynthesizeParentPageAsync(module, childPages);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Title, Is.EqualTo("PaymentSystem"));
        Assert.That(result.Content, Does.Contain("# PaymentSystem"));
        Assert.That(result.Content, Does.Contain("High level overview"));
        
        // Verify multiple LLM calls were made (Multi-stage)
        _mockLlmClient.Verify(x => x.ChatAsync(
            It.IsAny<string>(),
            It.Is<string>(s => s.Contains("theme") || s.Contains("pattern")),
            It.IsAny<List<ChatMessage>>(), It.IsAny<string?>()), Times.AtLeastOnce, "Should request theme analysis");

        _mockLlmClient.Verify(x => x.ChatAsync(
            It.IsAny<string>(),
            It.Is<string>(s => s.Contains("overview") || s.Contains("synthesis")),
            It.IsAny<List<ChatMessage>>(), It.IsAny<string?>()), Times.AtLeastOnce, "Should request overview synthesis");
    }

    [Test]
    public async Task SynthesizeParentPageAsync_ShouldHandleEmptyChildren()
    {
        // Arrange
        var module = new ModuleNode { Id = "mod-empty", Name = "EmptyModule" };
        var childPages = new List<WikiPage>();

        _mockLlmClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>()))
            .ReturnsAsync("# EmptyModule\n\nNo children.");

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
        var childPages = new List<WikiPage> { new() { Title = "Child", Content = "Content" } };

        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.Is<string>(p => p.Contains("pattern")),
                It.IsAny<List<ChatMessage>>(), It.IsAny<string?>()))
            .ReturnsAsync("- MVC Pattern\n- Singleton");

         _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.Is<string>(p => !p.Contains("pattern")), // The synthesis prompt
                It.IsAny<List<ChatMessage>>(), It.IsAny<string?>()))
            .ReturnsAsync("# Core\n\nUses MVC Pattern.");

        // Act
        var result = await _service.SynthesizeParentPageAsync(module, childPages);

        // Assert
        Assert.That(result.Content, Does.Contain("MVC Pattern"));
    }
}
