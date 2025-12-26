using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Server.Controllers;
using codeMRI.Server.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using ProgressInfo = codeMRI.Core.Models.ProgressInfo;
using WikiPage = codeMRI.Core.Models.WikiPage;
using WikiStructure = codeMRI.Core.Models.WikiStructure;

namespace codeMRI.Core.Tests.Controllers;

[TestFixture]
public class WikiControllerTests
{
    [SetUp]
    public void Setup()
    {
        _mockWikiService = new Mock<IWikiGenerationService>();
        _mockWikiRepo = new Mock<IWikiRepository>();
        _mockOrchestrator = new Mock<ICodeWikiOrchestrator>();
        _mockHubContext = new Mock<IHubContext<WikiHub>>();
        _mockLogger = new Mock<ILogger<WikiController>>();
        _mockMessageBus = new Mock<AgentMessageBus>(Mock.Of<ILogger<AgentMessageBus>>());
        _mockTelemetryService = new Mock<IAgentTelemetryService>();
        _mockIngestionManager = new Mock<IIngestionJobManager>();
        _mockGenerationManager = new Mock<IGenerationJobManager>();
        _controller = new WikiController(
            _mockWikiService.Object,
            _mockWikiRepo.Object,
            _mockOrchestrator.Object,
            _mockHubContext.Object,
            _mockLogger.Object,
            _mockMessageBus.Object,
            _mockTelemetryService.Object,
            _mockIngestionManager.Object,
            _mockGenerationManager.Object);
    }

    private Mock<IWikiGenerationService> _mockWikiService;
    private Mock<IWikiRepository> _mockWikiRepo;
    private Mock<ICodeWikiOrchestrator> _mockOrchestrator;
    private Mock<IHubContext<WikiHub>> _mockHubContext;
    private Mock<ILogger<WikiController>> _mockLogger;
    private Mock<AgentMessageBus> _mockMessageBus;
    private Mock<IAgentTelemetryService> _mockTelemetryService;
    private Mock<IIngestionJobManager> _mockIngestionManager;
    private Mock<IGenerationJobManager> _mockGenerationManager;
    private WikiController _controller;

    [Test]
    public async Task GeneratePage_WhenPageExistsAndNotForced_ReturnsCachedPage()
    {
        // Arrange
        var request = new PageGenerationRequest
        {
            RepoPath = "/test/repo",
            Title = "Existing Page",
            ForceRegenerate = false
        };

        var cachedPage = new WikiPage { Title = "Existing Page", Content = "Cached Content" };

        _mockWikiRepo.Setup(x => x.GetPageByTitleAsync(request.RepoPath, request.Title))
            .ReturnsAsync(cachedPage);

        // Act
        var result = await _controller.GeneratePage(request);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var page = okResult!.Value as WikiPage;

        Assert.That(page, Is.Not.Null);
        Assert.That(page!.Content, Is.EqualTo("Cached Content"));

        // Verify service was NOT called
        // Verify service was NOT called
        _mockWikiService.Verify(
            x => x.GeneratePageAsync(It.IsAny<string>(), It.IsAny<List<string>>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Never);
    }

    [Test]
    public async Task GeneratePage_WhenPageExistsButForced_RegeneratesPage()
    {
        // Arrange
        var request = new PageGenerationRequest
        {
            RepoPath = "/test/repo",
            Title = "Existing Page",
            ForceRegenerate = true
        };

        var newPage = new WikiPage { Title = "Existing Page", Content = "New Content" };

        _mockWikiService.Setup(x => x.GeneratePageAsync(It.IsAny<string>(), It.IsAny<List<string>>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(newPage);

        // Act
        var result = await _controller.GeneratePage(request);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var page = okResult!.Value as WikiPage;

        Assert.That(page!.Content, Is.EqualTo("New Content"));

        // Verify service WAS called
        // Verify service WAS called
        _mockWikiService.Verify(
            x => x.GeneratePageAsync(It.IsAny<string>(), It.IsAny<List<string>>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Once);

        // Verify save was called
        _mockWikiRepo.Verify(x => x.SavePageAsync(request.RepoPath, newPage), Times.Once);
    }

    [Test]
    public async Task GeneratePage_WhenSourceChangesButNotForced_ReturnsStaleCachedPage()
    {
        // Arrange
        var request = new PageGenerationRequest
        {
            RepoPath = "/test/repo",
            Title = "Existing Page",
            ForceRegenerate = false,
            FileContents = new Dictionary<string, string> { { "file1.cs", "New Code" } }
        };

        var cachedPage = new WikiPage { Title = "Existing Page", Content = "Old Content based on Old Code" };

        _mockWikiRepo.Setup(x => x.GetPageByTitleAsync(request.RepoPath, request.Title))
            .ReturnsAsync(cachedPage);

        // Act
        var result = await _controller.GeneratePage(request);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var page = okResult!.Value as WikiPage;

        // This confirms that it returns the OLD content even though we passed NEW code in the request
        Assert.That(page!.Content, Is.EqualTo("Old Content based on Old Code"));

        // Verify service was NOT called
        // Verify service was NOT called
        _mockWikiService.Verify(
            x => x.GeneratePageAsync(It.IsAny<string>(), It.IsAny<List<string>>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Never);
    }

    [Test]
    public async Task GenerateAdvancedWiki_DelegatesToOrchestrator()
    {
        // Arrange
        var request = new StructureRequest { RepoPath = "/test/repo", Language = "C#" };
        var expectedStructure = new WikiStructure { Title = "Advanced Wiki" };

        _mockOrchestrator.Setup(x => x.GenerateAdvancedWikiAsync(request.RepoPath, It.IsAny<RepositoryInfo>(),
                It.IsAny<IProgress<ProgressInfo>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStructure);

        // Act
        var result = await _controller.GenerateAdvancedWiki(request);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var structure = okResult!.Value as WikiStructure;

        Assert.That(structure, Is.Not.Null);
        Assert.That(structure.Title, Is.EqualTo("Advanced Wiki"));

        // Verify Orchestrator was called
        _mockOrchestrator.Verify(
            x => x.GenerateAdvancedWikiAsync(request.RepoPath, It.IsAny<RepositoryInfo>(),
                It.IsAny<IProgress<ProgressInfo>?>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify Save was called
        _mockWikiRepo.Verify(x => x.SaveStructureAsync(request.RepoPath, expectedStructure), Times.Once);
    }
}