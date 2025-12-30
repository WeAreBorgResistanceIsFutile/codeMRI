using Moq;
using NUnit.Framework;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;
using codeMRI.Core.Services.MessageComposition;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class LLMServiceFacadeMetricsTests
{
    private Mock<IMessageCompositionOrchestrator> _mockOrchestrator;
    private Mock<ILLMClient> _mockLlmClient;
    private Mock<ILLMValidator> _mockValidator;
    private Mock<ILogger<LLMServiceFacade>> _mockLogger;
    private LLMServiceFacade _facade;

    [SetUp]
    public void SetUp()
    {
        _mockOrchestrator = new Mock<IMessageCompositionOrchestrator>();
        _mockLlmClient = new Mock<ILLMClient>();
        _mockValidator = new Mock<ILLMValidator>();
        _mockLogger = new Mock<ILogger<LLMServiceFacade>>();
        
        _facade = new LLMServiceFacade(
            _mockOrchestrator.Object,
            _mockLlmClient.Object,
            _mockValidator.Object,
            _mockLogger.Object);
    }

    [Test]
    public async Task ExecuteAsync_ShouldCallMetricsCallback_WhenUsingSimpleStrategy()
    {
        // Arrange
        var context = new MessageCompositionContext 
        { 
            Model = "test-model",
            Validator = _mockValidator.Object
        };
        var compositionResult = new CompositionResult
        {
            StrategyUsed = "Simple",
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "hello" } }
        };

        _mockOrchestrator
            .Setup(o => o.ComposeMessagesAsync(It.IsAny<MessageCompositionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(compositionResult);

        _mockLlmClient
            .Setup(c => c.ChatAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("response");

        BenchmarkMetrics? recordedMetrics = null;
        _facade.SetMetricsCallback(m => recordedMetrics = m);

        // Act
        await _facade.ExecuteAsync("system", "text");

        // Assert
        Assert.That(recordedMetrics, Is.Not.Null, "Metrics should be recorded for simple strategy");
        Assert.That(recordedMetrics!.ModelName, Is.EqualTo("test-model"));
        Assert.That(recordedMetrics!.TaskType, Is.EqualTo("Simple"));
    }

    [Test]
    public async Task ExecuteAsync_ShouldCallMetricsCallback_WhenUsingIterativeStrategy()
    {
        // Arrange
        var mockIterativeStrategy = new Mock<IIterativeExecutionStrategy>();
        mockIterativeStrategy.Setup(s => s.StrategyName).Returns("Iterative");
        
        var compositionResult = new CompositionResult
        {
            StrategyUsed = "Iterative",
            Messages = new List<ChatMessage>(),
            Metadata = new Dictionary<string, object>
            {
                ["IsIterativeStrategy"] = true,
                ["Strategy"] = mockIterativeStrategy.Object
            }
        };

        _mockOrchestrator
            .Setup(o => o.ComposeMessagesAsync(It.IsAny<MessageCompositionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(compositionResult);

        mockIterativeStrategy
            .Setup(s => s.ExecuteAsync(It.IsAny<MessageCompositionContext>(), It.IsAny<ILLMClient>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IterativeExecutionResult { FinalResponse = "iterative response", IterationsProcessed = 3 });

        BenchmarkMetrics? recordedMetrics = null;
        _facade.SetMetricsCallback(m => recordedMetrics = m);

        // Act
        await _facade.ExecuteAsync("system", "text");

        // Assert
        Assert.That(recordedMetrics, Is.Not.Null, "Metrics should be recorded for iterative strategy");
        Assert.That(recordedMetrics!.TaskType, Is.EqualTo("Iterative"));
    }
}
