using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace codeMRI.Infrastructure.Tests.Services;

[TestFixture]
public class DocumentationIndexerTests
{
    private Mock<IEmbeddingService> _mockEmbeddingService = null!;
    private Mock<IVectorStoreService> _mockVectorStoreService = null!;
    private Mock<ILogger<DocumentationIndexer>> _mockLogger = null!;
    private DocumentationIndexer _indexer = null!;

    [SetUp]
    public void SetUp()
    {
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockVectorStoreService = new Mock<IVectorStoreService>();
        _mockLogger = new Mock<ILogger<DocumentationIndexer>>();

        _mockEmbeddingService.Setup(x => x.GetDimensions()).Returns(768);

        _indexer = new DocumentationIndexer(
            _mockEmbeddingService.Object,
            _mockVectorStoreService.Object,
            _mockLogger.Object);
    }

    [Test]
    public async Task IndexPageAsync_WhenEmbeddingServiceThrowsHttpRequestException_ShouldPropagateException()
    {
        // Arrange
        var page = new WikiPage
        {
            Id = "test-page",
            Title = "Test Page",
            Content = "This is test content"
        };

        _mockEmbeddingService
            .Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("Ollama service unavailable"));

        // Act & Assert
        var exception = Assert.ThrowsAsync<HttpRequestException>(
            async () => await _indexer.IndexPageAsync("/test/repo", page));

        Assert.That(exception!.Message, Does.Contain("Ollama service unavailable"));
    }

    [Test]
    public async Task IndexPageAsync_WhenEmbeddingServiceThrowsInvalidOperationException_ShouldPropagateException()
    {
        // Arrange
        var page = new WikiPage
        {
            Id = "test-page",
            Title = "Test Page",
            Content = "This is test content"
        };

        _mockEmbeddingService
            .Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Ollama returned null or empty embedding"));

        // Act & Assert
        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _indexer.IndexPageAsync("/test/repo", page));

        Assert.That(exception!.Message, Does.Contain("Ollama returned null or empty embedding"));
    }

    [Test]
    public async Task IndexPageAsync_WhenEmbeddingSucceeds_ShouldUpsertToVectorStore()
    {
        // Arrange
        var page = new WikiPage
        {
            Id = "test-page",
            Title = "Test Page",
            Content = "This is test content"
        };

        var expectedEmbedding = new float[768];
        for (int i = 0; i < 768; i++)
        {
            expectedEmbedding[i] = 0.1f * i;
        }

        _mockEmbeddingService
            .Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(expectedEmbedding);

        _mockVectorStoreService
            .Setup(x => x.UpsertAsync(It.IsAny<string>(), It.IsAny<VectorDocument>()))
            .ReturnsAsync("doc-id");

        // Act
        await _indexer.IndexPageAsync("/test/repo", page);

        // Assert
        _mockVectorStoreService.Verify(
            x => x.UpsertAsync(
                "documentation",
                It.Is<VectorDocument>(doc =>
                    doc.Text == page.Content &&
                    doc.Vector == expectedEmbedding &&
                    doc.Metadata["pageId"].ToString() == page.Id &&
                    doc.Metadata["pageTitle"].ToString() == page.Title)),
            Times.Once);
    }

    [Test]
    public async Task IndexPageAsync_WhenContentIsEmpty_ShouldNotCallEmbeddingService()
    {
        // Arrange
        var page = new WikiPage
        {
            Id = "test-page",
            Title = "Test Page",
            Content = ""
        };

        // Act
        await _indexer.IndexPageAsync("/test/repo", page);

        // Assert
        _mockEmbeddingService.Verify(
            x => x.GetEmbeddingAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Test]
    public async Task IndexDocumentationAsync_WhenStructureHasMultiplePages_ShouldIndexAllPages()
    {
        // Arrange
        var structure = new WikiStructure
        {
            Pages = new List<WikiPage>
            {
                new WikiPage { Id = "page1", Title = "Page 1", Content = "Content 1" },
                new WikiPage { Id = "page2", Title = "Page 2", Content = "Content 2" },
                new WikiPage { Id = "page3", Title = "Page 3", Content = "Content 3" }
            }
        };

        var expectedEmbedding = new float[768];
        _mockEmbeddingService
            .Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(expectedEmbedding);

        _mockVectorStoreService
            .Setup(x => x.UpsertAsync(It.IsAny<string>(), It.IsAny<VectorDocument>()))
            .ReturnsAsync("doc-id");

        // Act
        await _indexer.IndexDocumentationAsync("/test/repo", structure);

        // Assert
        _mockVectorStoreService.Verify(
            x => x.CreateCollectionAsync("documentation", 768),
            Times.Once);

        _mockEmbeddingService.Verify(
            x => x.GetEmbeddingAsync(It.IsAny<string>()),
            Times.Exactly(3));
    }
}
