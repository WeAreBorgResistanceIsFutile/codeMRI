using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Infrastructure.Services;
using FluentAssertions;
using NUnit.Framework;

namespace codeMRI.Infrastructure.Tests;

[TestFixture]
public class ReingestionDuplicationTests
{
    private string _testDbPath = null!;
    private IWikiRepository _wikiRepo = null!;

    [SetUp]
    public void Setup()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_reingestion_{Guid.NewGuid()}.db");
        _wikiRepo = new SqliteWikiRepository($"Data Source={_testDbPath}");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }

    [Test]
    public async Task SameRepoPath_ShouldReuseRepositoryId()
    {
        // Arrange
        var repoPath = "/data/repos/test-repo";

        // Act - First save
        var structure1 = new WikiStructure { Title = "First" };
        await _wikiRepo.SaveStructureAsync(repoPath, structure1);

        var summaries1 = await _wikiRepo.GetAllRepositorySummariesAsync();
        var firstCount = summaries1.Count;

        // Act - Second save (simulating re-ingestion)
        var structure2 = new WikiStructure { Title = "Second" };
        await _wikiRepo.SaveStructureAsync(repoPath, structure2);

        var summaries2 = await _wikiRepo.GetAllRepositorySummariesAsync();
        var secondCount = summaries2.Count;

        // Assert
        firstCount.Should().Be(1, "First save should create one repository");
        secondCount.Should().Be(1, "Second save with same path should NOT create duplicate");

        var retrieved = await _wikiRepo.GetStructureAsync(repoPath);

        retrieved.Should().NotBeNull();
        retrieved!.Title.Should().Be("Second", "Structure should be updated, not duplicated");
    }

    [Test]
    public async Task SameRepoPath_ShouldUpdateNotDuplicatePages()
    {
        // Arrange
        var repoPath = "/data/repos/test-repo";
        var pageId = "page-1";

        // Act - First save
        var page1 = new WikiPage 
        { 
            Id = pageId, 
            Title = "First Title",
            Content = "First content"
        };
        await _wikiRepo.SavePageAsync(repoPath, page1);

        var pages1 = await _wikiRepo.GetAllPagesAsync(repoPath);
        var firstCount = pages1.Count;

        // Act - Second save (re-ingestion with same page ID)
        var page2 = new WikiPage 
        { 
            Id = pageId, 
            Title = "Updated Title",
            Content = "Updated content"
        };
        await _wikiRepo.SavePageAsync(repoPath, page2);

        var pages2 = await _wikiRepo.GetAllPagesAsync(repoPath);
        var secondCount = pages2.Count;

        // Assert
        firstCount.Should().Be(1, "First save should create one page");
        secondCount.Should().Be(1, "Second save with same page ID should update, not duplicate");

        var retrieved = await _wikiRepo.GetPageAsync(repoPath, pageId);
        retrieved.Should().NotBeNull();
        retrieved!.Title.Should().Be("Updated Title", "Page should be updated");
        retrieved.Content.Should().Be("Updated content");
    }

    [Test]
    public async Task DifferentRepoPath_ShouldCreateSeparateRepository()
    {
        // Arrange
        var repoPath1 = "/data/repos/repo-1";
        var repoPath2 = "/data/repos/repo-2";

        // Act
        var structure1 = new WikiStructure { Title = "Repo 1" };
        await _wikiRepo.SaveStructureAsync(repoPath1, structure1);

        var structure2 = new WikiStructure { Title = "Repo 2" };
        await _wikiRepo.SaveStructureAsync(repoPath2, structure2);

        var summaries = await _wikiRepo.GetAllRepositorySummariesAsync();

        // Assert
        summaries.Count.Should().Be(2, "Different paths should create separate repositories");
        summaries.Should().Contain(s => s.Path == repoPath1);
        summaries.Should().Contain(s => s.Path == repoPath2);
    }

    [Test]
    public async Task RemoteUrl_ShouldBePreservedAcrossUpdates()
    {
        // Arrange
        var repoPath = "/data/repos/test-repo";
        var remoteUrl = "https://github.com/test/repo.git";

        // Act - Set remote URL
        await _wikiRepo.SetRepositoryRemoteUrlAsync(repoPath, remoteUrl);

        // Save structure (simulating ingestion)
        var structure1 = new WikiStructure { Title = "First" };
        await _wikiRepo.SaveStructureAsync(repoPath, structure1);

        var summaries1 = await _wikiRepo.GetAllRepositorySummariesAsync();
        var repo1 = summaries1.First(s => s.Path == repoPath);

        // Act - Re-ingestion (save structure again)
        var structure2 = new WikiStructure { Title = "Second" };
        await _wikiRepo.SaveStructureAsync(repoPath, structure2);

        var summaries2 = await _wikiRepo.GetAllRepositorySummariesAsync();
        var repo2 = summaries2.First(s => s.Path == repoPath);

        // Assert
        repo1.RemoteUrl.Should().Be(remoteUrl, "Remote URL should be set initially");
        repo2.RemoteUrl.Should().Be(remoteUrl, "Remote URL should be preserved after re-ingestion");
        summaries2.Count.Should().Be(1, "Should still have only one repository");
    }
}
