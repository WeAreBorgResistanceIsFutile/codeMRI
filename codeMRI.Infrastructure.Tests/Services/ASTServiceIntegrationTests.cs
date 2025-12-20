using System.Diagnostics;
using System.Text.Json;
using codeMRI.Infrastructure.Configuration;
using codeMRI.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
// Added this

namespace codeMRI.Infrastructure.Tests.Services;

[TestFixture]
public class ASTServiceIntegrationTests
{
    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        // 1. Start the AST Service (Node.js application)
        var astServicePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..",
            "codeMRI.ASTService");
        astServicePath = Path.GetFullPath(astServicePath);

        Console.WriteLine($"Starting AST Service from: {astServicePath}");

        _astServiceProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = "src/app.js",
                WorkingDirectory = astServicePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        _astServiceProcess.OutputDataReceived += (sender, args) => Console.WriteLine($"AST_OUT: {args.Data}");
        _astServiceProcess.ErrorDataReceived += (sender, args) => Console.Error.WriteLine($"AST_ERR: {args.Data}");

        _astServiceProcess.Start();
        _astServiceProcess.BeginOutputReadLine();
        _astServiceProcess.BeginErrorReadLine();

        // 2. Wait for the service to become healthy
        _httpClient = new HttpClient();
        _loggerMock = new Mock<ILogger<ASTServiceClient>>();
        _settingsMock = new Mock<IOptions<ASTServiceSettings>>();
        var csharpParserMock = new Mock<ICSharpParser>();

        _settingsMock.Setup(s => s.Value).Returns(new ASTServiceSettings
        {
            BaseUrl = AstServiceBaseUrl,
            TimeoutSeconds = 60, // Increased timeout for integration tests
            Enabled = true
        });

        _service = new ASTServiceClient(_httpClient, _loggerMock.Object, _settingsMock.Object, csharpParserMock.Object);

        Console.WriteLine("Waiting for AST Service to become healthy...");
        var isServiceHealthy = false;
        var attempts = 0;
        while (!isServiceHealthy && attempts < 20) // Max 20 attempts, 1s delay each
        {
            try
            {
                isServiceHealthy = await _service.IsHealthyAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Health check failed: {ex.Message}. Retrying...");
            }

            if (!isServiceHealthy)
            {
                await Task.Delay(1000);
                attempts++;
            }
        }

        if (!isServiceHealthy) throw new Exception("AST Service did not become healthy within the expected time.");
        Console.WriteLine("AST Service is healthy.");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Console.WriteLine("Shutting down AST Service...");
        _astServiceProcess?.Kill(true); // Terminate the process and its children
        _astServiceProcess?.Dispose();
        _httpClient.Dispose();
        Console.WriteLine("AST Service shut down.");
    }

    private const string AstServiceBaseUrl = "http://localhost:3002";
    private Process? _astServiceProcess;
    private HttpClient _httpClient = null!;
    private ASTServiceClient _service = null!;
    private Mock<ILogger<ASTServiceClient>> _loggerMock = null!;
    private Mock<IOptions<ASTServiceSettings>> _settingsMock = null!;

    [Test]
    public async Task ParseCodeAsync_WithSpecificPythonContent_ShouldReturnSuccess()
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
        var filePath = "/Users/levente/AI/tunesynctool/tunesynctool/__init__.py"; // Original user path

        // Act
        var result = await _service.ParseCodeAsync(code, language, filePath);

        // Assert
        Assert.That(result, Is.Not.Null, "Expected successful parse result from AST Service");
        Assert.That(result!.Language, Is.EqualTo("Python"), "Expected parsed language to be Python");
        Assert.That(result.FilePath, Is.EqualTo(filePath), "Expected parsed file path to match");
        Assert.That(result.Tree, Is.Not.Null, "Expected AST tree to be present");
        Assert.That(result.Metrics, Is.Not.Null, "Expected metrics to be present");

        // Access metrics properties using JsonElement
        var metricsElement = (JsonElement)result.Metrics;
        Assert.That(metricsElement.GetProperty("linesOfCode").GetInt32(), Is.GreaterThan(0),
            "Expected lines of code metric to be greater than 0");
        Assert.That(metricsElement.GetProperty("cyclomaticComplexity").GetInt32(), Is.GreaterThanOrEqualTo(1),
            "Expected cyclomatic complexity to be at least 1");

        Assert.That(result.DependencyGraph, Is.Not.Null, "Expected dependency graph to be present");
        Assert.That(result.HierarchicalStructure, Is.Not.Null, "Expected hierarchical structure to be present");
    }

    [Test]
    public async Task ParseCodeAsync_WithInvalidLanguage_ShouldThrowErrorHandledByClient()
    {
        // Arrange
        var code = "some invalid code";
        var language = "invalid_lang";
        var filePath = "test.txt";

        // Act
        var result = await _service.ParseCodeAsync(code, language, filePath);

        // Assert
        Assert.That(result, Is.Null, "Expected null result for unsupported language handled gracefully by client");
        // Verify a warning/error was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) =>
                    o.ToString()!.Contains($"Failed to parse code using AST Service for language: {language}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }
}