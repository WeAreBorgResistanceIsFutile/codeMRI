using codeMRI.Core.Interfaces;
using codeMRI.Core.Services.MessageComposition;
using codeMRI.Core.Services.MessageComposition.Strategies;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace codeMRI.Core.Tests.Services.MessageComposition;

[TestFixture]
public class MessageCompositionOrchestratorTests
{
    private Mock<ILLMValidator> _mockValidator;
    private Mock<ILogger<MessageCompositionOrchestrator>> _mockLogger;
    private Mock<ILogger<SimpleMessageStrategy>> _mockSimpleLogger;
    private MessageCompositionOrchestrator _orchestrator;
    private SimpleMessageStrategy _simpleStrategy;

    [SetUp]
    public void Setup()
    {
        _mockValidator = new Mock<ILLMValidator>();
        _mockLogger = new Mock<ILogger<MessageCompositionOrchestrator>>();
        _mockSimpleLogger = new Mock<ILogger<SimpleMessageStrategy>>();
        
        _mockValidator.Setup(v => v.ContextSize).Returns(4096);
        _mockValidator.Setup(v => v.ResponseBuffer).Returns(1024);
        _mockValidator.Setup(v => v.EstimateTokenCount(It.IsAny<string>())).Returns<string>(s => s.Length / 4);

        _simpleStrategy = new SimpleMessageStrategy(_mockSimpleLogger.Object);
        
        var compositionStrategies = new List<IMessageCompositionStrategy> { _simpleStrategy };
        var iterativeStrategies = new List<IIterativeExecutionStrategy>();
        
        _orchestrator = new MessageCompositionOrchestrator(
            compositionStrategies,
            iterativeStrategies,
            _mockLogger.Object);
    }

    [Test]
    public async Task ComposeMessagesAsync_ShouldSelectSimpleStrategy_WhenContentFits()
    {
        // Arrange
        var context = new MessageCompositionContext
        {
            SystemPrompt = "System",
            TextToProcess = "Short text",
            Validator = _mockValidator.Object
        };

        // Act
        var result = await _orchestrator.ComposeMessagesAsync(context);

        // Assert
        Assert.That(result.StrategyUsed, Is.EqualTo("Simple"));
        Assert.That(result.Messages.Count, Is.GreaterThan(0));
        Assert.That(result.Messages.Any(m => m.Content == "Short text"), Is.True);
    }

    [Test]
    public void ComposeMessagesAsync_ShouldThrow_WhenNoStrategyFound()
    {
        // Arrange
        var orchestrator = new MessageCompositionOrchestrator(
            new List<IMessageCompositionStrategy>(),
            new List<IIterativeExecutionStrategy>(),
            _mockLogger.Object);
            
        var context = new MessageCompositionContext
        {
            Validator = _mockValidator.Object
        };

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await orchestrator.ComposeMessagesAsync(context));
    }
}
