using codeMRI.Core.Interfaces;
using codeMRI.Core.Services.MessageComposition;
using codeMRI.Core.Services.MessageComposition.Strategies;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace codeMRI.Core.Tests.Services.MessageComposition;

[TestFixture]
public class ChunkingMessageStrategyTests
{
    private Mock<ILLMValidator> _mockValidator;
    private Mock<ILogger<ChunkingMessageStrategy>> _mockLogger;
    private Mock<ILLMClient> _mockLlmClient;
    private ChunkingMessageStrategy _strategy;

    [SetUp]
    public void Setup()
    {
        _mockValidator = new Mock<ILLMValidator>();
        _mockLogger = new Mock<ILogger<ChunkingMessageStrategy>>();
        _mockLlmClient = new Mock<ILLMClient>();
        
        _mockValidator.Setup(v => v.ContextSize).Returns(100); // Very small for testing
        _mockValidator.Setup(v => v.ResponseBuffer).Returns(20);
        _mockValidator.Setup(v => v.EstimateTokenCount(It.IsAny<string>())).Returns<string>(s => s.Length);

        _strategy = new ChunkingMessageStrategy(_mockLogger.Object);
    }

    [Test]
    public void CanHandle_ShouldReturnTrue_WhenContentIsLarge()
    {
        // Arrange
        var context = new MessageCompositionContext
        {
            TextToProcess = new string('a', 150), // Larger than ContextSize
            Validator = _mockValidator.Object
        };

        // Act
        var result = _strategy.CanHandle(context);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task ExecuteAsync_ShouldCallChatAsyncMultipleTimes()
    {
        // Arrange
        // With ContextSize=100, chunkSize = 100 * 0.6 * 4 = 240 chars
        // We need text > 240 to create multiple chunks
        var context = new MessageCompositionContext
        {
            SystemPrompt = "Task",
            TextToProcess = new string('a', 500), // Large enough for multiple chunks
            Validator = _mockValidator.Object
        };

        _mockLlmClient.Setup(x => x.ChatAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Finding");

        // Act
        var result = await _strategy.ExecuteAsync(context, _mockLlmClient.Object);

        // Assert
        // ChunkingMessageStrategy makes one call per chunk PLUS one final synthesis call
        Assert.That(result.IterationsProcessed, Is.GreaterThan(1), "Should process multiple chunks");
        _mockLlmClient.Verify(x => x.ChatAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeast(3)); // 2+ chunks + 1 synthesis
    }

    [Test]
    public async Task ExecuteAsync_WhenFindingsAreTooLarge_ShouldCompactAndNotTruncate()
    {
        // Arrange
        // We need larger context for this test
        _mockValidator.Setup(v => v.ContextSize).Returns(1000);
        _mockValidator.Setup(v => v.ResponseBuffer).Returns(100);
        
        var context = new MessageCompositionContext
        {
            SystemPrompt = "Task",
            TextToProcess = new string('a', 5000), // Many chunks
            Validator = _mockValidator.Object,
            Model = "test-model"
        };

        // First call returns a large response
        var largeFindings = new string('f', 300);
        
        var callCount = 0;
        _mockLlmClient.Setup(x => x.ChatAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<ChatMessage> messages, string model, CancellationToken ct) => {
                callCount++;
                if (messages.Any(m => m.Role == "user" && m.Content.Contains("Summarize these findings")))
                {
                    return "Compacted findings";
                }
                if (callCount == 1) return largeFindings;
                return "Subsequent findings";
            });

        // Act
        await _strategy.ExecuteAsync(context, _mockLlmClient.Object);

        // Assert
        // Verify that the LogInformation (compaction) was called
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Rolling findings context too large")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.AtLeastOnce, "Should log information about compaction");

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Accumulated findings too large")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Never, "Should NOT log warning about truncation");
            
        // Verify that the LLM was used for compaction
        _mockLlmClient.Verify(
            x => x.ChatAsync(
                It.Is<List<ChatMessage>>(m => 
                    m.Any(m => m.Role == "user" && m.Content.Contains("Summarize these findings"))),
                "test-model",
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce, "Should call LLM for compaction");
    }

    [Test]
    public async Task SynthesizeFinalResult_WhenFindingsTooLarge_ShouldPerformHierarchicalSynthesis()
    {
        // Arrange
        // We need larger context for this test
        _mockValidator.Setup(v => v.ContextSize).Returns(1000);
        _mockValidator.Setup(v => v.ResponseBuffer).Returns(100);
        
        var context = new MessageCompositionContext
        {
            SystemPrompt = "Task",
            TextToProcess = new string('a', 2000), 
            Validator = _mockValidator.Object,
            Model = "test-model"
        };

        // Mock ChatAsync to return large findings
        int callCount = 0;
        _mockLlmClient.Setup(x => x.ChatAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<ChatMessage> messages, string model, CancellationToken ct) => {
                callCount++;
                
                // If it's a compaction request (used in hierarchical synthesis) or final synthesis
                // Check if it is NOT a chunk processing request
                if (messages.Any(m => m.Role == "user" && m.Content.Contains("CHUNK")))
                {
                    // Regular chunk processing - return a large finding (300 chars)
                    return "Finding " + new string('x', 290);
                }
                
                // For everything else (Compaction, Final Synthesis), return a small string
                // to ensure hierarchical synthesis terminates.
                if (messages.Any(m => m.Role == "user" && m.Content.Contains("final well-structured response")))
                {
                    return "Final Result";
                }
                
                return "Group Summary";
            });

        // Act
        await _strategy.ExecuteAsync(context, _mockLlmClient.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Performing hierarchical synthesis")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.AtLeastOnce, "Should log information about hierarchical synthesis");
    }
}
