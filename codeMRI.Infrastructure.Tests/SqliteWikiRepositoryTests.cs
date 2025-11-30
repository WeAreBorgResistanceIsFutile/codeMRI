using codeMRI.Infrastructure.Services;
using codeMRI.Shared.Models;
using FluentAssertions;
using NUnit.Framework;

namespace codeMRI.Infrastructure.Tests;

[TestFixture]
public class SqliteWikiRepositoryTests
{
    private string _dbFile;
    private string _connectionString;

    [SetUp]
    public void Setup()
    {
        _dbFile = Path.Combine(Path.GetTempPath(), $"test_wiki_{Guid.NewGuid()}.db");
        _connectionString = $"Data Source={_dbFile}";
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_dbFile))
        {
            try
            {
                File.Delete(_dbFile);
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
    }

    [Test]
    public async Task SaveAndGetStructure_ShouldPersistData()
    {
        // Arrange
        var sut = new SqliteWikiRepository(_connectionString);
        var repoPath = "/test/repo";
        var structure = new WikiStructure
        {
            Title = "Test Wiki",
            Sections = new List<WikiSection>
            {
                new WikiSection { Title = "Intro", PageRefs = { "p1" } }
            }
        };

        // Act
        await sut.SaveStructureAsync(repoPath, structure);
        var result = await sut.GetStructureAsync(repoPath);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("Test Wiki");
        result.Sections.Should().HaveCount(1);
        result.Sections[0].Title.Should().Be("Intro");
    }

    [Test]
    public async Task SaveAndGetPage_ShouldPersistData()
    {
        // Arrange
        var sut = new SqliteWikiRepository(_connectionString);
        var repoPath = "/test/repo";
        var page = new WikiPage
        {
            Id = "p1",
            Title = "Page One",
            Content = "Content of page one"
        };

        // Act
        await sut.SavePageAsync(repoPath, page);
        var result = await sut.GetPageAsync(repoPath, "p1");

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("Page One");
        result.Content.Should().Be("Content of page one");
    }

    [Test]
    public async Task GetPageByTitle_ShouldReturnCorrectPage()
    {
        // Arrange
        var sut = new SqliteWikiRepository(_connectionString);
        var repoPath = "/test/repo";
        var page1 = new WikiPage { Id = "p1", Title = "Alpha", Content = "A" };
        var page2 = new WikiPage { Id = "p2", Title = "Beta", Content = "B" };

        await sut.SavePageAsync(repoPath, page1);
        await sut.SavePageAsync(repoPath, page2);

        // Act
        var result = await sut.GetPageByTitleAsync(repoPath, "beta"); // Case insensitive check

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("p2");
    }

    [Test]
    public async Task SaveAndGetManifest_ShouldPersistData()
    {
        // Arrange
        var sut = new SqliteWikiRepository(_connectionString);
        var repoPath = "/test/repo";
        var manifest = new Dictionary<string, string>
        {
            { "file1.cs", "hash1" },
            { "file2.cs", "hash2" }
        };

        // Act
        await sut.SaveIngestionManifestAsync(repoPath, manifest);
        var result = await sut.GetIngestionManifestAsync(repoPath);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey("file1.cs").WhoseValue.Should().Be("hash1");
        result.Should().ContainKey("file2.cs").WhoseValue.Should().Be("hash2");
    }

    [Test]
    public async Task DeleteStructure_ShouldRemoveData()
    {
        // Arrange
        var sut = new SqliteWikiRepository(_connectionString);
        var repoPath = "/test/repo";
        var structure = new WikiStructure { Title = "To Delete" };
        await sut.SaveStructureAsync(repoPath, structure);

        // Act
        await sut.DeleteStructureAsync(repoPath);
        var result = await sut.GetStructureAsync(repoPath);

        // Assert
        result.Should().BeNull();
    }
}
