using System.Net;
using codeMRI.Core.Models;
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
        
        _settingsMock.Setup(s => s.Value).Returns(new ASTServiceSettings 
        { 
            BaseUrl = "http://localhost:3000",
            TimeoutSeconds = 10,
            Enabled = true
        });

        _service = new ASTServiceClient(_httpClient, _loggerMock.Object, _settingsMock.Object);
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
            ""language"": ""csharp"",
            ""filePath"": ""test.cs"",
            ""dependencyGraph"": {
                ""dependencies"": [""System"", ""System.IO""]
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
        var result = await _service.ParseCodeAsync("public class Test {}", "csharp", "test.cs");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Language, Is.EqualTo("csharp"));
        Assert.That(result.FilePath, Is.EqualTo("test.cs"));
        
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
}
