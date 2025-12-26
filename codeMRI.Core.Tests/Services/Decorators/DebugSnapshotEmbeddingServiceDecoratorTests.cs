using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services.Decorators;
using Microsoft.Extensions.Logging;
using Moq;

namespace codeMRI.Core.Tests.Services.Decorators;

[TestFixture]
public class DebugSnapshotEmbeddingServiceDecoratorTests
{
    private Mock<IEmbeddingService> _mockInner = null!;
    private Mock<IDebugSnapshotService> _mockSnapshotService = null!;
    private Mock<ILLMInvocationContext> _mockContext = null!;
    private Mock<ILogger<DebugSnapshotEmbeddingServiceDecorator>> _mockLogger = null!;
    private DebugSnapshotEmbeddingServiceDecorator _decorator = null!;

    [SetUp]
    public void SetUp()
    {
        _mockInner = new Mock<IEmbeddingService>();
        _mockSnapshotService = new Mock<IDebugSnapshotService>();
        _mockContext = new Mock<ILLMInvocationContext>();
        _mockLogger = new Mock<ILogger<DebugSnapshotEmbeddingServiceDecorator>>();

        _decorator = new DebugSnapshotEmbeddingServiceDecorator(
            _mockInner.Object,
            _mockSnapshotService.Object,
            _mockContext.Object,
            _mockLogger.Object);
    }

    [Test]
    public async Task GetEmbeddingAsync_WhenSuccessful_ShouldNotSaveSnapshot()
    {
        // Arrange
        var embedding = new float[] { 1.0f, 2.0f, 3.0f };
        _mockInner.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(embedding);

        // Act
        var result = await _decorator.GetEmbeddingAsync("test text");

        // Assert
        Assert.That(result, Is.EqualTo(embedding));
        _mockSnapshotService.Verify(
            x => x.SaveSnapshotAsync(It.IsAny<string>(), It.IsAny<DebugSnapshot>()),
            Times.Never);
    }

    [Test]
    public async Task GetEmbeddingAsync_WhenFails_ShouldSaveSnapshotWithTextAndError()
    {
        // Arrange
        var testText = "test embedding text";
        var errorMessage = "Response status code does not indicate success: 500 (Internal Server Error). Error: {\"error\":\"model not found\"}";
        var exception = new HttpRequestException(errorMessage);

        _mockContext.Setup(x => x.RepoPath).Returns("/test/repo");
        _mockContext.Setup(x => x.JobId).Returns("test-job-123");
        _mockContext.Setup(x => x.ComponentId).Returns("DocumentationIndexer");

        _mockInner.Setup(x => x.GetEmbeddingAsync(testText))
            .ThrowsAsync(exception);

        DebugSnapshot? capturedSnapshot = null;
        _mockSnapshotService
            .Setup(x => x.SaveSnapshotAsync(It.IsAny<string>(), It.IsAny<DebugSnapshot>()))
            .Callback<string, DebugSnapshot>((_, snapshot) => capturedSnapshot = snapshot)
            .ReturnsAsync("/test/repo/.codemri/debug/snapshot.json");

        // Act & Assert
        Assert.ThrowsAsync<HttpRequestException>(
            async () => await _decorator.GetEmbeddingAsync(testText));

        // Verify snapshot was saved
        _mockSnapshotService.Verify(
            x => x.SaveSnapshotAsync("/test/repo", It.IsAny<DebugSnapshot>()),
            Times.Once);

        // Verify snapshot content
        Assert.That(capturedSnapshot, Is.Not.Null);
        Assert.That(capturedSnapshot!.JobId, Is.EqualTo("test-job-123"));
        Assert.That(capturedSnapshot.ComponentId, Is.EqualTo("DocumentationIndexer"));
        Assert.That(capturedSnapshot.Error, Contains.Substring("500"));
        Assert.That(capturedSnapshot.Error, Contains.Substring("model not found"));
        Assert.That(capturedSnapshot.Metadata.ContainsKey("embeddingText"), Is.True);
        Assert.That(capturedSnapshot.Metadata["embeddingText"], Is.EqualTo(testText));
    }

    [Test]
    public async Task GetEmbeddingAsync_WhenFailsAndRepoPathMissing_ShouldLogWarningAndNotSaveSnapshot()
    {
        // Arrange
        _mockContext.Setup(x => x.RepoPath).Returns((string?)null);
        _mockInner.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("Test error"));

        // Act & Assert
        Assert.ThrowsAsync<HttpRequestException>(
            async () => await _decorator.GetEmbeddingAsync("test"));

        _mockSnapshotService.Verify(
            x => x.SaveSnapshotAsync(It.IsAny<string>(), It.IsAny<DebugSnapshot>()),
            Times.Never);
    }

    [Test]
    public void GetDimensions_ShouldPassThroughToInner()
    {
        // Arrange
        _mockInner.Setup(x => x.GetDimensions()).Returns(768);

        // Act
        var result = _decorator.GetDimensions();

        // Assert
        Assert.That(result, Is.EqualTo(768));
    }
}
