using NUnit.Framework;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class CodeWikiOrchestratorClusterTests
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

        // Default Mocks
        _mockProgressService.Setup(p => p.WithScalingAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Func<Task>>()))
            .Returns<double, double, Func<Task>>(async (start, width, op) => await op());

        var mockOptions = new Mock<IOptions<CodeWikiOptions>>();
        mockOptions.Setup(o => o.Value).Returns(new CodeWikiOptions { MaxDegreeOfParallelism = 1 });
        
         _mockGraphService
            .Setup(g => g.GetComponentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CodeComponent>());
        _mockGraphService
            .Setup(g => g.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnhancedDependencyGraph());

        _mockNavigationService
            .Setup(n => n.GenerateDocumentationStructureAsync(It.IsAny<ModuleTree>(), It.IsAny<RepositoryInfo>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((ModuleTree tree, RepositoryInfo info, CancellationToken _) =>
            {
                // Basic structure: Root -> Sections...
                var structure = new WikiStructure
                {
                    Title = "Test Structure",
                    Sections = new List<WikiSection>(),
                    Pages = new List<WikiPage>(),
                    ModuleToSectionMap = new Dictionary<string, string>()
                };
                
                // Helper to build sections recursively
                void BuildSections(ModuleNode node, WikiSection? parentSection)
                {
                    var sectionId = $"section_{node.Id}";
                    var section = new WikiSection 
                    { 
                        Id = sectionId, 
                        Title = node.Name, 
                        ModuleIds = new List<string> { node.Id },
                        PageRefs = new List<string>()
                    };
                    
                    structure.ModuleToSectionMap[node.Id] = sectionId;
                    
                    if (parentSection == null)
                        structure.Sections.Add(section);
                    else
                        parentSection.SubSections.Add(section);
                        
                    foreach(var child in node.Children)
                        BuildSections(child, section);
                }
                
                if (tree.Root != null)
                    BuildSections(tree.Root, null);
                    
                return structure;
            });

        _mockRubricService.Setup(s => s.GenerateRubricAsync(It.IsAny<WikiStructure>(), It.IsAny<RepositoryInfo>(), It.IsAny<CancellationToken>())).ReturnsAsync(new EvaluationRubric());
        _mockJudgeService.Setup(s => s.EvaluateRequirementsAsync(It.IsAny<List<RubricRequirement>>(), It.IsAny<WikiStructure>(), It.IsAny<List<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<RequirementAssessment>());

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
    public async Task GenerateAdvancedWikiAsync_ShouldNotSaveOrNavigateClusterPages()
    {
        // Arrange
        var repoPath = "/test/repo";
        var repoInfo = new RepositoryInfo { Name = "TestRepo" };
        
        // Setup Module Tree with Delegation (Cluster)
        // Root -> Child (Leaf) -> Cluster1 (Leaf, created by delegation)
        
        var clusterNode = new ClusterModuleNode
        {
            Id = "cluster1",
            Name = "Cluster 1",
            IsLeaf = true,
            Level = 2,
            Components = new HashSet<string> { "c1" }
        };

        var childNode = new ModuleNode
        {
            Id = "child",
            Name = "Child Module",
            IsLeaf = true, // Initially leaf, but will delegate
            Level = 1,
            Components = new HashSet<string> { "c1", "c2" }
        };

        var rootNode = new ModuleNode
        {
            Id = "root",
            Name = "Root",
            IsLeaf = false,
            Children = new List<ModuleNode> { childNode }
        };

        // Delegation service logic simulation in Orchestrator:
        // Orchestrator calls EvaluateDelegation.
        // If ShouldDelegate, it calls DelegateModuleAsync which returns sub-modules.
        // It then recursively processes sub-modules.

        _mockDelegationService
            .Setup(d => d.EvaluateDelegation(childNode, It.IsAny<EnhancedDependencyGraph>(), It.IsAny<int>()))
            .Returns(DelegationDecision.ForComplexity(100, 50)); // Trigger delegation for 'child'

        _mockDelegationService
            .Setup(d => d.DelegateModuleAsync(childNode, It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModuleNode> { clusterNode }) // Returns the cluster node
            .Callback<ModuleNode, EnhancedDependencyGraph, CancellationToken>((m, g, c) =>
            {
                m.Children.Add(clusterNode);
                clusterNode.Parent = m;
            });

        _mockDelegationService
            .Setup(d => d.EvaluateDelegation(clusterNode, It.IsAny<EnhancedDependencyGraph>(), It.IsAny<int>()))
            .Returns(DelegationDecision.NoDelegation()); // Cluster itself is not delegated further

        // Mock Decompose
        _mockDecompositionService
            .Setup(s => s.DecomposeHierarchicallyAsync(repoPath, It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleTree { Root = rootNode });

        // Mock Generation Services
        
        // 1. Cluster Page Generation (Should return ClusterWikiPage)
        var clusterPage = new ClusterWikiPage { Id = "cluster1", Title = "Cluster 1", Content = "Cluster Content" };
        
        _mockWikiGenerationService
            .Setup(s => s.GenerateEnhancedPageAsync(clusterNode, It.IsAny<List<WikiPage>?>(), It.IsAny<ModulePageContext>(), 
                It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AudienceType>(), 
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>()))
            .ReturnsAsync(clusterPage); // Return the special type

        // 2. Parent Page Synthesis (Child Module)
        var childPage = new WikiPage { Id = "child", Title = "Child Module", Content = "Child Content" };
        _mockSynthesisService
            .Setup(s => s.SynthesizeParentPageAsync(childNode, It.IsAny<List<WikiPage>>(), It.IsAny<string>(), 
                It.IsAny<AudienceType>(), It.IsAny<bool>(), It.IsAny<SynthesisStrategy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(childPage);
        
        // 3. Root Page Synthesis
        var rootPage = new WikiPage { Id = "root", Title = "Root", Content = "Root Content" };
        _mockSynthesisService
            .Setup(s => s.SynthesizeParentPageAsync(rootNode, It.IsAny<List<WikiPage>>(), It.IsAny<string>(), 
                It.IsAny<AudienceType>(), It.IsAny<bool>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rootPage);

        // Act
        var structure = await _orchestrator.GenerateAdvancedWikiAsync(repoPath, repoInfo);

        // Assert
        
        // 1. Verify SavePageAsync calls
        _mockWikiRepo.Verify(r => r.SavePageAsync(repoPath, rootPage), Times.Once, "Root page should be saved");
        _mockWikiRepo.Verify(r => r.SavePageAsync(repoPath, childPage), Times.Once, "Child page should be saved");
        _mockWikiRepo.Verify(r => r.SavePageAsync(repoPath, clusterPage), Times.Never, "Cluster page should NOT be saved"); // Fails here if not implemented

        // 2. Verify Structure Navigation
        // Check finding section for cluster
        // Using our Mock NavigationService, it replicates the tree structure.
        // However, the Orchestrator logic attempts to add the page to the section.
        // We need to ensure that EVEN IF a section exists (from structure generation), the page is NOT added to it?
        // Or that structure generation shouldn't create a section for it? 
        // NOTE: Structure generation happens BEFORE content generation. The module tree is decomposed first.
        // If delegation happens dynamically inside Orchestrator (it does!), does NavigationService see the new dynamic nodes?
        // Wait, CodeWikiOrchestrator calls `Decompose` first, then `GenerateDocumentationStructure`. 
        // THEN it iterates. 
        // Delegation happens INSIDE `GenerateContentForModulesAsync`, which is AFTER structure generation.
        // So `GenerateDocumentationStructure` ONLY sees the original tree (ChildNode), NOT ClusterNode.
        // So ClusterNode will NOT have a corresponding section in `structure.ModuleToSectionMap`.
        // The Orchestrator has fallback logic: `if (!TryFindMappedSectionId... sectionId = $"section_{module.Id}"`.
        // It then tries `FindSectionById`. If that fails (it should, as no section created), it logs warning.
        // WAIT. If no section exists, it logs "Section ... not found". It doesn't add it.
        // BUT, the user requirement says "exclude from navigation structure".
        // If the section doesn't exist, it's effectively excluded unless Orchestrator creates one?
        // Orchestrator: `if (section != null) ... section.PageRefs.Add(page.Id)`.
        
        // So if NavigationService runs BEFORE delegation, there is NO section for the cluster.
        // BUT, if the orchestrator finds a section (maybe fallback logic matches parent?), it might add it.
        // Let's verify what happens.
        // If `TryFindMappedSectionId` fails, it uses `section_{module.Id}`.
        // `FindSectionById` will fail because `GenerateDocumentationStructure` didn't create it.
        // So strictly speaking, it might already be excluded if no section exists?
        // OR does delegation happen earlier?
        // Orchestrator: Line 93 Decompose. Line 125 GenerateDocumentationStructure. Line 151 GenerateContent.
        // Inside GenerateContent: Line 312 `DelegateModuleAsync`.
        // So delegation adds new nodes to the tree AFTER structure is generated.
        // So structure definitely doesn't have sections for them.
        // So `section` will be null.
        // So `PageRefs.Add` won't happen.
        
        // HOWEVER, the user says "Why are there included in the navigation structure?".
        // If so, my understanding of the flow might be slightly off OR the fallback logic is finding something.
        // Or maybe `DelegateModuleAsync` modifies the tree in a way that `ModuleToSectionMap` (passed by reference?) is updated? No.
        
        // Let's look at `TryFindMappedSectionId`: 
        // It walks UP the parent chain until it finds a mapped section.
        // `clusterNode.Parent` -> `childNode`.
        // `structure.ModuleToSectionMap` HAS entry for `childNode` ("section_child").
        // So `TryFindMappedSectionId` returns "section_child" for `clusterNode`.
        // So `sectionId` becomes "section_child".
        // So `FindSectionById` finds the section for "Child Module".
        // So `section.PageRefs.Add(clusterPage.Id)` happens.
        // So the cluster page IS added to the PARENT'S section.
        // This explains why they appear in navigation (likely intermingled with the parent or as sub-items if UI handles it).
        
        // So the test should assert that:
        // The parent section ("section_child") does NOT contain "cluster1" in PageRefs.

        var allSections = GetAllSections(structure.Sections);
        var childSection = allSections.FirstOrDefault(s => s.Id == "section_child");
        
        Assert.That(childSection, Is.Not.Null, "Child section should exist");
        Assert.That(childSection.PageRefs, Does.Not.Contain("cluster1"), "Cluster page should not be added to parent section");
    }

    private List<WikiSection> GetAllSections(List<WikiSection> sections)
    {
        var result = new List<WikiSection>();
        foreach(var s in sections)
        {
            result.Add(s);
            if (s.SubSections != null)
                result.AddRange(GetAllSections(s.SubSections));
        }
        return result;
    }
}
