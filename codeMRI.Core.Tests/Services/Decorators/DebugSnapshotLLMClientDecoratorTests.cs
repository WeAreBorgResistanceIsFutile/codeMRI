using NUnit.Framework;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services.Decorators;
using codeMRI.Core.Services.MessageComposition;
using Microsoft.Extensions.Logging;
using Moq;

namespace codeMRI.Core.Tests.Services.Decorators;

[TestFixture]
public class DebugSnapshotLLMClientDecoratorTests
{
    private Mock<ILLMClient> _mockInner;
    private Mock<IDebugSnapshotService> _mockSnapshotService;
    private Mock<ILLMInvocationContext> _mockInvocationContext;
    private Mock<ILLMValidator> _mockValidator;
    private Mock<ILogger<DebugSnapshotLLMClientDecorator>> _mockLogger;
    private DebugSnapshotLLMClientDecorator _decorator;

    [SetUp]
    public void Setup()
    {
        _mockInner = new Mock<ILLMClient>();
        _mockSnapshotService = new Mock<IDebugSnapshotService>();
        _mockInvocationContext = new Mock<ILLMInvocationContext>();
        _mockValidator = new Mock<ILLMValidator>();
        _mockLogger = new Mock<ILogger<DebugSnapshotLLMClientDecorator>>();

        _mockInvocationContext.Setup(c => c.RepoPath).Returns("/test/repo");
        
        _decorator = new DebugSnapshotLLMClientDecorator(
            _mockInner.Object,
            _mockSnapshotService.Object,
            _mockInvocationContext.Object,
            _mockValidator.Object,
            _mockLogger.Object);
    }

    [Test]
    public async Task ChatAsync_OnFailure_ShouldIncludeTokenCount()
    {
        // Arrange
        var messages = new List<ChatMessage> { new ChatMessage { Role = "user", Content = "hi" } };
        var expectedTokens = 10;
        
        _mockValidator.Setup(v => v.ValidateMessages(messages)).Returns(new MessageValidationResult
        {
            IsValid = true,
            EstimatedTokens = expectedTokens,
            AvailableTokens = 100
        });
        
        _mockInner.Setup(s => s.ChatAsync(messages, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Fail"));

        // Act & Assert
        Assert.ThrowsAsync<Exception>(() => _decorator.ChatAsync(messages));

        _mockSnapshotService.Verify(s => s.SaveSnapshotAsync(
            It.IsAny<string>(),
            It.Is<DebugSnapshot>(snap => 
                snap.Metadata.ContainsKey("tokenCount") && 
                snap.Metadata["tokenCount"] == expectedTokens.ToString())),
            Times.Once);
    }
}
