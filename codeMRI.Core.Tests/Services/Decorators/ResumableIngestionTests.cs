using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using codeMRI.Core.Services.Decorators;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace codeMRI.Core.Tests.Services.Decorators;

[TestFixture]
public class ResumableIngestionTests
{
    private Mock<IHierarchicalDecompositionService> _mockInnerDecomposition;
    private Mock<IWikiRepository> _mockWikiRepo;
    private Mock<ILLMInvocationContext> _mockInvocationContext;
    private Mock<ILogger<ResumableDecompositionDecorator>> _mockDecompLogger;
    private ResumableDecompositionDecorator _decompositionDecorator;

    [SetUp]
    public void Setup()
    {
        _mockInnerDecomposition = new Mock<IHierarchicalDecompositionService>();
        _mockWikiRepo = new Mock<IWikiRepository>();
        _mockInvocationContext = new Mock<ILLMInvocationContext>();
        _mockDecompLogger = new Mock<ILogger<ResumableDecompositionDecorator>>();
        _decompositionDecorator = new ResumableDecompositionDecorator(
            _mockInnerDecomposition.Object,
            _mockWikiRepo.Object,
            _mockInvocationContext.Object,
            _mockDecompLogger.Object);
    }

    [Test]
    public async Task DecompositionDecorator_ShouldSkip_WhenStateHasSerializedGraph()
    {
        // Arrange
        var repoPath = "/test/repo";
        var state = new IngestionProcessingState
        {
            SerializedGraph = "{\"Nodes\":[{\"Id\":\"module_1\", \"Type\":\"Module\"}], \"OutgoingEdges\":{}, \"IncomingEdges\":{}}" 
        };

        _mockWikiRepo.Setup(r => r.GetIngestionProcessingStateAsync(repoPath))
            .ReturnsAsync(state);

        // Act
        var result = await _decompositionDecorator.DecomposeHierarchicallyAsync(repoPath, new EnhancedDependencyGraph());

        // Assert
        Assert.That(result, Is.Not.Null);
        _mockInnerDecomposition.Verify(s => s.DecomposeHierarchicallyAsync(It.IsAny<string>(), It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task DecompositionDecorator_ShouldCallInner_WhenStateIsEmpty()
    {
        // Arrange
        var repoPath = "/test/repo";
        _mockWikiRepo.Setup(r => r.GetIngestionProcessingStateAsync(repoPath))
            .ReturnsAsync(new IngestionProcessingState());

        _mockInnerDecomposition.Setup(s => s.DecomposeHierarchicallyAsync(It.IsAny<string>(), It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleTree());

        // Act
        await _decompositionDecorator.DecomposeHierarchicallyAsync(repoPath, new EnhancedDependencyGraph());

        // Assert
        _mockInnerDecomposition.Verify(s => s.DecomposeHierarchicallyAsync(repoPath, It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task DebugSnapshotLLMClientDecorator_ShouldSaveSnapshot_OnFailure()
    {
        // Arrange
        var mockInnerLLM = new Mock<ILLMClient>();
        var mockSnapshotService = new Mock<IDebugSnapshotService>();
        var mockInvocationContext = new Mock<ILLMInvocationContext>();
        var mockValidator = new Mock<codeMRI.Core.Services.MessageComposition.ILLMValidator>();
        var mockLogger = new Mock<ILogger<DebugSnapshotLLMClientDecorator>>();

        mockInvocationContext.Setup(c => c.RepoPath).Returns("/test/repo");
        mockInvocationContext.Setup(c => c.ComponentId).Returns("test-component");
        
        mockValidator.Setup(v => v.ValidateMessages(It.IsAny<List<ChatMessage>>()))
            .Returns(new codeMRI.Core.Services.MessageComposition.MessageValidationResult 
            { 
                IsValid = true, 
                EstimatedTokens = 10, 
                AvailableTokens = 100 
            });

        var decorator = new DebugSnapshotLLMClientDecorator(
            mockInnerLLM.Object,
            mockSnapshotService.Object,
            mockInvocationContext.Object,
            mockValidator.Object,
            mockLogger.Object);

        mockInnerLLM.Setup(l => l.ChatAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM Error"));

        // Act & Assert
        Assert.ThrowsAsync<Exception>(async () => await decorator.ChatAsync(new List<ChatMessage> { new ChatMessage("user", "test prompt") }));
        
        mockSnapshotService.Verify(s => s.SaveSnapshotAsync("/test/repo", It.Is<DebugSnapshot>(d => d.ComponentId == "test-component")), Times.Once);
    }

    [Test]
    public async Task WikiGenerationDecorator_ShouldSkip_WhenPageExists()
    {
        // Arrange
        var mockInnerWiki = new Mock<IWikiGenerationService>();
        var mockInvocationContext = new Mock<ILLMInvocationContext>();
        var mockLogger = new Mock<ILogger<ResumableWikiGenerationDecorator>>();

        var decorator = new ResumableWikiGenerationDecorator(
            mockInnerWiki.Object,
            _mockWikiRepo.Object,
            mockInvocationContext.Object,
            mockLogger.Object);

        var existingPage = new WikiPage { Id = "existing", Title = "Test Page", Content = "Existing Content" };
        _mockWikiRepo.Setup(r => r.GetPageByTitleAsync("/test/repo", "Test Page"))
            .ReturnsAsync(existingPage);

        // Act
        var result = await decorator.GeneratePageAsync("Test Page", new List<string>(), new Dictionary<string, string>(), "English", "/test/repo");

        // Assert
        Assert.That(result, Is.EqualTo(existingPage));
        mockInnerWiki.Verify(w => w.GeneratePageAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Test]
    public async Task RubricDecorator_ShouldSkip_WhenSerializedRubricExists()
    {
        // Arrange
        var mockInnerRubric = new Mock<IRubricGenerationService>();
        var mockLogger = new Mock<ILogger<ResumableRubricDecorator>>();

        var decorator = new ResumableRubricDecorator(
            mockInnerRubric.Object,
            _mockWikiRepo.Object,
            _mockInvocationContext.Object,
            mockLogger.Object);

        var state = new IngestionProcessingState
        {
            SerializedRubric = "{\"Title\":\"Existing Rubric\"}"
        };
        _mockWikiRepo.Setup(r => r.GetIngestionProcessingStateAsync("/test/repo"))
            .ReturnsAsync(state);

        // Act
        var result = await decorator.GenerateRubricAsync(new WikiStructure(), new RepositoryInfo { RepoPath = "/test/repo" });

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Title, Is.EqualTo("Existing Rubric"));
        mockInnerRubric.Verify(r => r.GenerateRubricAsync(It.IsAny<WikiStructure>(), It.IsAny<RepositoryInfo>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
