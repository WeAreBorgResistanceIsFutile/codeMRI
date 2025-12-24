using NUnit.Framework;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using codeMRI.Core.Services.MessageComposition;
using Microsoft.Extensions.Logging;
using Moq;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class HierarchicalSummaryServiceTests
{
    [SetUp]
    public void Setup()
    {
        _mockLlmFacade = new Mock<ILLMServiceFacade>();
        _mockLogger = new Mock<ILogger<HierarchicalSummaryService>>();
        _service = new HierarchicalSummaryService(_mockLlmFacade.Object, _mockLogger.Object);
    }

    private Mock<ILLMServiceFacade> _mockLlmFacade;
    private Mock<ILogger<HierarchicalSummaryService>> _mockLogger;
    private HierarchicalSummaryService _service;

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

        _mockLlmFacade.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<MessageCompositionOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse { Content = "Handles payment processing.\n\nKey functions: ProcessPayment, RefundPayment", StrategyUsed = "Simple" });

        // Act
        var summary = await _service.SummarizeModuleAsync(page);

        // Assert
        Assert.That(summary.ModuleName, Is.EqualTo("TestModule"));
        Assert.That(summary.CorePurpose, Does.Contain("payment"));
    }
}