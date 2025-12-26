using NUnit.Framework;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Concurrent;

namespace codeMRI.Core.Tests.Services;

/// <summary>
///     Tests to verify cluster synthesis behavior in CodeWikiOrchestrator.
///     This test FAILS because the orchestrator doesn't properly pass mergeChildContent parameter.
/// </summary>
[TestFixture]
public class CodeWikiOrchestratorClusterSynthesisTests
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
    public async Task GenerateAdvancedWikiAsync_ShouldCallSynthesisWithMergeTrue_WhenModuleDelegated()
    {
        // Arrange
        var repoPath = "/test/repo";
        var repoInfo = new RepositoryInfo { Name = "TestRepo" };

        var originalModule = new ModuleNode
        {
            Id = "original_module",
            Name = "LargeModule",
            IsLeaf = true,
            Components = new HashSet<string> { "comp1", "comp2", "comp3" }
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
                new WikiSection { Id = "section_1", Title = "Large Module Section" }
            },
            ModuleToSectionMap = new Dictionary<string, string> { { "original_module", "section_1" } }
        };

        _mockNavigationService
            .Setup(n => n.GenerateDocumentationStructureAsync(It.IsAny<ModuleTree>(), It.IsAny<RepositoryInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(wikiStructure);

        _mockRubricService.Setup(s => s.GenerateRubricAsync(It.IsAny<WikiStructure>(), It.IsAny<RepositoryInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationRubric());

        // Default: no delegation
        _mockDelegationService
            .Setup(d => d.EvaluateDelegation(It.IsAny<ModuleNode>(), It.IsAny<EnhancedDependencyGraph>(), It.IsAny<int>()))
            .Returns(DelegationDecision.NoDelegation());

        // For original_module: delegate into 2 clusters
        _mockDelegationService
            .Setup(d => d.EvaluateDelegation(originalModule, It.IsAny<EnhancedDependencyGraph>(), It.IsAny<int>()))
            .Returns(DelegationDecision.ForComplexity(150, 50));

        var cluster1 = new ModuleNode { Id = "cluster_0", Name = "src/cluster_0", IsLeaf = true, Parent = originalModule };
        var cluster2 = new ModuleNode { Id = "cluster_1", Name = "src/cluster_1", IsLeaf = true, Parent = originalModule };

        _mockDelegationService
            .Setup(d => d.DelegateModuleAsync(originalModule, It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModuleNode> { cluster1, cluster2 })
            .Callback(() =>
            {
                originalModule.IsLeaf = false;
                originalModule.Children.Add(cluster1);
                originalModule.Children.Add(cluster2);
            });

        // Mock cluster page generation
        // Note: The mock page IDs MUST match the cluster module IDs
        var clusterPage1 = new WikiPage { Id = "cluster_0", Title = "Cluster_0", Content = "Cluster 0 content" };
        var clusterPage2 = new WikiPage { Id = "cluster_1", Title = "Cluster_1", Content = "Cluster 1 content" };

        _mockWikiGenerationService
            .Setup(s => s.GenerateEnhancedPageAsync(
                It.Is<ModuleNode>(m => m.Id == "cluster_0"),
                It.IsAny<List<WikiPage>?>(),
                It.IsAny<ModulePageContext>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AudienceType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<string>?>()))
            .ReturnsAsync(clusterPage1);

        _mockWikiGenerationService
            .Setup(s => s.GenerateEnhancedPageAsync(
                It.Is<ModuleNode>(m => m.Id == "cluster_1"),
                It.IsAny<List<WikiPage>?>(),
                It.IsAny<ModulePageContext>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AudienceType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<string>?>()))
            .ReturnsAsync(clusterPage2);

        // Mock synthesis for parent (merged from clusters)
        _mockSynthesisService
            .Setup(s => s.SynthesizeParentPageAsync(
                originalModule,
                It.IsAny<List<WikiPage>>(),
                It.IsAny<string>(),
                It.IsAny<AudienceType>(),
                true, // THIS IS THE CRITICAL PARAMETER - mergeChildContent should be TRUE
                It.IsAny<SynthesisStrategy?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WikiPage { Id = "page_original", Title = "LargeModule" });

        _mockJudgeService.Setup(s => s.EvaluateRequirementsAsync(
                It.IsAny<List<RubricRequirement>>(),
                It.IsAny<WikiStructure>(),
                It.IsAny<List<string>>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RequirementAssessment>());

        // Act
        var result = await _orchestrator.GenerateAdvancedWikiAsync(repoPath, repoInfo);

        // Assert - VERIFY that SynthesizeParentPageAsync was called with mergeChildContent: TRUE
        _mockSynthesisService.Verify(
            s => s.SynthesizeParentPageAsync(
                It.Is<ModuleNode>(m => m.Id == "original_module"),
                It.Is<List<WikiPage>>(pages => pages.Count == 2), // Should have found 2 cluster pages
                It.IsAny<string>(),
                It.IsAny<AudienceType>(),
                true, // mergeChildContent MUST be true for cluster synthesis
                It.IsAny<SynthesisStrategy?>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "SynthesizeParentPageAsync should be called with mergeChildContent=true when module is delegated into clusters");
    }
}
