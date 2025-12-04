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
public class EdgeCaseTests
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

        _testRepoPath = Path.Combine(Path.GetTempPath(), "test_repo_edge_cases");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testRepoPath))
        {
            try
            {
                Directory.Delete(_testRepoPath, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Test]
    public async Task IngestRepo_WhenEmptyRepository_HandlesGracefully()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        var request = new IngestRequest { RepoPath = _testRepoPath };

        _mockEmbedder.Setup(x => x.EmbedAsync(It.IsAny<string>()))
                     .ReturnsAsync(new float[] { 1, 2, 3 });
        _mockWikiRepo.Setup(x => x.GetIngestionProcessingStateAsync(_testRepoPath))
                     .ReturnsAsync(new IngestionProcessingState());
        _mockWikiRepo.Setup(x => x.GetIngestionManifestAsync(_testRepoPath))
                     .ReturnsAsync(new Dictionary<string, string>());

        // Act
        var result = await _controller.IngestRepo(request);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var responseObject = okResult!.Value;
        var messageProperty = responseObject?.GetType().GetProperty("Message");
        Assert.That(messageProperty, Is.Not.Null);
        var messageValue = messageProperty!.GetValue(responseObject)?.ToString();
        Assert.That(messageValue, Does.Contain("0 new/changed files"));
    }

    [Test]
    public async Task IngestRepo_WhenRepositoryWithOnlyIgnoredFiles_HandlesGracefully()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        
        // Create only ignored files
        Directory.CreateDirectory(Path.Combine(_testRepoPath, ".git"));
        File.WriteAllText(Path.Combine(_testRepoPath, ".git", "config"), "git config");
        Directory.CreateDirectory(Path.Combine(_testRepoPath, "node_modules"));
        File.WriteAllText(Path.Combine(_testRepoPath, "node_modules", "package.json"), "{}");
        Directory.CreateDirectory(Path.Combine(_testRepoPath, "bin"));
        File.WriteAllText(Path.Combine(_testRepoPath, "bin", "test.exe"), "binary content");
        Directory.CreateDirectory(Path.Combine(_testRepoPath, "obj"));
        File.WriteAllText(Path.Combine(_testRepoPath, "obj", "test.dll"), "binary content");

        var request = new IngestRequest { RepoPath = _testRepoPath };

        _mockEmbedder.Setup(x => x.EmbedAsync(It.IsAny<string>()))
                     .ReturnsAsync(new float[] { 1, 2, 3 });
        _mockWikiRepo.Setup(x => x.GetIngestionProcessingStateAsync(_testRepoPath))
                     .ReturnsAsync(new IngestionProcessingState());
        _mockWikiRepo.Setup(x => x.GetIngestionManifestAsync(_testRepoPath))
                     .ReturnsAsync(new Dictionary<string, string>());

        // Act
        var result = await _controller.IngestRepo(request);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var responseObject = okResult!.Value;
        var messageProperty = responseObject?.GetType().GetProperty("Message");
        Assert.That(messageProperty, Is.Not.Null);
        var messageValue = messageProperty!.GetValue(responseObject)?.ToString();
        Assert.That(messageValue, Does.Contain("0 new/changed files"));
    }

    [Test]
    public async Task IngestRepo_WhenRepositoryWithOnlyBinaryFiles_HandlesGracefully()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        
        // Create only binary files (non-text)
        File.WriteAllBytes(Path.Combine(_testRepoPath, "image.png"), new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        File.WriteAllBytes(Path.Combine(_testRepoPath, "document.pdf"), new byte[] { 0x25, 0x50, 0x44, 0x46 });
        File.WriteAllBytes(Path.Combine(_testRepoPath, "archive.zip"), new byte[] { 0x50, 0x4B, 0x03, 0x04 });

        var request = new IngestRequest { RepoPath = _testRepoPath };

        _mockEmbedder.Setup(x => x.EmbedAsync(It.IsAny<string>()))
                     .ReturnsAsync(new float[] { 1, 2, 3 });
        _mockWikiRepo.Setup(x => x.GetIngestionProcessingStateAsync(_testRepoPath))
                     .ReturnsAsync(new IngestionProcessingState());
        _mockWikiRepo.Setup(x => x.GetIngestionManifestAsync(_testRepoPath))
                     .ReturnsAsync(new Dictionary<string, string>());

        // Act
        var result = await _controller.IngestRepo(request);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var responseObject = okResult!.Value;
        var messageProperty = responseObject?.GetType().GetProperty("Message");
        Assert.That(messageProperty, Is.Not.Null);
        var messageValue = messageProperty!.GetValue(responseObject)?.ToString();
        Assert.That(messageValue, Does.Contain("0 new/changed files"));
    }

    [Test]
    public async Task IngestRepo_WhenFilesWithSpecialCharactersInNames_HandlesCorrectly()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        
        // Create files with special characters
        File.WriteAllText(Path.Combine(_testRepoPath, "file with spaces.cs"), "content1");
        File.WriteAllText(Path.Combine(_testRepoPath, "file-with-dashes.py"), "content2");
        File.WriteAllText(Path.Combine(_testRepoPath, "file_with_underscores.js"), "content3");
        File.WriteAllText(Path.Combine(_testRepoPath, "file.with.dots.ts"), "content4");
        File.WriteAllText(Path.Combine(_testRepoPath, "file(with)parentheses.md"), "content5");
        File.WriteAllText(Path.Combine(_testRepoPath, "file[with]brackets.txt"), "content6");

        var request = new IngestRequest { RepoPath = _testRepoPath };

        _mockEmbedder.Setup(x => x.EmbedAsync(It.IsAny<string>()))
                     .ReturnsAsync(new float[] { 1, 2, 3 });
        _mockWikiRepo.Setup(x => x.GetIngestionProcessingStateAsync(_testRepoPath))
                     .ReturnsAsync(new IngestionProcessingState());
        _mockWikiRepo.Setup(x => x.GetIngestionManifestAsync(_testRepoPath))
                     .ReturnsAsync(new Dictionary<string, string>());
        _mockProcessor.Setup(x => x.Split(It.IsAny<Document>(), It.IsAny<int>(), It.IsAny<int>()))
                        .Returns(new List<Document> { new Document { Content = "test" } });

        // Act
        var result = await _controller.IngestRepo(request);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var responseObject = okResult!.Value;
        var messageProperty = responseObject?.GetType().GetProperty("Message");
        Assert.That(messageProperty, Is.Not.Null);
        var messageValue = messageProperty!.GetValue(responseObject)?.ToString();
        Assert.That(messageValue, Does.Contain("6 new/changed files"));
    }

    [Test]
    public async Task IngestRepo_WhenFilesWithUnicodeCharactersInNames_HandlesCorrectly()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        
        // Create files with Unicode characters
        File.WriteAllText(Path.Combine(_testRepoPath, "файл.cs"), "Russian content");
        File.WriteAllText(Path.Combine(_testRepoPath, "文件.py"), "Chinese content");
        File.WriteAllText(Path.Combine(_testRepoPath, "ファイル.js"), "Japanese content");
        File.WriteAllText(Path.Combine(_testRepoPath, "fichier.md"), "French content");

        var request = new IngestRequest { RepoPath = _testRepoPath };

        _mockEmbedder.Setup(x => x.EmbedAsync(It.IsAny<string>()))
                     .ReturnsAsync(new float[] { 1, 2, 3 });
        _mockWikiRepo.Setup(x => x.GetIngestionProcessingStateAsync(_testRepoPath))
                     .ReturnsAsync(new IngestionProcessingState());
        _mockWikiRepo.Setup(x => x.GetIngestionManifestAsync(_testRepoPath))
                     .ReturnsAsync(new Dictionary<string, string>());
        _mockProcessor.Setup(x => x.Split(It.IsAny<Document>(), It.IsAny<int>(), It.IsAny<int>()))
                        .Returns(new List<Document> { new Document { Content = "test" } });

        // Act
        var result = await _controller.IngestRepo(request);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var responseObject = okResult!.Value;
        var messageProperty = responseObject?.GetType().GetProperty("Message");
        Assert.That(messageProperty, Is.Not.Null);
        var messageValue = messageProperty!.GetValue(responseObject)?.ToString();
        Assert.That(messageValue, Does.Contain("4 new/changed files"));
    }

    [Test]
    public async Task IngestRepo_WhenVeryLongFilePaths_HandlesCorrectly()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        
        // Create deeply nested directory structure
        var deepPath = _testRepoPath;
        for (int i = 0; i < 10; i++)
        {
            deepPath = Path.Combine(deepPath, $"very_long_directory_name_{i}");
            Directory.CreateDirectory(deepPath);
        }
        
        var longFilePath = Path.Combine(deepPath, "very_long_file_name.cs");
        File.WriteAllText(longFilePath, "content in deep path");

        var request = new IngestRequest { RepoPath = _testRepoPath };

        _mockEmbedder.Setup(x => x.EmbedAsync(It.IsAny<string>()))
                     .ReturnsAsync(new float[] { 1, 2, 3 });
        _mockWikiRepo.Setup(x => x.GetIngestionProcessingStateAsync(_testRepoPath))
                     .ReturnsAsync(new IngestionProcessingState());
        _mockWikiRepo.Setup(x => x.GetIngestionManifestAsync(_testRepoPath))
                     .ReturnsAsync(new Dictionary<string, string>());
        _mockProcessor.Setup(x => x.Split(It.IsAny<Document>(), It.IsAny<int>(), It.IsAny<int>()))
                        .Returns(new List<Document> { new Document { Content = "test" } });

        // Act
        var result = await _controller.IngestRepo(request);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var responseObject = okResult!.Value;
        var messageProperty = responseObject?.GetType().GetProperty("Message");
        Assert.That(messageProperty, Is.Not.Null);
        var messageValue = messageProperty!.GetValue(responseObject)?.ToString();
        Assert.That(messageValue, Does.Contain("1 new/changed files"));
        // Note: The controller doesn't return error count in the message for this case
    }

    [Test]
    public async Task IngestRepo_WhenRepositoryWithMixedFileTypes_ProcessesOnlyTextFiles()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        
        // Create text files
        File.WriteAllText(Path.Combine(_testRepoPath, "code.cs"), "C# code");
        File.WriteAllText(Path.Combine(_testRepoPath, "script.py"), "Python script");
        File.WriteAllText(Path.Combine(_testRepoPath, "document.md"), "Markdown document");
        
        // Create binary files
        File.WriteAllBytes(Path.Combine(_testRepoPath, "image.png"), new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        File.WriteAllBytes(Path.Combine(_testRepoPath, "data.dat"), new byte[] { 0x01, 0x02, 0x03, 0x04 });

        var request = new IngestRequest { RepoPath = _testRepoPath };

        _mockEmbedder.Setup(x => x.EmbedAsync(It.IsAny<string>()))
                     .ReturnsAsync(new float[] { 1, 2, 3 });
        _mockWikiRepo.Setup(x => x.GetIngestionProcessingStateAsync(_testRepoPath))
                     .ReturnsAsync(new IngestionProcessingState());
        _mockWikiRepo.Setup(x => x.GetIngestionManifestAsync(_testRepoPath))
                     .ReturnsAsync(new Dictionary<string, string>());
        _mockProcessor.Setup(x => x.Split(It.IsAny<Document>(), It.IsAny<int>(), It.IsAny<int>()))
                        .Returns(new List<Document> { new Document { Content = "test" } });

        // Act
        var result = await _controller.IngestRepo(request);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var responseObject = okResult!.Value;
        var messageProperty = responseObject?.GetType().GetProperty("Message");
        Assert.That(messageProperty, Is.Not.Null);
        var messageValue = messageProperty!.GetValue(responseObject)?.ToString();
        Assert.That(messageValue, Does.Contain("3 new/changed files"));
    }

    [Test]
    public async Task IngestRepo_WhenRepositoryWithHiddenDirectories_SkipsHiddenDirectories()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        
        // Create normal directory with files
        Directory.CreateDirectory(Path.Combine(_testRepoPath, "normal"));
        File.WriteAllText(Path.Combine(_testRepoPath, "normal", "file.cs"), "normal content");
        
        // Create hidden directories with files
        Directory.CreateDirectory(Path.Combine(_testRepoPath, ".hidden"));
        File.WriteAllText(Path.Combine(_testRepoPath, ".hidden", "file.cs"), "hidden content");
        Directory.CreateDirectory(Path.Combine(_testRepoPath, ".vs"));
        File.WriteAllText(Path.Combine(_testRepoPath, ".vs", "settings.json"), "{}");
        Directory.CreateDirectory(Path.Combine(_testRepoPath, ".idea"));
        File.WriteAllText(Path.Combine(_testRepoPath, ".idea", "config.xml"), "<config></config>");

        var request = new IngestRequest { RepoPath = _testRepoPath };

        _mockEmbedder.Setup(x => x.EmbedAsync(It.IsAny<string>()))
                     .ReturnsAsync(new float[] { 1, 2, 3 });
        _mockWikiRepo.Setup(x => x.GetIngestionProcessingStateAsync(_testRepoPath))
                     .ReturnsAsync(new IngestionProcessingState());
        _mockWikiRepo.Setup(x => x.GetIngestionManifestAsync(_testRepoPath))
                     .ReturnsAsync(new Dictionary<string, string>());
        _mockProcessor.Setup(x => x.Split(It.IsAny<Document>(), It.IsAny<int>(), It.IsAny<int>()))
                        .Returns(new List<Document> { new Document { Content = "test" } });

        // Act
        var result = await _controller.IngestRepo(request);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var responseObject = okResult!.Value;
        var messageProperty = responseObject?.GetType().GetProperty("Message");
        Assert.That(messageProperty, Is.Not.Null);
        var messageValue = messageProperty!.GetValue(responseObject)?.ToString();
        Assert.That(messageValue, Does.Contain("1 new/changed files"));
    }

    [Test]
    public async Task IngestRepo_WhenRepositoryWithEmptyFiles_HandlesGracefully()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        
        // Create empty files
        File.WriteAllText(Path.Combine(_testRepoPath, "empty.cs"), "");
        File.WriteAllText(Path.Combine(_testRepoPath, "empty.py"), "");
        
        // Create non-empty file
        File.WriteAllText(Path.Combine(_testRepoPath, "content.js"), "console.log('hello');");

        var request = new IngestRequest { RepoPath = _testRepoPath };

        _mockEmbedder.Setup(x => x.EmbedAsync(It.IsAny<string>()))
                     .ReturnsAsync(new float[] { 1, 2, 3 });
        _mockWikiRepo.Setup(x => x.GetIngestionProcessingStateAsync(_testRepoPath))
                     .ReturnsAsync(new IngestionProcessingState());
        _mockWikiRepo.Setup(x => x.GetIngestionManifestAsync(_testRepoPath))
                     .ReturnsAsync(new Dictionary<string, string>());
        _mockProcessor.Setup(x => x.Split(It.IsAny<Document>(), It.IsAny<int>(), It.IsAny<int>()))
                        .Returns(new List<Document> { new Document { Content = "test" } });

        // Act
        var result = await _controller.IngestRepo(request);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var responseObject = okResult!.Value;
        var messageProperty = responseObject?.GetType().GetProperty("Message");
        Assert.That(messageProperty, Is.Not.Null);
        var messageValue = messageProperty!.GetValue(responseObject)?.ToString();
        Assert.That(messageValue, Does.Contain("3 new/changed files"));
    }
}
