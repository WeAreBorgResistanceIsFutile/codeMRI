using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class CodeWikiOrchestratorProgressTests
{
    private Mock<IHierarchicalDecompositionService> _mockDecompositionService;
    private Mock<IEnhancedDependencyGraphService> _mockGraphService;
    private Mock<IRubricGenerationService> _mockRubricService;
    private Mock<IDocumentationJudgeService> _mockJudgeService;
    private Mock<IWikiGenerationService> _mockWikiGenerationService;
    private Mock<IDocumentationSynthesisService> _mockSynthesisService;
    private Mock<IWikiRepository> _mockWikiRepo;
    private Mock<ILogger<CodeWikiOrchestrator>> _mockLogger;
    private Mock<IProgressService> _mockProgressService;
    private CodeWikiOrchestrator _orchestrator;

    [SetUp]
    public void Setup()
    {
        _mockDecompositionService = new Mock<IHierarchicalDecompositionService>();
        _mockGraphService = new Mock<IEnhancedDependencyGraphService>();
        _mockRubricService = new Mock<IRubricGenerationService>();
        _mockJudgeService = new Mock<IDocumentationJudgeService>();
        _mockWikiGenerationService = new Mock<IWikiGenerationService>();
        _mockSynthesisService = new Mock<IDocumentationSynthesisService>();
        _mockWikiRepo = new Mock<IWikiRepository>();
        _mockLogger = new Mock<ILogger<CodeWikiOrchestrator>>();
        _mockProgressService = new Mock<IProgressService>();
        var mockOptions = new Mock<IOptions<CodeWikiOptions>>();
        mockOptions.Setup(o => o.Value).Returns(new CodeWikiOptions { MaxDegreeOfParallelism = 1 });
        
        // Mock GetComponentsAsync and BuildGraphAsync
        _mockGraphService
            .Setup(g => g.GetComponentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CodeComponent>());
        _mockGraphService
            .Setup(g => g.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnhancedDependencyGraph());

        _orchestrator = new CodeWikiOrchestrator(
            _mockDecompositionService.Object,
            _mockGraphService.Object,
            _mockRubricService.Object,
            _mockJudgeService.Object,
            _mockWikiGenerationService.Object,
            _mockSynthesisService.Object,
            _mockWikiRepo.Object,
            _mockProgressService.Object,
            mockOptions.Object,
            _mockLogger.Object
        );
    }

    [Test]
    public async Task GenerateAdvancedWikiAsync_ReportsProgress()
    {
        // Arrange
        var repoPath = "/test/repo";
        var repoInfo = new RepositoryInfo { Name = "TestRepo" };
        var progress = new Mock<IProgress<ProgressInfo>>();
        var capturedProgress = new List<ProgressInfo>();

        progress.Setup(p => p.Report(It.IsAny<ProgressInfo>()))
                .Callback<ProgressInfo>(p => capturedProgress.Add(p));

        // Mock ProgressService behavior
        Action<ProgressInfo> storedHandler = null;
        _mockProgressService.Setup(x => x.SetHandler(It.IsAny<Action<ProgressInfo>>()))
            .Callback<Action<ProgressInfo>>(h => storedHandler = h);
        
        _mockProgressService.Setup(x => x.Report(It.IsAny<ProgressInfo>()))
            .Callback<ProgressInfo>(info => storedHandler?.Invoke(info));

        _mockProgressService
            .Setup(p => p.WithScalingAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Func<Task>>()))
            .Returns<double, double, Func<Task>>(async (start, width, op) => await op());

        // Setup Mocks
        var rootNode = new ModuleNode { Id = "root", Name = "Root", IsLeaf = false };
        var childNode = new ModuleNode { Id = "child", Name = "Child", IsLeaf = true };
        rootNode.Children.Add(childNode);
        var moduleTree = new ModuleTree { Root = rootNode };

        _mockDecompositionService.Setup(x => x.DecomposeHierarchicallyAsync(repoPath, It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(moduleTree);

        var rubric = new EvaluationRubric 
        { 
            Title = "Root", 
            Children = new List<RubricNode> 
            { 
                new RubricCategory { Title = "Category" } 
            } 
        };
        _mockRubricService.Setup(x => x.GenerateRubricAsync(It.IsAny<WikiStructure>(), repoInfo, It.IsAny<CancellationToken>()))
                          .ReturnsAsync(rubric);

        _mockWikiGenerationService.Setup(x => x.GeneratePageAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
                                  .ReturnsAsync(new WikiPage { Id = "p1", Title = "Child" });
        
        _mockSynthesisService.Setup(x => x.SynthesizeParentPageAsync(It.IsAny<ModuleNode>(), It.IsAny<List<WikiPage>>(), It.IsAny<string>(), It.IsAny<AudienceType>()))
                             .ReturnsAsync(new WikiPage { Id = "p2", Title = "Root" });

        _mockJudgeService.Setup(x => x.EvaluateRequirementsAsync(It.IsAny<List<RubricRequirement>>(), It.IsAny<WikiStructure>(), It.IsAny<List<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(new List<RequirementAssessment>());

        // Act
        await _orchestrator.GenerateAdvancedWikiAsync(repoPath, repoInfo, progress.Object);

        // Assert
        Assert.That(capturedProgress, Is.Not.Empty);
        
        // Check for specific phases
        Assert.That(capturedProgress.Any(p => p.Phase == "Decomposition"), Is.True);
        Assert.That(capturedProgress.Any(p => p.Phase == "Rubric Generation"), Is.True);
        Assert.That(capturedProgress.Any(p => p.Phase == "Content Generation"), Is.True);
        Assert.That(capturedProgress.Any(p => p.Phase == "Evaluation"), Is.True);
        Assert.That(capturedProgress.Any(p => p.Phase == "Complete"), Is.True);
        
        // Verify increasing percentages
        var percentages = capturedProgress.Select(p => p.Percentage).ToList();
        
        // We can't strictly assert strictly ordered because multiple modules might report same percentage if rounding
        // But the last one should be 100
        Assert.That(percentages.Last(), Is.EqualTo(100));
        
        // Verify that Content Generation phase reported at least once
        Assert.That(capturedProgress.Any(p => p.Phase == "Content Generation" && p.Message.Contains("Child")), Is.True);
    }
}