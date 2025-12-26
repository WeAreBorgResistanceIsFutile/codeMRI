using System.Net;
using System.Net.Http.Json;
using codeMRI.Infrastructure.Configuration;
using codeMRI.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace codeMRI.Infrastructure.Tests.Services;

[TestFixture]
public class OllamaEmbeddingServiceTests
{
    private Mock<ILogger<OllamaEmbeddingService>> _mockLogger = null!;
    private OllamaSettings _ollamaSettings = null!;
    private EmbeddingSettings _embeddingSettings = null!;

    [SetUp]
    public void SetUp()
    {
        _mockLogger = new Mock<ILogger<OllamaEmbeddingService>>();
        _ollamaSettings = new OllamaSettings
        {
            BaseUrl = "http://localhost:11434"
        };
        _embeddingSettings = new EmbeddingSettings
        {
            Model = "nomic-embed-text"
        };
    }

    [Test]
    public async Task GetEmbeddingAsync_WhenOllamaReturns500_ShouldThrowException()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Internal Server Error")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri(_ollamaSettings.BaseUrl)
        };

        var service = new OllamaEmbeddingService(
            httpClient,
            Options.Create(_ollamaSettings),
            Options.Create(_embeddingSettings),
            _mockLogger.Object);

        // Act & Assert
        var exception = Assert.ThrowsAsync<HttpRequestException>(
            async () => await service.GetEmbeddingAsync("test text"));
        
        Assert.That(exception!.Message, Does.Contain("500"));
    }

    [Test]
    public async Task GetEmbeddingAsync_WhenOllamaSucceeds_ShouldReturnValidEmbedding()
    {
        // Arrange
        var expectedEmbedding = new float[768];
        for (int i = 0; i < 768; i++)
        {
            expectedEmbedding[i] = 0.1f * i;
        }

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(new { embedding = expectedEmbedding })
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri(_ollamaSettings.BaseUrl)
        };

        var service = new OllamaEmbeddingService(
            httpClient,
            Options.Create(_ollamaSettings),
            Options.Create(_embeddingSettings),
            _mockLogger.Object);

        // Act
        var result = await service.GetEmbeddingAsync("test text");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(768));
        Assert.That(result, Is.EqualTo(expectedEmbedding));
    }

    [Test]
    public void GetDimensions_ShouldReturn768()
    {
        // Arrange
        var httpClient = new HttpClient();
        var service = new OllamaEmbeddingService(
            httpClient,
            Options.Create(_ollamaSettings),
            Options.Create(_embeddingSettings),
            _mockLogger.Object);

        // Act
        var dimensions = service.GetDimensions();

        // Assert
        Assert.That(dimensions, Is.EqualTo(768));
    }

    [Test]
    public async Task GetEmbeddingAsync_WhenOllamaReturns500WithErrorBody_ShouldThrowExceptionWithErrorDetails()
    {
        // Arrange
        var errorMessage = "{\"error\":\"model 'nomic-embed-text' not found\"}";
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent(errorMessage)
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri(_ollamaSettings.BaseUrl)
        };

        var service = new OllamaEmbeddingService(
            httpClient,
            Options.Create(_ollamaSettings),
            Options.Create(_embeddingSettings),
            _mockLogger.Object);

        // Act & Assert
        var exception = Assert.ThrowsAsync<HttpRequestException>(
            async () => await service.GetEmbeddingAsync("test text"));
        
        Assert.That(exception!.Message, Does.Contain("500"));
        Assert.That(exception.Message, Does.Contain("model 'nomic-embed-text' not found"));
    }
}
