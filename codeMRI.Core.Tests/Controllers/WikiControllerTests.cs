using codeMRI.Core.Interfaces;
using codeMRI.Server.Api;
using codeMRI.Server.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Microsoft.Extensions.Logging;
using codeMRI.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace codeMRI.Core.Tests.Controllers;

[TestFixture]
public class WikiControllerTests
{
    private Mock<IWikiGenerationService> _mockWikiService;
    private Mock<IWikiRepository> _mockWikiRepo;
    private Mock<ICodeWikiOrchestrator> _mockOrchestrator;
    private Mock<IHubContext<WikiHub>> _mockHubContext;
    private Mock<ILogger<WikiController>> _mockLogger;
    private WikiController _controller;

    [SetUp]
    public void Setup()
    {
        _mockWikiService = new Mock<IWikiGenerationService>();
        _mockWikiRepo = new Mock<IWikiRepository>();
        _mockOrchestrator = new Mock<ICodeWikiOrchestrator>();
        _mockHubContext = new Mock<IHubContext<WikiHub>>();
        _mockLogger = new Mock<ILogger<WikiController>>();
        _controller = new WikiController(_mockWikiService.Object, _mockWikiRepo.Object, _mockOrchestrator.Object, _mockHubContext.Object, _mockLogger.Object);
    }

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

        var cachedPage = new codeMRI.Core.Models.WikiPage { Title = "Existing Page", Content = "Cached Content" };

        _mockWikiRepo.Setup(x => x.GetPageByTitleAsync(request.RepoPath, request.Title))
                     .ReturnsAsync(cachedPage);

        // Act
        var result = await _controller.GeneratePage(request);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var page = okResult!.Value as codeMRI.Core.Models.WikiPage;
        
        Assert.That(page, Is.Not.Null);
        Assert.That(page!.Content, Is.EqualTo("Cached Content"));

        // Verify service was NOT called
        _mockWikiService.Verify(x => x.GeneratePageAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
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

        var newPage = new codeMRI.Core.Models.WikiPage { Title = "Existing Page", Content = "New Content" };

        _mockWikiService.Setup(x => x.GeneratePageAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<string?>()))
                        .ReturnsAsync(newPage);

        // Act
        var result = await _controller.GeneratePage(request);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var page = okResult!.Value as codeMRI.Core.Models.WikiPage;
        
        Assert.That(page!.Content, Is.EqualTo("New Content"));

        // Verify service WAS called
        _mockWikiService.Verify(x => x.GeneratePageAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
        
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

        var cachedPage = new codeMRI.Core.Models.WikiPage { Title = "Existing Page", Content = "Old Content based on Old Code" };

        _mockWikiRepo.Setup(x => x.GetPageByTitleAsync(request.RepoPath, request.Title))
                     .ReturnsAsync(cachedPage);

        // Act
        var result = await _controller.GeneratePage(request);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var page = okResult!.Value as codeMRI.Core.Models.WikiPage;
        
        // This confirms that it returns the OLD content even though we passed NEW code in the request
        Assert.That(page!.Content, Is.EqualTo("Old Content based on Old Code"));

        // Verify service was NOT called
        _mockWikiService.Verify(x => x.GeneratePageAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Test]
    public async Task GenerateAdvancedWiki_DelegatesToOrchestrator()
    {
        // Arrange
        var request = new StructureRequest { RepoPath = "/test/repo", Language = "C#" };
        var expectedStructure = new codeMRI.Core.Models.WikiStructure { Title = "Advanced Wiki" };

        _mockOrchestrator.Setup(x => x.GenerateAdvancedWikiAsync(request.RepoPath, It.IsAny<RepositoryInfo>(), It.IsAny<IProgress<codeMRI.Core.Models.ProgressInfo>?>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(expectedStructure);

        // Act
        var result = await _controller.GenerateAdvancedWiki(request);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var structure = okResult!.Value as codeMRI.Core.Models.WikiStructure;

        Assert.That(structure, Is.Not.Null);
        Assert.That(structure.Title, Is.EqualTo("Advanced Wiki"));

        // Verify Orchestrator was called
        _mockOrchestrator.Verify(x => x.GenerateAdvancedWikiAsync(request.RepoPath, It.IsAny<RepositoryInfo>(), It.IsAny<IProgress<codeMRI.Core.Models.ProgressInfo>?>(), It.IsAny<CancellationToken>()), Times.Once);
        
        // Verify Save was called
        _mockWikiRepo.Verify(x => x.SaveStructureAsync(request.RepoPath, expectedStructure), Times.Once);
    }
}
