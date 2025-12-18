using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class CodeWikiOrchestratorTests
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
    private Mock<IAgentTelemetryService> _mockTelemetryService;
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
        _mockTelemetryService = new Mock<IAgentTelemetryService>();
        
        // Ensure WithScalingAsync executes the passed operation
        _mockProgressService
            .Setup(p => p.WithScalingAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Func<Task>>()))
            .Returns<double, double, Func<Task>>(async (start, width, op) => await op());

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
            _mockTelemetryService.Object,
            mockOptions.Object,
            _mockLogger.Object
        );
    }

    [Test]
    public async Task GenerateAdvancedWikiAsync_ShouldReuseExistingPage_WhenPageExists()
    {
        // Arrange
        var repoPath = "/test/repo";
        var repoInfo = new RepositoryInfo { Name = "TestRepo", Language = "C#" };
        var moduleName = "TestModule";
        
        var moduleTree = new ModuleTree
        {
            Root = new ModuleNode
            {
                Id = "root",
                Name = moduleName,
                IsLeaf = true,
                Components = new HashSet<string> { "comp1" }
            }
        };

        _mockDecompositionService
            .Setup(s => s.DecomposeHierarchicallyAsync(repoPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(moduleTree);

        _mockRubricService
            .Setup(s => s.GenerateRubricAsync(It.IsAny<WikiStructure>(), repoInfo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationRubric());
            
        _mockJudgeService
            .Setup(s => s.EvaluateRequirementsAsync(It.IsAny<List<RubricRequirement>>(), It.IsAny<WikiStructure>(), It.IsAny<List<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RequirementAssessment>());

        // Mock Cache Hit
        var existingPage = new WikiPage { Id = "existing-id", Title = moduleName, Content = "Cached Content" };
        _mockWikiRepo
            .Setup(r => r.GetPageByTitleAsync(repoPath, moduleName))
            .ReturnsAsync(existingPage);

        // Act
        await _orchestrator.GenerateAdvancedWikiAsync(repoPath, repoInfo);

        // Assert
        // Verify GetPageByTitleAsync was called
        _mockWikiRepo.Verify(r => r.GetPageByTitleAsync(repoPath, moduleName), Times.Once);
        
        // Verify GeneratePageAsync was NOT called
        _mockWikiGenerationService.Verify(
            s => s.GenerateEnhancedPageAsync(It.IsAny<ModuleNode>(), It.IsAny<List<WikiPage>?>(), It.IsAny<ModulePageContext>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AudienceType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>()),
            Times.Never);
            
        // Verify Synthesis was NOT called
        _mockSynthesisService.Verify(
            s => s.SynthesizeParentPageAsync(It.IsAny<ModuleNode>(), It.IsAny<List<WikiPage>>(), It.IsAny<string>(), It.IsAny<AudienceType>()),
            Times.Never);
    }

    [Test]
    public async Task GenerateAdvancedWikiAsync_ShouldGeneratePage_WhenPageDoesNotExist()
    {
        // Arrange
        var repoPath = "/test/repo";
        var repoInfo = new RepositoryInfo { Name = "TestRepo", Language = "C#" };
        var moduleName = "TestModule";
        
        var moduleTree = new ModuleTree
        {
            Root = new ModuleNode
            {
                Id = "root",
                Name = moduleName,
                IsLeaf = true,
                Components = new HashSet<string> { "comp1" }
            }
        };

        _mockDecompositionService
            .Setup(s => s.DecomposeHierarchicallyAsync(repoPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(moduleTree);

        _mockRubricService
            .Setup(s => s.GenerateRubricAsync(It.IsAny<WikiStructure>(), repoInfo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationRubric());
            
        _mockJudgeService
            .Setup(s => s.EvaluateRequirementsAsync(It.IsAny<List<RubricRequirement>>(), It.IsAny<WikiStructure>(), It.IsAny<List<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RequirementAssessment>());

        // Mock Cache Miss
        _mockWikiRepo
            .Setup(r => r.GetPageByTitleAsync(repoPath, moduleName))
            .ReturnsAsync((WikiPage?)null);

        // Mock Generation
        _mockWikiGenerationService
             .Setup(s => s.GenerateEnhancedPageAsync(It.IsAny<ModuleNode>(), It.IsAny<List<WikiPage>?>(), It.IsAny<ModulePageContext>(), It.IsAny<Dictionary<string, string>>(), "English", It.IsAny<string?>(), It.IsAny<AudienceType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>()))
             .ReturnsAsync(new WikiPage { Id = "new-id", Title = moduleName, Content = "Generated Content" });

        // Act
        await _orchestrator.GenerateAdvancedWikiAsync(repoPath, repoInfo);

        // Assert
        // Verify GetPageByTitleAsync was called
        _mockWikiRepo.Verify(r => r.GetPageByTitleAsync(repoPath, moduleName), Times.Once);
        
        // Verify GeneratePageAsync WAS called
        _mockWikiGenerationService.Verify(
            s => s.GenerateEnhancedPageAsync(It.IsAny<ModuleNode>(), It.IsAny<List<WikiPage>?>(), It.IsAny<ModulePageContext>(), It.IsAny<Dictionary<string, string>>(), "English", It.IsAny<string?>(), It.IsAny<AudienceType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>()),
            Times.Once);
            
        // Verify SavePageAsync was called
        _mockWikiRepo.Verify(r => r.SavePageAsync(repoPath, It.Is<WikiPage>(p => p.Title == moduleName)), Times.Once);
    }

    [Test]
    public async Task GenerateAdvancedWikiAsync_ShouldBuildHierarchicalSections_WhenModuleTreeIsNested()
    {
        // Arrange
        var repoPath = "/test/repo";
        var repoInfo = new RepositoryInfo { Name = "TestRepo", Url = "https://github.com/test", Branch = "main" };
        
        var childModule = new ModuleNode
        {
            Id = "child",
            Name = "ChildModule",
            IsLeaf = true,
            Components = new HashSet<string> { "comp2" }
        };

        var rootModule = new ModuleNode
        {
            Id = "root",
            Name = "RootModule",
            IsLeaf = false,
            Children = new List<ModuleNode> { childModule }
        };

        var moduleTree = new ModuleTree { Root = rootModule };

        _mockDecompositionService
            .Setup(s => s.DecomposeHierarchicallyAsync(repoPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(moduleTree);

        _mockRubricService
            .Setup(s => s.GenerateRubricAsync(It.IsAny<WikiStructure>(), repoInfo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationRubric());
            
        _mockJudgeService
            .Setup(s => s.EvaluateRequirementsAsync(It.IsAny<List<RubricRequirement>>(), It.IsAny<WikiStructure>(), It.IsAny<List<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RequirementAssessment>());

        // Mock Generation
        _mockWikiGenerationService
             .Setup(s => s.GenerateEnhancedPageAsync(It.IsAny<ModuleNode>(), It.IsAny<List<WikiPage>?>(), It.IsAny<ModulePageContext>(), It.IsAny<Dictionary<string, string>>(), "English", It.IsAny<string?>(), It.IsAny<AudienceType>(), repoInfo.Url, repoInfo.Branch, It.IsAny<List<string>?>()))
             .ReturnsAsync((ModuleNode m, List<WikiPage>? r, ModulePageContext ctx, Dictionary<string, string> fc, string l, string? rp, AudienceType a, string? remote, string? branch, List<string>? filePaths) => 
                new WikiPage { Id = $"id_{m.Name}", Title = m.Name, Content = $"Content for {m.Name}" });

        _mockSynthesisService
            .Setup(s => s.SynthesizeParentPageAsync(rootModule, It.IsAny<List<WikiPage>>(), "English", AudienceType.All))
            .ReturnsAsync(new WikiPage { Id = "id_RootModule", Title = "RootModule", Content = "Synthesized Content" });

        // Act
        var structure = await _orchestrator.GenerateAdvancedWikiAsync(repoPath, repoInfo);

        // Assert
        Assert.That(structure.Sections.Count, Is.EqualTo(1), "Only root section should be in the top-level list");
        var rootSection = structure.Sections.First();
        Assert.That(rootSection.Title, Is.EqualTo("RootModule"));
        Assert.That(rootSection.SubSections.Count, Is.EqualTo(1), "Root section should have one sub-section");
        Assert.That(rootSection.SubSections.First().Title, Is.EqualTo("ChildModule"));
    }
}
