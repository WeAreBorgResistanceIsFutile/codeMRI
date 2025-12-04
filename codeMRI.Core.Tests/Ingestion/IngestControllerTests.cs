using System.Security.Cryptography;
using System.Text;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Server.Api;
using codeMRI.Server.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace codeMRI.Core.Tests.Ingestion;

[TestFixture]
public class IngestControllerTests
{
    private Mock<IDocumentProcessor> _mockProcessor;
    private Mock<IEmbedder> _mockEmbedder;
    private Mock<IVectorDatabase> _mockVectorDb;
    private Mock<IWikiRepository> _mockWikiRepo;
    private Mock<ILogger<IngestController>> _mockLogger;
    private IngestController _controller;
    private string _testRepoPath;

    [SetUp]
    public void Setup()
    {
        _mockProcessor = new Mock<IDocumentProcessor>();
        _mockEmbedder = new Mock<IEmbedder>();
        _mockVectorDb = new Mock<IVectorDatabase>();
        _mockWikiRepo = new Mock<IWikiRepository>();
        _mockLogger = new Mock<ILogger<IngestController>>();

        _controller = new IngestController(
            _mockProcessor.Object,
            _mockEmbedder.Object,
            _mockVectorDb.Object,
            _mockWikiRepo.Object,
            _mockLogger.Object
        );

        _testRepoPath = Path.Combine(Path.GetTempPath(), "test_repo");
    }

    [Test]
    public async Task IngestRepo_WhenDeleteRequested_DeletesAllData()
    {
        // Arrange
        var request = new IngestRequest { RepoPath = _testRepoPath, Delete = true };

        // Act
        var result = await _controller.IngestRepo(request);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        _mockVectorDb.Verify(x => x.DeleteByMetadataAsync("repo_path", _testRepoPath), Times.Once);
        _mockWikiRepo.Verify(x => x.DeleteIngestionManifestAsync(_testRepoPath), Times.Once);
        _mockWikiRepo.Verify(x => x.DeleteIngestionProcessingStateAsync(_testRepoPath), Times.Once);
    }

    [Test]
    public async Task IngestRepo_WhenDirectoryNotFound_ReturnsBadRequest()
    {
        // Arrange
        var request = new IngestRequest { RepoPath = "/nonexistent/path" };

        // Act
        var result = await _controller.IngestRepo(request);

        // Assert
        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        var badRequest = result as BadRequestObjectResult;
        Assert.That(badRequest!.Value, Does.Contain("Directory not found"));
    }

    [Test]
    public async Task IngestRepo_WhenOllamaConnectionFails_ReturnsServerError()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        var request = new IngestRequest { RepoPath = _testRepoPath };
        _mockEmbedder.Setup(x => x.EmbedAsync(It.IsAny<string>()))
                     .ThrowsAsync(new Exception("Connection failed"));

        // Act
        var result = await _controller.IngestRepo(request);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var errorResult = result as ObjectResult;
        Assert.That(errorResult!.StatusCode, Is.EqualTo(500));
        Assert.That(errorResult!.Value!.ToString(), Does.Contain("Ollama Error"));

