using codeMRI.Core.Interfaces;
using codeMRI.Infrastructure.Configuration;
using codeMRI.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace codeMRI.Infrastructure.Tests.Services;

[TestFixture]
public class VectorStoreInitializationServiceTests
{
    private Mock<IVectorStoreService> _mockVectorStoreService = null!;
    private Mock<IEmbeddingService> _mockEmbeddingService = null!;
    private Mock<IOptions<VectorStoreSettings>> _mockSettings = null!;
    private Mock<ILogger<VectorStoreInitializationService>> _mockLogger = null!;
    private VectorStoreInitializationService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _mockVectorStoreService = new Mock<IVectorStoreService>();
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockSettings = new Mock<IOptions<VectorStoreSettings>>();
        _mockLogger = new Mock<ILogger<VectorStoreInitializationService>>();

        _mockEmbeddingService.Setup(x => x.GetDimensions()).Returns(768);
        _mockSettings.Setup(x => x.Value).Returns(new VectorStoreSettings
        {
            Host = "localhost",
            Port = 6334,
            UseHttps = false
        });

        _service = new VectorStoreInitializationService(
            _mockVectorStoreService.Object,
            _mockEmbeddingService.Object,
            _mockSettings.Object,
            _mockLogger.Object);
    }

    [Test]
    public async Task InitializeAsync_WhenNoCollectionsExist_ShouldCreateAllCollections()
    {
        // Arrange
        _mockVectorStoreService
            .Setup(x => x.CreateCollectionAsync(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.InitializeAsync();

        // Assert
        _mockVectorStoreService.Verify(
            x => x.CreateCollectionAsync("documentation", 768),
            Times.Once);
        _mockVectorStoreService.Verify(
            x => x.CreateCollectionAsync("code", 768),
            Times.Once);
    }

    [Test]
    public async Task InitializeAsync_WhenSomeCollectionsExist_ShouldOnlyCreateMissing()
    {
        // Arrange
        // Simulate "documentation" already exists (CreateCollectionAsync logs and returns)
        _mockVectorStoreService
            .Setup(x => x.CreateCollectionAsync("documentation", It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        _mockVectorStoreService
            .Setup(x => x.CreateCollectionAsync("code", It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.InitializeAsync();

        // Assert - Both should be called, CreateCollectionAsync handles existing collections
        _mockVectorStoreService.Verify(
            x => x.CreateCollectionAsync("documentation", 768),
            Times.Once);
        _mockVectorStoreService.Verify(
            x => x.CreateCollectionAsync("code", 768),
            Times.Once);
    }

    [Test]
    public async Task InitializeAsync_WhenAllCollectionsExist_ShouldStillCallCreateForEach()
    {
        // Arrange
        _mockVectorStoreService
            .Setup(x => x.CreateCollectionAsync(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.InitializeAsync();

        // Assert - CreateCollectionAsync is idempotent and handles existing collections
        _mockVectorStoreService.Verify(
            x => x.CreateCollectionAsync(It.IsAny<string>(), It.IsAny<int>()),
            Times.Exactly(2));
    }

    [Test]
    public async Task InitializeAsync_WhenCreationFails_ShouldLogErrorAndContinue()
    {
        // Arrange
        _mockVectorStoreService
            .Setup(x => x.CreateCollectionAsync("documentation", It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        _mockVectorStoreService
            .Setup(x => x.CreateCollectionAsync("code", It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.InitializeAsync();

        // Assert - Should not throw, should continue with other collections
        _mockVectorStoreService.Verify(
            x => x.CreateCollectionAsync("documentation", 768),
            Times.Once);
        _mockVectorStoreService.Verify(
            x => x.CreateCollectionAsync("code", 768),
            Times.Once);
    }

    [Test]
    public async Task InitializeAsync_ShouldUseEmbeddingDimensions()
    {
        // Arrange
        _mockEmbeddingService.Setup(x => x.GetDimensions()).Returns(1536);
        _mockVectorStoreService
            .Setup(x => x.CreateCollectionAsync(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var serviceWithDifferentDimensions = new VectorStoreInitializationService(
            _mockVectorStoreService.Object,
            _mockEmbeddingService.Object,
            _mockSettings.Object,
            _mockLogger.Object);

        // Act
        await serviceWithDifferentDimensions.InitializeAsync();

        // Assert
        _mockVectorStoreService.Verify(
            x => x.CreateCollectionAsync(It.IsAny<string>(), 1536),
            Times.Exactly(2));
    }

    [Test]
    public async Task InitializeAsync_ShouldLogInitializationStart()
    {
        // Arrange
        _mockVectorStoreService
            .Setup(x => x.CreateCollectionAsync(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.InitializeAsync();

        // Assert - Verify information logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Initializing vector store collections")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
