using codeMRI.Infrastructure.Configuration;
using codeMRI.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Qdrant.Client;
using Testcontainers.Qdrant;
using Document = codeMRI.Core.Models.Document;

namespace codeMRI.Infrastructure.Tests;

[TestFixture]
public class QdrantVectorDbTests
{
    [OneTimeSetUp]
    public async Task GlobalSetup()
    {
        // Initialize Qdrant container
        _qdrantContainer = new QdrantBuilder()
            .WithImage("qdrant/qdrant:v1.11.0") // Using a stable recent version
            .Build();

        await _qdrantContainer.StartAsync();

        // Create client connecting to the container
        // Qdrant gRPC port is 6334
        _qdrantClient = new QdrantClient("localhost", _qdrantContainer.GetMappedPublicPort(6334));

        _settings = new QdrantSettings
        {
            VectorSize = 4,
            Host = "localhost",
            Port = _qdrantContainer.GetMappedPublicPort(6334)
        };

        _mockSettings = new Mock<IOptions<QdrantSettings>>();
        _mockSettings.Setup(s => s.Value).Returns(_settings);
    }

    [OneTimeTearDown]
    public async Task GlobalTeardown()
    {
        _qdrantClient.Dispose();
        await _qdrantContainer.DisposeAsync();
    }

    private QdrantContainer _qdrantContainer;
    private QdrantClient _qdrantClient;
    private Mock<IOptions<QdrantSettings>> _mockSettings;
    private QdrantSettings _settings;

    private QdrantVectorDb CreateSut()
    {
        return new QdrantVectorDb(_qdrantClient, _mockSettings.Object);
    }

    [Test]
    public async Task InitializeAsync_ShouldCreateCollection()
    {
        // Arrange
        var sut = CreateSut();
        var collectionName = "test_init_" + Guid.NewGuid().ToString("N");

        // Act
        await sut.InitializeAsync(collectionName);

        // Assert
        var collections = await _qdrantClient.ListCollectionsAsync();
        collections.Should().Contain(collectionName);
    }

    [Test]
    public async Task UpsertAndSearch_ShouldStoreAndRetrieveDocuments()
    {
        // Arrange
        var sut = CreateSut();
        var collectionName = "test_upsert_" + Guid.NewGuid().ToString("N");
        await sut.InitializeAsync(collectionName);

        var docId = Guid.NewGuid().ToString();
        var documents = new[]
        {
            new Document
            {
                Id = docId,
                Content = "This is a test document about search.",
                FilePath = "/path/to/doc.txt",
                Embedding = new[] { 0.1f, 0.2f, 0.3f, 0.4f }, // Size 4 matches settings
                Metadata = new Dictionary<string, string> { { "author", "tester" } }
            }
        };

        // Act
        await sut.UpsertAsync(documents);

        // Allow slight delay for indexing (though Qdrant is usually instant for small data)
        // In a real unit test, we might want to wait/poll, but for this integrated test:
        await Task.Delay(200);

        // Search with a similar vector
        var searchVector = new[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var results = await sut.SearchAsync(searchVector, 1);

        // Assert
        results.Should().HaveCount(1);
        var resultDoc = results.First();
        resultDoc.Content.Should().Be("This is a test document about search.");
        resultDoc.FilePath.Should().Be("/path/to/doc.txt");
        resultDoc.Metadata.Should().ContainKey("author").WhoseValue.Should().Be("tester");
        // Id might be normalized, let's check equality
        // Qdrant returns UUIDs.
        Guid.Parse(resultDoc.Id).Should().Be(Guid.Parse(docId));
    }

    [Test]
    public async Task DeleteByMetadataAsync_ShouldRemoveDocuments()
    {
        // Arrange
        var sut = CreateSut();
        var collectionName = "test_delete_" + Guid.NewGuid().ToString("N");
        await sut.InitializeAsync(collectionName);

        var documents = new[]
        {
            new Document
            {
                Id = Guid.NewGuid().ToString(),
                Content = "Doc 1",
                Embedding = new[] { 0.1f, 0.1f, 0.1f, 0.1f },
                Metadata = new Dictionary<string, string> { { "category", "delete_me" } }
            },
            new Document
            {
                Id = Guid.NewGuid().ToString(),
                Content = "Doc 2",
                Embedding = new[] { 0.2f, 0.2f, 0.2f, 0.2f },
                Metadata = new Dictionary<string, string> { { "category", "keep_me" } }
            }
        };

        await sut.UpsertAsync(documents);
        await Task.Delay(200);

        // Verify both exist
        var initialSearch = await sut.SearchAsync(new[] { 0.1f, 0.1f, 0.1f, 0.1f }, 10);
        initialSearch.Should().HaveCount(2);

        // Act
        await sut.DeleteByMetadataAsync("category", "delete_me");
        await Task.Delay(200);

        // Assert
        var finalSearch = await sut.SearchAsync(new[] { 0.1f, 0.1f, 0.1f, 0.1f }, 10);
        finalSearch.Should().HaveCount(1);
        finalSearch.First().Metadata["category"].Should().Be("keep_me");
    }
}