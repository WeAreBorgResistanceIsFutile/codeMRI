using NUnit.Framework;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services.Decorators;
using codeMRI.Core.Services.MessageComposition;
using Microsoft.Extensions.Logging;
using Moq;

namespace codeMRI.Core.Tests.Services.Decorators;

[TestFixture]
public class DebugSnapshotEmbeddingServiceDecoratorTests
{
    private Mock<IEmbeddingService> _mockInner;
    private Mock<IDebugSnapshotService> _mockSnapshotService;
    private Mock<ILLMInvocationContext> _mockInvocationContext;
    private Mock<ILLMValidator> _mockValidator;
    private Mock<ILogger<DebugSnapshotEmbeddingServiceDecorator>> _mockLogger;
    private DebugSnapshotEmbeddingServiceDecorator _decorator;

    [SetUp]
    public void Setup()
    {
        _mockInner = new Mock<IEmbeddingService>();
        _mockSnapshotService = new Mock<IDebugSnapshotService>();
        _mockInvocationContext = new Mock<ILLMInvocationContext>();
        _mockValidator = new Mock<ILLMValidator>();
        _mockLogger = new Mock<ILogger<DebugSnapshotEmbeddingServiceDecorator>>();

        _mockInvocationContext.Setup(c => c.RepoPath).Returns("/test/repo");
        _mockInvocationContext.Setup(c => c.JobId).Returns("test-job");
        _mockInvocationContext.Setup(c => c.ComponentId).Returns("test-comp");

        _decorator = new DebugSnapshotEmbeddingServiceDecorator(
            _mockInner.Object,
            _mockSnapshotService.Object,
            _mockInvocationContext.Object,
            _mockValidator.Object, // Now takes validator
            _mockLogger.Object);
    }

    [Test]
    public async Task GetEmbeddingAsync_OnFailure_ShouldIncludeTokenCount()
    {
        // Arrange
        var text = "Some text to embed";
        var expectedTokens = 4;
        _mockValidator.Setup(v => v.EstimateTokenCount(text)).Returns(expectedTokens);
        _mockInner.Setup(s => s.GetEmbeddingAsync(text)).ThrowsAsync(new Exception("Fail"));

        // Act & Assert
        Assert.ThrowsAsync<Exception>(() => _decorator.GetEmbeddingAsync(text));

        _mockSnapshotService.Verify(s => s.SaveSnapshotAsync(
            It.IsAny<string>(),
            It.Is<DebugSnapshot>(snap => 
                snap.Metadata.ContainsKey("tokenCount") && 
                snap.Metadata["tokenCount"] == expectedTokens.ToString())),
            Times.Once);
    }

    [Test]
    public async Task GetEmbeddingAsync_OnFailure_ShouldPopulateUserPromptAndModel()
    {
        // Arrange
        var text = "Some text to embed";
        var expectedModel = "test-model";
        _mockInner.Setup(s => s.ModelName).Returns(expectedModel);
        _mockInner.Setup(s => s.GetEmbeddingAsync(text)).ThrowsAsync(new Exception("Fail"));

        // Act & Assert
        Assert.ThrowsAsync<Exception>(() => _decorator.GetEmbeddingAsync(text));

        _mockSnapshotService.Verify(s => s.SaveSnapshotAsync(
            It.IsAny<string>(),
            It.Is<DebugSnapshot>(snap => snap.UserPrompt == text && snap.Model == expectedModel)),
            Times.Once, "UserPrompt and Model should be correctly populated");
    }
}
