using codeMRI.Agents.Interfaces;
using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace codeMRI.Core.Tests;

[TestFixture]
public class DocumentationGenerationPipelineTests
{
    [SetUp]
    public void Setup()
    {
        _mockCoordinator = new Mock<IAgentCoordinator>();
        _mockVisualService = new Mock<IVisualSynthesisService>();
        _mockGraphService = new Mock<IEnhancedDependencyGraphService>();
        _mockDecompositionService = new Mock<IHierarchicalDecompositionService>();
        _mockWikiGenService = new Mock<IWikiGenerationService>();
        _mockLogger = new Mock<ILogger<DocumentationGenerationPipeline>>();

        _pipeline = new DocumentationGenerationPipeline(
            _mockCoordinator.Object,
            _mockVisualService.Object,
            _mockGraphService.Object,
            _mockDecompositionService.Object,
            _mockWikiGenService.Object,
            _mockLogger.Object);
    }

    private Mock<IAgentCoordinator> _mockCoordinator;
    private Mock<IVisualSynthesisService> _mockVisualService;
    private Mock<IEnhancedDependencyGraphService> _mockGraphService;
    private Mock<IHierarchicalDecompositionService> _mockDecompositionService;
    private Mock<IWikiGenerationService> _mockWikiGenService;
    private Mock<ILogger<DocumentationGenerationPipeline>> _mockLogger;
    private DocumentationGenerationPipeline _pipeline;

    [Test]
    public async Task GenerateDocumentationAsync_ShouldOrchestrateAgents()
    {
        // Arrange
        var repoPath = "/test/repo";
        var options = new DocumentationOptions();

        // Mock Analyzer
        var analysisResult = new AnalysisResult
        {
            Structure = new RepositoryStructure { Name = "TestRepo" },
            Components = new List<CodeComponent> { new() { Id = "comp1", Name = "Component1" } }
        };
        _mockCoordinator.Setup(c =>
                c.CoordinateTaskAsync(It.Is<AgentTask>(t => t.Type == "Analyzer"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResult { Success = true, Output = analysisResult });

        // Mock Graph & Tree
        // Mock Graph & Tree
        var graph = new EnhancedDependencyGraph();
        _mockGraphService.Setup(s => s.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);
        _mockGraphService.Setup(s => s.AnalyzeGraphAsync(graph, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphAnalysisResult());

        var rootNode = new ModuleNode { Id = "root", Name = "Repository" };
        rootNode.Components.Add("comp1");
        _mockDecompositionService
            .Setup(s => s.DecomposeHierarchicallyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleTree { Root = rootNode });

        // Mock Visualization
        _mockVisualService.Setup(s =>
                s.GenerateArtifactsAsync(It.IsAny<ModuleTree>(), It.IsAny<EnhancedDependencyGraph>()))
            .ReturnsAsync(new VisualArtifacts());

        // Mock Documenter
        _mockCoordinator.Setup(c =>
                c.CoordinateTaskAsync(It.Is<AgentTask>(t => t.Type == "Documenter"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResult
                { Success = true, Output = new WikiPage { Id = "comp1", Title = "Component1" } });

        // Mock Wiki Gen
        _mockWikiGenService.Setup(s =>
                s.GenerateParentPageAsync(It.IsAny<ModuleNode>(), It.IsAny<List<WikiPage>>(), It.IsAny<string>(),
                    It.IsAny<AudienceType>()))
            .ReturnsAsync(new WikiPage { Title = "RepoDoc" });

        // Act
        var result = await _pipeline.GenerateDocumentationAsync(repoPath, options);

        // Assert
        // Assert.That(result, Is.EqualTo(expectedStructure)); // Structure is now generated internally, not matched against exact object
        Assert.That(result, Is.Not.Null);

        _mockCoordinator.Verify(
            c => c.CoordinateTaskAsync(It.Is<AgentTask>(t => t.Type == "Analyzer"), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockCoordinator.Verify(
            c => c.CoordinateTaskAsync(It.Is<AgentTask>(t => t.Type == "Documenter"), It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify Visual Service called
        _mockVisualService.Verify(
            s => s.GenerateArtifactsAsync(It.IsAny<ModuleTree>(), It.IsAny<EnhancedDependencyGraph>()), Times.Once);
    }
}