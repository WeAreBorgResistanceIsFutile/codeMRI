using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Server.Api;
using codeMRI.Server.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;

namespace codeMRI.Core.Tests.Controllers;

[TestFixture]
public class WikiControllerTests
{
    private Mock<IWikiGenerationService> _mockWikiService;
    private Mock<IWikiRepository> _mockWikiRepo;
    private WikiController _controller;

    [SetUp]
    public void Setup()
    {
        _mockWikiService = new Mock<IWikiGenerationService>();
        _mockWikiRepo = new Mock<IWikiRepository>();
        _controller = new WikiController(_mockWikiService.Object, _mockWikiRepo.Object);
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
        _mockWikiService.Verify(x => x.GeneratePageAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()), Times.Never);
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

        _mockWikiService.Setup(x => x.GeneratePageAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                        .ReturnsAsync(newPage);

        // Act
        var result = await _controller.GeneratePage(request);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var page = okResult!.Value as codeMRI.Core.Models.WikiPage;
        
        Assert.That(page!.Content, Is.EqualTo("New Content"));

        // Verify service WAS called
        _mockWikiService.Verify(x => x.GeneratePageAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()), Times.Once);
        
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
        _mockWikiService.Verify(x => x.GeneratePageAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()), Times.Never);
    }
}
