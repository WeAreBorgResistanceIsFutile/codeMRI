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
public class GenerateAllPagesTests
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
    private Mock<IBenchmarkingService> _mockBenchmarkingService;

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
        _mockBenchmarkingService = new Mock<IBenchmarkingService>();

        _mockDelegationService
            .Setup(d => d.EvaluateDelegation(It.IsAny<ModuleNode>(), It.IsAny<EnhancedDependencyGraph>(), It.IsAny<int>()))
            .Returns(DelegationDecision.NoDelegation());

        _mockProgressService
            .Setup(p => p.WithScalingAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Func<Task>>()))
            .Returns<double, double, Func<Task>>(async (start, width, op) => await op());

        _mockGraphService
            .Setup(g => g.GetComponentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CodeComponent>());
        _mockGraphService
            .Setup(g => g.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnhancedDependencyGraph());

        _mockRubricService
            .Setup(s => s.GenerateRubricAsync(It.IsAny<WikiStructure>(), It.IsAny<RepositoryInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationRubric());

        _mockJudgeService
            .Setup(s => s.EvaluateRequirementsAsync(It.IsAny<List<RubricRequirement>>(), It.IsAny<WikiStructure>(),
                It.IsAny<List<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RequirementAssessment>());

        _mockWikiGenerationService
            .Setup(s => s.GenerateEnhancedPageAsync(It.IsAny<ModuleNode>(), It.IsAny<List<WikiPage>?>(),
                It.IsAny<ModulePageContext>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<AudienceType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<string>?>()))
            .ReturnsAsync(new WikiPage { Id = "mock", Title = "Mock" });

        _mockSynthesisService
            .Setup(s => s.SynthesizeParentPageAsync(It.IsAny<ModuleNode>(), It.IsAny<List<WikiPage>>(), It.IsAny<string>(),
                It.IsAny<AudienceType>(), It.IsAny<bool>(), It.IsAny<SynthesisStrategy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WikiPage { Id = "parent", Title = "Parent" });
    }

    private CodeWikiOrchestrator CreateOrchestrator(CodeWikiOptions options)
    {
        var mockOptions = new Mock<IOptions<CodeWikiOptions>>();
        mockOptions.Setup(o => o.Value).Returns(options);

        return new CodeWikiOrchestrator(
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
            _mockLogger.Object,
            "default",
            _mockBenchmarkingService.Object
        );
    }

    [Test]
    public async Task GenerateAdvancedWikiAsync_WhenGenerateAllPagesIsDefault_ShouldGenerateVisibleAndInvisiblePages()
    {
        // Arrange
        var options = new CodeWikiOptions { MaxDegreeOfParallelism = 1 }; // GenerateAllPages is missing now, but we want to test current behavior
        var orchestrator = CreateOrchestrator(options);

        var invisibleModule = new ModuleNode { Id = "invisible", Name = "Invisible", IsLeaf = true };
        var visibleModule = new ModuleNode { Id = "visible", Name = "Visible", IsLeaf = true };
        
        var root = new ModuleNode { Id = "root", Name = "Root", IsLeaf = false };
        root.AddChild(invisibleModule);
        root.AddChild(visibleModule);

        _mockDecompositionService
            .Setup(s => s.DecomposeHierarchicallyAsync(It.IsAny<string>(), It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleTree { Root = root });

        // Setup structure where only 'visible' is mapped
        _mockNavigationService
            .Setup(n => n.GenerateDocumentationStructureAsync(It.IsAny<ModuleTree>(), It.IsAny<RepositoryInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WikiStructure
            {
                Sections = new List<WikiSection>
                {
                    new WikiSection { Id = "sec1", Title = "Section 1" }
                },
                ModuleToSectionMap = new Dictionary<string, string>
                {
                    { "visible", "sec1" }
                }
            });

        // Act
        await orchestrator.GenerateAdvancedWikiAsync("/test", new RepositoryInfo());

        // Assert
        // Current behavior: both are generated
        _mockWikiGenerationService.Verify(s => s.GenerateEnhancedPageAsync(It.Is<ModuleNode>(m => m.Id == "visible"), It.IsAny<List<WikiPage>?>(), It.IsAny<ModulePageContext>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AudienceType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>()), Times.Once);
        _mockWikiGenerationService.Verify(s => s.GenerateEnhancedPageAsync(It.Is<ModuleNode>(m => m.Id == "invisible"), It.IsAny<List<WikiPage>?>(), It.IsAny<ModulePageContext>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AudienceType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>()), Times.Once);
    }

    [Test]
    public async Task GenerateAdvancedWikiAsync_WhenGenerateAllPagesIsFalse_ShouldSkipInvisiblePages()
    {
        // Arrange
        var options = new CodeWikiOptions { MaxDegreeOfParallelism = 1, GenerateAllPages = false };
        var orchestrator = CreateOrchestrator(options);

        var invisibleModule = new ModuleNode { Id = "invisible", Name = "Invisible", IsLeaf = true };
        var visibleModule = new ModuleNode { Id = "visible", Name = "Visible", IsLeaf = true };
        
        var root = new ModuleNode { Id = "root", Name = "Root", IsLeaf = false };
        root.AddChild(invisibleModule);
        root.AddChild(visibleModule);

        _mockDecompositionService
            .Setup(s => s.DecomposeHierarchicallyAsync(It.IsAny<string>(), It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleTree { Root = root });

        // Setup structure where only 'visible' is mapped
        _mockNavigationService
            .Setup(n => n.GenerateDocumentationStructureAsync(It.IsAny<ModuleTree>(), It.IsAny<RepositoryInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WikiStructure
            {
                Sections = new List<WikiSection>
                {
                    new WikiSection { Id = "sec1", Title = "Section 1" }
                },
                ModuleToSectionMap = new Dictionary<string, string>
                {
                    { "visible", "sec1" }
                }
            });

        // Act
        await orchestrator.GenerateAdvancedWikiAsync("/test", new RepositoryInfo());

        // Assert
        // 'visible' should be generated
        _mockWikiGenerationService.Verify(s => s.GenerateEnhancedPageAsync(It.Is<ModuleNode>(m => m.Id == "visible"), It.IsAny<List<WikiPage>?>(), It.IsAny<ModulePageContext>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AudienceType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>()), Times.Once);
        
        // 'invisible' should be skipped
        _mockWikiGenerationService.Verify(s => s.GenerateEnhancedPageAsync(It.Is<ModuleNode>(m => m.Id == "invisible"), It.IsAny<List<WikiPage>?>(), It.IsAny<ModulePageContext>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AudienceType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>()), Times.Never);
    }
}
