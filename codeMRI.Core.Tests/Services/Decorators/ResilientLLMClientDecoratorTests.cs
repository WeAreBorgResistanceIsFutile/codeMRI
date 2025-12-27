using System.Net;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models.Configuration;
using codeMRI.Core.Services.Decorators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace codeMRI.Core.Tests.Services.Decorators;

[TestFixture]
public class ResilientLLMClientDecoratorTests
{
    private Mock<ILLMClient> _mockInner;
    private Mock<ILogger<ResilientLLMClientDecorator>> _mockLogger;
    private Mock<IOptions<RetrySettings>> _mockSettings;
    private ResilientLLMClientDecorator _decorator;
    private RetrySettings _retrySettings;

    [SetUp]
    public void Setup()
    {
        _mockInner = new Mock<ILLMClient>();
        _mockLogger = new Mock<ILogger<ResilientLLMClientDecorator>>();
        _mockSettings = new Mock<IOptions<RetrySettings>>();
        _retrySettings = new RetrySettings
        {
            MaxRetries = 2,
            BaseDelayMilliseconds = 10,
            MaxDelayMilliseconds = 100
        };
        _mockSettings.Setup(s => s.Value).Returns(_retrySettings);

        _decorator = new ResilientLLMClientDecorator(_mockInner.Object, _mockLogger.Object, _mockSettings.Object);
    }

    [Test]
    public async Task ChatAsync_WhenInnerSucceedsFirstTime_ShouldNotRetry()
    {
        // Arrange
        _mockInner.Setup(x => x.ChatAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Success");

        // Act
        var result = await _decorator.ChatAsync(new List<ChatMessage> { new() { Role = "user", Content = "test" } });

        // Assert
        Assert.That(result, Is.EqualTo("Success"));
        _mockInner.Verify(x => x.ChatAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ChatAsync_WhenTransientError_ShouldRetry()
    {
        // Arrange
        var transientEx = new HttpRequestException("Transient", null, HttpStatusCode.InternalServerError);
        
        _mockInner.SetupSequence(x => x.ChatAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(transientEx)
            .ReturnsAsync("Success");

        // Act
        var result = await _decorator.ChatAsync(new List<ChatMessage> { new() { Role = "user", Content = "test" } });

        // Assert
        Assert.That(result, Is.EqualTo("Success"));
        _mockInner.Verify(x => x.ChatAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public void ChatAsync_WhenTransientErrorPersists_ShouldThrowAfterMaxRetries()
    {
        // Arrange
        var transientEx = new HttpRequestException("Transient", null, HttpStatusCode.ServiceUnavailable);
        
        _mockInner.Setup(x => x.ChatAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(transientEx);

        // Act & Assert
        var ex = Assert.ThrowsAsync<HttpRequestException>(async () => 
            await _decorator.ChatAsync(new List<ChatMessage> { new() { Role = "user", Content = "test" } }));
        
        // Assert we got the last exception
        Assert.That(ex, Is.SameAs(transientEx));
        
        // Should retry MaxRetries (2) + Initial Attempt (1) = 3 calls
        _mockInner.Verify(x => x.ChatAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        
        // Should verify log was called
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Max retries")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public void ChatAsync_WhenNonTransientError_ShouldNotRetry()
    {
        // Arrange
        var nonTransientEx = new InvalidOperationException("Fatal error");
        
        _mockInner.Setup(x => x.ChatAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(nonTransientEx);

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await _decorator.ChatAsync(new List<ChatMessage> { new() { Role = "user", Content = "test" } }));

        // Should call only once
        _mockInner.Verify(x => x.ChatAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
