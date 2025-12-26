using NUnit.Framework;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Concurrent;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class CodeWikiOrchestratorDelegationTests
{
    private Mock<IHierarchicalDecompositionService> _mockDecompositionService;
    private Mock<IEnhancedDependencyGraphService> _mockGraphService;
    private Mock<IRubricGenerationService> _mockRubricService;
    private Mock<IDocumentationJudgeService> _mockJudgeService;
    private Mock<IWikiGenerationService> _mockWikiGenerationService;
    private Mock<IDocumentationSynthesisService> _mockSynthesisService;
    private Mock<IDocumentationRevisionService> _mockRevisionService;
    private Mock<IWikiRepository> _mockWikiRepo;
    private Mock<ILogger<CodeWikiOrchestrator>> _mockLogger;
    private Mock<IProgressService> _mockProgressService;
    private Mock<IAgentTelemetryService> _mockTelemetryService;
    private Mock<IDelegationService> _mockDelegationService;
    private Mock<IDocumentIndexer> _mockDocumentIndexer;
    private Mock<INavigationStructureService> _mockNavigationService;
    private Mock<ILLMInvocationContext> _mockInvocationContext;
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
        _mockRevisionService = new Mock<IDocumentationRevisionService>();
        _mockWikiRepo = new Mock<IWikiRepository>();
        _mockLogger = new Mock<ILogger<CodeWikiOrchestrator>>();
        _mockProgressService = new Mock<IProgressService>();
        _mockTelemetryService = new Mock<IAgentTelemetryService>();
        _mockDelegationService = new Mock<IDelegationService>();
        _mockDocumentIndexer = new Mock<IDocumentIndexer>();
        _mockNavigationService = new Mock<INavigationStructureService>();
        _mockInvocationContext = new Mock<ILLMInvocationContext>();

        // Ensure WithScalingAsync executes the passed operation
        _mockProgressService
            .Setup(p => p.WithScalingAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Func<Task>>()))
            .Returns<double, double, Func<Task>>(async (start, width, op) =>
            {
                await op();
            });

        var mockOptions = new Mock<IOptions<CodeWikiOptions>>();
        mockOptions.Setup(o => o.Value).Returns(new CodeWikiOptions { MaxDegreeOfParallelism = 1 });

        _orchestrator = new CodeWikiOrchestrator(
            _mockDecompositionService.Object,
            _mockGraphService.Object,
            _mockRubricService.Object,
            _mockJudgeService.Object,
            _mockWikiGenerationService.Object,
            _mockSynthesisService.Object,
            _mockRevisionService.Object,
            _mockWikiRepo.Object,
            _mockProgressService.Object,
            _mockTelemetryService.Object,
            _mockDelegationService.Object,
            _mockDocumentIndexer.Object,
            _mockNavigationService.Object,
            mockOptions.Object,
            _mockInvocationContext.Object,
            _mockLogger.Object
        );
    }

    [Test]
    public async Task GenerateAdvancedWikiAsync_ShouldMapSubModulesToParentSection_WhenDelegated()
    {
        // Arrange
        var repoPath = "/test/repo";
        var repoInfo = new RepositoryInfo { Name = "TestRepo" };
        
        var originalModule = new ModuleNode
        {
            Id = "original_module",
            Name = "OriginalModule",
            IsLeaf = true,
            Components = new HashSet<string> { "comp1", "comp2" }
        };

        var moduleTree = new ModuleTree { Root = originalModule };

        _mockDecompositionService
            .Setup(s => s.DecomposeHierarchicallyAsync(repoPath, It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(moduleTree);

        _mockGraphService.Setup(g => g.GetComponentsAsync(repoPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CodeComponent>());
        _mockGraphService.Setup(g => g.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnhancedDependencyGraph());

        // Navigation Structure maps 'original_module' to 'section_1'
        var wikiStructure = new WikiStructure
        {
            Title = "Test Wiki",
            Sections = new List<WikiSection>
            {
                new WikiSection { Id = "section_1", Title = "Main Section" }
            },
            ModuleToSectionMap = new Dictionary<string, string> { { "original_module", "section_1" } }
        };

        _mockNavigationService
            .Setup(n => n.GenerateDocumentationStructureAsync(It.IsAny<ModuleTree>(), It.IsAny<RepositoryInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(wikiStructure);

        _mockRubricService.Setup(s => s.GenerateRubricAsync(It.IsAny<WikiStructure>(), It.IsAny<RepositoryInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationRubric());

        // Default delegation setup: no delegation needed
        _mockDelegationService
            .Setup(d => d.EvaluateDelegation(It.IsAny<ModuleNode>(), It.IsAny<EnhancedDependencyGraph>(), It.IsAny<int>()))
            .Returns(DelegationDecision.NoDelegation());

        // Simulation of delegation: original_module is subdivided into sub1 and sub2
        _mockDelegationService
            .Setup(d => d.EvaluateDelegation(originalModule, It.IsAny<EnhancedDependencyGraph>(), It.IsAny<int>()))
            .Returns(DelegationDecision.ForComplexity(100, 50));

        var subModule1 = new ModuleNode { Id = "sub1", Name = "Sub1", IsLeaf = true, Parent = originalModule };
        var subModule2 = new ModuleNode { Id = "sub2", Name = "Sub2", IsLeaf = true, Parent = originalModule };

        _mockDelegationService
            .Setup(d => d.DelegateModuleAsync(originalModule, It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModuleNode> { subModule1, subModule2 })
            .Callback(() => {
                 originalModule.IsLeaf = false;
                 originalModule.Children.Add(subModule1);
                 originalModule.Children.Add(subModule2);
            });

        // Mock Page Generation for SubModules
        _mockWikiGenerationService
            .Setup(s => s.GenerateEnhancedPageAsync(It.IsAny<ModuleNode>(), It.IsAny<List<WikiPage>?>(), It.IsAny<ModulePageContext>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AudienceType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>()))
            .ReturnsAsync((ModuleNode m, List<WikiPage>? r, ModulePageContext ctx, Dictionary<string, string> fc, string l, string? rp, AudienceType aud, string? rem, string? br, List<string>? fp) => 
                new WikiPage { Id = $"page_{m.Id}", Title = m.Name });

        // Mock Synthesis for Parent Page
        _mockSynthesisService
            .Setup(s => s.SynthesizeParentPageAsync(originalModule, It.IsAny<List<WikiPage>>(), It.IsAny<string>(), It.IsAny<AudienceType>(), true, It.IsAny<SynthesisStrategy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WikiPage { Id = "page_original", Title = "OriginalModule" });

        _mockJudgeService.Setup(s => s.EvaluateRequirementsAsync(It.IsAny<List<RubricRequirement>>(), It.IsAny<WikiStructure>(), It.IsAny<List<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RequirementAssessment>());

        // Act
        var result = await _orchestrator.GenerateAdvancedWikiAsync(repoPath, repoInfo);

        // Assert
        var mainSection = result.Sections.First(s => s.Id == "section_1");
        
        // The pages for sub1 and sub2 should be in section_1
        Assert.That(mainSection.PageRefs, Contains.Item("page_sub1"), "SubModule 1 page should be in parent section");
        Assert.That(mainSection.PageRefs, Contains.Item("page_sub2"), "SubModule 2 page should be in parent section");
        Assert.That(mainSection.PageRefs, Contains.Item("page_original"), "Original module page should be in its mapped section");
    }
}
