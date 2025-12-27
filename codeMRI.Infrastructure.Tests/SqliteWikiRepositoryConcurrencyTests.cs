using codeMRI.Core.Models;
using codeMRI.Infrastructure.Services;
using FluentAssertions;
using NUnit.Framework;

namespace codeMRI.Infrastructure.Tests;

[TestFixture]
public class SqliteWikiRepositoryConcurrencyTests
{
    private string _dbFile;
    private string _connectionString;

    [SetUp]
    public void Setup()
    {
        _dbFile = Path.Combine(Path.GetTempPath(), $"test_wiki_concurrency_{Guid.NewGuid()}.db");
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
                // Ignore
            }
        }
    }

    [Test]
    public async Task ConcurrentWrites_ShouldNotFailWithReadOnlyError()
    {
        // Arrange
        var sut = new SqliteWikiRepository(_connectionString);
        var repoPath = "/test/repo";
        
        int taskCount = 20;
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < taskCount; i++)
        {
            int index = i;
            tasks.Add(Task.Run(async () => 
            {
                var page = new WikiPage
                {
                    Id = $"page_{index}",
                    Title = $"Title {index}",
                    Content = $"Content {index}"
                };
                await sut.SavePageAsync(repoPath, page);
            }));
        }

        // Assert
        Func<Task> act = async () => await Task.WhenAll(tasks);
        await act.Should().NotThrowAsync();
    }
}
