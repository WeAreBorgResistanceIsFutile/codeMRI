using System.Net;
using codeMRI.Infrastructure.Configuration;
using codeMRI.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace codeMRI.Infrastructure.Tests.Services;

[TestFixture]
public class ASTServiceClientTests
{
    private Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private Mock<ILogger<ASTServiceClient>> _loggerMock;
    private Mock<IOptions<ASTServiceSettings>> _settingsMock;
    private Mock<ICSharpParser> _csharpParserMock;
    private HttpClient _httpClient;
    private ASTServiceClient _service;

    [SetUp]
    public void Setup()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:3000")
        };

        _loggerMock = new Mock<ILogger<ASTServiceClient>>();
        _settingsMock = new Mock<IOptions<ASTServiceSettings>>();
        _csharpParserMock = new Mock<ICSharpParser>();

        _settingsMock.Setup(s => s.Value).Returns(new ASTServiceSettings
        {
            BaseUrl = "http://localhost:3000",
            TimeoutSeconds = 10,
            Enabled = true
        });

        _service = new ASTServiceClient(_httpClient, _loggerMock.Object, _settingsMock.Object, _csharpParserMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient.Dispose();
    }

    [Test]
    public async Task ParseCodeAsync_ShouldDeserializeIntoRawDependencyData_AndReturnASTParseResult()
    {
        // Arrange
        var jsonResponse = @"{
            ""language"": ""java"",
            ""filePath"": ""test.java"",
            ""dependencyGraph"": {
                ""dependencies"": [""java.util"", ""java.io""]
            },
            ""tree"": {},
            ""metrics"": {},
            ""entryPoints"": [],
            ""hierarchicalStructure"": {},
            ""crossModuleReferences"": []
        }";

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri.ToString().EndsWith("/api/ast/parse")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            });

        // Act
        var result = await _service.ParseCodeAsync("public class Test {}", "java", "test.java");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Language, Is.EqualTo("java"));
        Assert.That(result.FilePath, Is.EqualTo("test.java"));

        // Verify dependencies were mapped correctly
        Assert.That(result.DependencyGraph, Is.Not.Null);
    }

    [Test]
    public async Task ParseCodeAsync_ShouldHandleNetworkFailure()
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Network Error"));

        var result = await _service.ParseCodeAsync("code", "csharp", "test.cs");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ParseCodeAsync_ShouldSendCorrectParameters_InRequestBody()
    {
        // Arrange
        var code = "public class Foo { }";
        var language = "java";
        var filePath = "src/Foo.java";
        
        // We capture the request to inspect it later
        HttpRequestMessage capturedRequest = null;

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}") // Return empty JSON object to satisfy EnsureSuccessStatusCode and Deserialize
            });

        // Act
        await _service.ParseCodeAsync(code, language, filePath);

        // Assert
        Assert.That(capturedRequest, Is.Not.Null);
        Assert.That(capturedRequest!.Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(capturedRequest.RequestUri!.ToString(), Does.EndWith("/api/ast/parse"));

        var content = await capturedRequest.Content!.ReadAsStringAsync();
        Assert.That(content, Does.Contain($"\"code\":\"{code}\""));
        Assert.That(content, Does.Contain($"\"language\":\"{language}\""));
        Assert.That(content, Does.Contain($"\"filePath\":\"{filePath}\""));
    }

    [Test]
    public async Task ParseCodeAsync_ShouldHandle500Error_ForSpecificPythonContent()
    {
        // Arrange
        var code = @"from .models.configuration import Configuration
from .drivers import SubsonicDriver, SpotifyDriver, DeezerDriver, YouTubeDriver
from .models import Playlist, Track
from .features import TrackMatcher, PlaylistSynchronizer, AsyncTrackMatcher
import logging

logger = logging.getLogger(__name__)

if not logger.hasHandlers():
    handler = logging.StreamHandler()
    formatter = logging.Formatter('%(asctime)s - %(name)s - %(levelname)s - %(message)s')
    handler.setFormatter(formatter)
    logger.addHandler(handler)
    logger.setLevel(logging.INFO)";
        var language = "Python";
        var filePath = "@/Users/levente/AI/tunesynctool/tunesynctool/__init__.py";

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Server Error")
            });

        // Act
        var result = await _service.ParseCodeAsync(code, language, filePath);

        // Assert
        Assert.That(result, Is.Null, "Expected null result when AST Service returns 500");
    }

    [Test]
    public async Task ParseCodeAsync_ShouldUseRoslyn_ForCSharp()
    {
        // Arrange
        var code = "public class Foo {}";
        var language = "csharp";
        var filePath = "test.cs";
        var expectedResult = new codeMRI.Core.Interfaces.ASTParseResult { Language = "C#" };
        
        _csharpParserMock.Setup(p => p.Parse(code, filePath)).Returns(expectedResult);

        // Act
        var result = await _service.ParseCodeAsync(code, language, filePath);

        // Assert
        Assert.That(result, Is.SameAs(expectedResult));
        _csharpParserMock.Verify(p => p.Parse(code, filePath), Times.Once);
        
        // Verify HTTP was NOT called
        _httpMessageHandlerMock.Protected().Verify("SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }
}