        // Cleanup
        Directory.Delete(_testRepoPath, true);
    }

    [Test]
    public async Task IngestRepo_WhenProcessingInProgressAndNotStale_ReturnsStatus()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        var request = new IngestRequest { RepoPath = _testRepoPath };
        
        var existingState = new IngestionProcessingState
        {
            CurrentStatus = "processing",
            LastCheckpoint = DateTime.UtcNow.AddMinutes(-2), // Not stale
            CompletedFiles = new List<string> { "file1.cs" },
            TotalFiles = 2
        };

        _mockEmbedder.Setup(x => x.EmbedAsync(It.IsAny<string>()))
                     .ReturnsAsync(new float[] { 1, 2, 3 });
        _mockWikiRepo.Setup(x => x.GetIngestionProcessingStateAsync(_testRepoPath))
                     .ReturnsAsync(existingState);

        // Act
        var result = await _controller.IngestRepo(request);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var responseObject = okResult!.Value;

        var messageProperty = responseObject?.GetType().GetProperty("Message");
        Assert.That(messageProperty, Is.Not.Null);
        var messageValue = messageProperty!.GetValue(responseObject)?.ToString();
        Assert.That(messageValue, Is.EqualTo("Ingestion is already in progress"));

        var statusProperty = responseObject?.GetType().GetProperty("Status");
        Assert.That(statusProperty, Is.Not.Null);
        var statusValue = statusProperty!.GetValue(responseObject)?.ToString();
        Assert.That(statusValue, Is.EqualTo("processing"));

        // Cleanup
        Directory.Delete(_testRepoPath, true);
    }

    [Test]
    public async Task IngestRepo_WhenForceRequested_ClearsExistingData()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        var request = new IngestRequest { RepoPath = _testRepoPath, Force = true };
        
        _mockEmbedder.Setup(x => x.EmbedAsync(It.IsAny<string>()))
                     .ReturnsAsync(new float[] { 1, 2, 3 });
        _mockWikiRepo.Setup(x => x.GetIngestionProcessingStateAsync(_testRepoPath))
                     .ReturnsAsync(new IngestionProcessingState());
        _mockWikiRepo.Setup(x => x.GetIngestionManifestAsync(_testRepoPath))
                     .ReturnsAsync(new Dictionary<string, string>());

        // Act
        var result = await _controller.IngestRepo(request);

        // Assert
        _mockVectorDb.Verify(x => x.DeleteByMetadataAsync("repo_path", _testRepoPath), Times.Once);
        _mockWikiRepo.Verify(x => x.DeleteIngestionManifestAsync(_testRepoPath), Times.Once);
        _mockWikiRepo.Verify(x => x.DeleteIngestionProcessingStateAsync(_testRepoPath), Times.Once);

        // Cleanup
        Directory.Delete(_testRepoPath, true);
    }

    [Test]
    public async Task GetIngestionStatus_WhenRepoPathEmpty_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetIngestionStatus("");

        // Assert
        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task GetIngestionStatus_WhenDirectoryNotFound_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetIngestionStatus("/nonexistent/path");

        // Assert
        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task GetIngestionStatus_WhenProcessingStateExists_ReturnsCorrectStatus()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        
        var processingState = new IngestionProcessingState
        {
            CurrentStatus = "processing",
            TotalFiles = 10,
            CompletedFiles = new List<string> { "file1.cs", "file2.cs" },
            FailedFiles = new List<string> { "file3.cs" },
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            LastCheckpoint = DateTime.UtcNow.AddMinutes(-1),
            Errors = new Dictionary<string, string>
            {
                ["file3.cs"] = "Access denied"
            }
        };

        _mockWikiRepo.Setup(x => x.GetIngestionProcessingStateAsync(_testRepoPath))
                     .ReturnsAsync(processingState);
        _mockWikiRepo.Setup(x => x.GetIngestionManifestAsync(_testRepoPath))
                     .ReturnsAsync(new Dictionary<string, string>());

        // Act
        var result = await _controller.GetIngestionStatus(_testRepoPath);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var response = okResult!.Value as IngestionStatusResponse;
        
        Assert.That(response!.Status, Is.EqualTo("processing"));
        Assert.That(response.TotalFiles, Is.EqualTo(10));
        Assert.That(response.ProcessedFiles, Is.EqualTo(2));
        Assert.That(response.FailedFiles, Is.EqualTo(1));
        Assert.That(response.ProgressPercentage, Is.EqualTo(20.0));
        Assert.That(response.CanResume, Is.False); // Not stale yet

        // Cleanup
        Directory.Delete(_testRepoPath, true);
    }

    [Test]
    public async Task GetIngestionStatus_WhenProcessIsStale_CanResumeIsTrue()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        
        var processingState = new IngestionProcessingState
        {
            CurrentStatus = "processing",
            TotalFiles = 10,
            CompletedFiles = new List<string> { "file1.cs" },
            LastCheckpoint = DateTime.UtcNow.AddMinutes(-10) // Stale
        };

        _mockWikiRepo.Setup(x => x.GetIngestionProcessingStateAsync(_testRepoPath))
                     .ReturnsAsync(processingState);
        _mockWikiRepo.Setup(x => x.GetIngestionManifestAsync(_testRepoPath))
                     .ReturnsAsync(new Dictionary<string, string>());

        // Act
        var result = await _controller.GetIngestionStatus(_testRepoPath);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var response = okResult!.Value as IngestionStatusResponse;
        
        Assert.That(response!.CanResume, Is.True);

        // Cleanup
        Directory.Delete(_testRepoPath, true);
    }

    [Test]
    public async Task ResumeIngestion_WhenRepoPathEmpty_ReturnsBadRequest()
    {
        // Arrange
        var request = new ResumeIngestionRequest { RepoPath = "" };

        // Act
        var result = await _controller.ResumeIngestion(request);

        // Assert
        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task ResumeIngestion_WhenDirectoryNotFound_ReturnsBadRequest()
    {
        // Arrange
        var request = new ResumeIngestionRequest { RepoPath = "/nonexistent/path" };

        // Act
        var result = await _controller.ResumeIngestion(request);

        // Assert
        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task ResumeIngestion_WhenNoInterruptedProcess_ReturnsBadRequest()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        var request = new ResumeIngestionRequest { RepoPath = _testRepoPath };
        
        var processingState = new IngestionProcessingState
        {
            CurrentStatus = "completed",
            LastCheckpoint = DateTime.UtcNow.AddMinutes(-1)
        };

        _mockEmbedder.Setup(x => x.EmbedAsync(It.IsAny<string>()))
                     .ReturnsAsync(new float[] { 1, 2, 3 });
        _mockWikiRepo.Setup(x => x.GetIngestionProcessingStateAsync(_testRepoPath))
                     .ReturnsAsync(processingState);

        // Act
        var result = await _controller.ResumeIngestion(request);

        // Assert
        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        var badRequest = result as BadRequestObjectResult;
        Assert.That(badRequest!.Value, Is.EqualTo("No interrupted ingestion process found to resume"));

        // Cleanup
        Directory.Delete(_testRepoPath, true);
    }

    [Test]
    public async Task ResumeIngestion_WhenPausedProcess_ResumesSuccessfully()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        var request = new ResumeIngestionRequest { RepoPath = _testRepoPath };
        
        var processingState = new IngestionProcessingState
        {
            CurrentStatus = "paused",
            ProcessingQueue = new List<string> { "file1.cs" },
            TotalFiles = 1,
            CurrentBatchSize = 50
        };

        _mockEmbedder.Setup(x => x.EmbedAsync(It.IsAny<string>()))
                     .ReturnsAsync(new float[] { 1, 2, 3 });
        _mockWikiRepo.Setup(x => x.GetIngestionProcessingStateAsync(_testRepoPath))
                     .ReturnsAsync(processingState);
        _mockWikiRepo.Setup(x => x.GetIngestionManifestAsync(_testRepoPath))
                     .ReturnsAsync(new Dictionary<string, string>());
        _mockProcessor.Setup(x => x.Split(It.IsAny<Document>(), It.IsAny<int>(), It.IsAny<int>()))
                       .Returns(new List<Document> { new Document { Content = "test" } });

        // Act
        var result = await _controller.ResumeIngestion(request);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var responseObject = okResult!.Value;

        var messageProperty = responseObject?.GetType().GetProperty("Message");
        Assert.That(messageProperty, Is.Not.Null);
        var messageValue = messageProperty!.GetValue(responseObject)?.ToString();
        Assert.That(messageValue, Does.Contain("Resumed ingestion"));

        // Verify state was updated (status should be processing or completed)
        _mockWikiRepo.Verify(x => x.SaveIngestionProcessingStateAsync(
            _testRepoPath,
            It.Is<IngestionProcessingState>(s => s.CurrentStatus == "processing" || s.CurrentStatus == "completed")),
            Times.AtLeastOnce);

        // Cleanup
        Directory.Delete(_testRepoPath, true);
    }

    [Test]
    public async Task ResumeIngestion_WhenBatchSizeProvided_UpdatesBatchSize()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        var request = new ResumeIngestionRequest { RepoPath = _testRepoPath, BatchSize = 25 };
        
        var processingState = new IngestionProcessingState
        {
            CurrentStatus = "error",
            ProcessingQueue = new List<string> { "file1.cs" },
            TotalFiles = 1,
            CurrentBatchSize = 50
        };

        _mockEmbedder.Setup(x => x.EmbedAsync(It.IsAny<string>()))
                     .ReturnsAsync(new float[] { 1, 2, 3 });
        _mockWikiRepo.Setup(x => x.GetIngestionProcessingStateAsync(_testRepoPath))
                     .ReturnsAsync(processingState);
        _mockWikiRepo.Setup(x => x.GetIngestionManifestAsync(_testRepoPath))
                     .ReturnsAsync(new Dictionary<string, string>());
        _mockProcessor.Setup(x => x.Split(It.IsAny<Document>(), It.IsAny<int>(), It.IsAny<int>()))
                       .Returns(new List<Document> { new Document { Content = "test" } });

        // Act
        var result = await _controller.ResumeIngestion(request);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        
        // Verify batch size was updated
        _mockWikiRepo.Verify(x => x.SaveIngestionProcessingStateAsync(
            _testRepoPath, 
            It.Is<IngestionProcessingState>(s => s.CurrentBatchSize == 25)), Times.AtLeastOnce);

        // Cleanup
        Directory.Delete(_testRepoPath, true);
    }

    [Test]
    public async Task ResumeIngestion_WhenOllamaConnectionFails_ReturnsServerError()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        var request = new ResumeIngestionRequest { RepoPath = _testRepoPath };
        
        var processingState = new IngestionProcessingState
        {
            CurrentStatus = "paused"
        };

        _mockEmbedder.Setup(x => x.EmbedAsync(It.IsAny<string>()))
                     .ThrowsAsync(new Exception("Connection failed"));
        _mockWikiRepo.Setup(x => x.GetIngestionProcessingStateAsync(_testRepoPath))
                     .ReturnsAsync(processingState);

        // Act
        var result = await _controller.ResumeIngestion(request);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var errorResult = result as ObjectResult;
        Assert.That(errorResult!.StatusCode, Is.EqualTo(500));
        Assert.That(errorResult!.Value!.ToString(), Does.Contain("Error resuming ingestion"));

        // Cleanup
        Directory.Delete(_testRepoPath, true);
    }
}
