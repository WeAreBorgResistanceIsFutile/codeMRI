using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class HierarchicalSummaryServiceTests
{
    private Mock<ILLMClient> _mockLlmClient;
    private Mock<ILogger<HierarchicalSummaryService>> _mockLogger;
    private HierarchicalSummaryService _service;

    [SetUp]
    public void Setup()
    {
        _mockLlmClient = new Mock<ILLMClient>();
        _mockLlmClient.Setup(x => x.ContextSize).Returns(4096);
        _mockLogger = new Mock<ILogger<HierarchicalSummaryService>>();
        _service = new HierarchicalSummaryService(_mockLlmClient.Object, _mockLogger.Object);
    }

    [Test]
    public void ExtractKeyEntities_ShouldExtractClassNames()
    {
        // Arrange
        var page = new WikiPage
        {
            Id = "1",
            Title = "Test",
            Content = "The `PaymentService` handles payments. The `OrderRepository` stores orders."
        };

        // Act
        var entities = _service.ExtractKeyEntities(page);

        // Assert
        Assert.That(entities.ClassNames, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(entities.ClassNames, Does.Contain("PaymentService"));
        Assert.That(entities.ClassNames, Does.Contain("OrderRepository"));
    }

    [Test]
    public void ExtractKeyEntities_ShouldExtractPatterns()
    {
        // Arrange
        var page = new WikiPage
        {
            Id = "1",
            Title = "Test",
            Content = "This module implements the Repository pattern and uses CQRS for commands."
        };

        // Act
        var entities = _service.ExtractKeyEntities(page);

        // Assert
        Assert.That(entities.PatternNames, Has.Count.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void ExtractKeyEntities_ShouldExtractDependencies()
    {
        // Arrange
        var page = new WikiPage
        {
            Id = "1",
            Title = "Test",
            Content = "Connects to PostgreSQL database and uses Redis for caching."
        };

        // Act
        var entities = _service.ExtractKeyEntities(page);

        // Assert
        Assert.That(entities.DependencyNames, Does.Contain("PostgreSQL"));
        Assert.That(entities.DependencyNames, Does.Contain("Redis"));
    }

    [Test]
    public void ExtractKeyEntities_FromMultiplePages_ShouldDeduplicate()
    {
        // Arrange
        var pages = new List<WikiPage>
        {
            new() { Id = "1", Title = "Page1", Content = "Uses `PaymentService`" },
            new() { Id = "2", Title = "Page2", Content = "Also uses `PaymentService` and `OrderService`" }
        };

        // Act
        var entities = _service.ExtractKeyEntities(pages);

        // Assert
        Assert.That(entities.ClassNames.Count(c => c == "PaymentService"), Is.EqualTo(1));
    }

    [Test]
    public void ExtractKeyEntities_WithEmptyContent_ShouldReturnEmpty()
    {
        // Arrange
        var page = new WikiPage { Id = "1", Title = "Empty", Content = "" };

        // Act
        var entities = _service.ExtractKeyEntities(page);

        // Assert
        Assert.That(entities.HasEntities, Is.False);
    }

    [Test]
    public async Task SummarizeModuleAsync_ShouldCallLLM()
    {
        // Arrange
        var page = new WikiPage
        {
            Id = "1",
            Title = "TestModule",
            Content = "This module handles `PaymentService` operations."
        };

        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Handles payment processing.\n\nKey functions: ProcessPayment, RefundPayment");

        // Act
        var summary = await _service.SummarizeModuleAsync(page);

        // Assert
        Assert.That(summary.ModuleName, Is.EqualTo("TestModule"));
        Assert.That(summary.CorePurpose, Does.Contain("payment"));
    }
}
