using codeMRI.Agents.Interfaces;
using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

// Added
// Added

namespace codeMRI.Agents.Tests.Unit;

[TestFixture]
public class DocumentationGenerationPipelineTests
{
    [SetUp]
    public void Setup()
    {
        _mockCoordinator = new Mock<IAgentCoordinator>();
        _mockVisualSynthesis = new Mock<IVisualSynthesisService>();
        _mockGraphService = new Mock<IEnhancedDependencyGraphService>();
        _mockDecompositionService = new Mock<IHierarchicalDecompositionService>();
        _mockWikiGenService = new Mock<IWikiGenerationService>();
        _mockLogger = new Mock<ILogger<DocumentationGenerationPipeline>>();

        _pipeline = new DocumentationGenerationPipeline(
            _mockCoordinator.Object,
            _mockVisualSynthesis.Object,
            _mockGraphService.Object,
            _mockDecompositionService.Object,
            _mockWikiGenService.Object,
            _mockLogger.Object
        );
    }

    private Mock<IAgentCoordinator> _mockCoordinator = null!;
    private Mock<IVisualSynthesisService> _mockVisualSynthesis = null!;
    private Mock<IEnhancedDependencyGraphService> _mockGraphService = null!;
    private Mock<IHierarchicalDecompositionService> _mockDecompositionService = null!;
    private Mock<IWikiGenerationService> _mockWikiGenService = null!;
    private Mock<ILogger<DocumentationGenerationPipeline>> _mockLogger = null!;
    private DocumentationGenerationPipeline _pipeline = null!;

    [Test]
    public async Task GenerateDocumentationAsync_ShouldUsePostOrderTraversal_AndSynthesizeParentPages()
    {
        // Arrange
        var rootNode = new ModuleNode { Id = "root", Name = "Root" };
        var childNode = new ModuleNode { Id = "child", Name = "Child" };
        rootNode.AddChild(childNode);
        childNode.Components.Add("ComponentA");

        var components = new List<CodeComponent>
        {
            new() { Id = "ComponentA", Name = "ComponentA" }
        };

        // Mock Analysis (Step 1)
        _mockCoordinator.Setup(x =>
                x.CoordinateTaskAsync(It.Is<AgentTask>(t => t.Type == "Analyzer"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResult
            {
                Success = true,
                Output = new AnalysisResult { Components = components, Structure = new RepositoryStructure() }
            });

        // Mock Services (Step 1.5)
        _mockGraphService.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnhancedDependencyGraph());

        _mockDecompositionService
            .Setup(x => x.DecomposeHierarchicallyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleTree { Root = rootNode }); // Should probably use logic to return our tree

        _mockVisualSynthesis.Setup(x =>
                x.GenerateArtifactsAsync(It.IsAny<ModuleTree>(), It.IsAny<EnhancedDependencyGraph>()))
            .ReturnsAsync(new VisualArtifacts());

        // Mock Documenter (Step 2 - Child)
        var childWikiPage = new WikiPage { Title = "ChildDoc", Content = "Summary of Child" };
        _mockCoordinator.Setup(x =>
                x.CoordinateTaskAsync(
                    It.Is<AgentTask>(t => t.Type == "Documenter" && t.Payload != null && ((CodeComponent)t.Payload).Name == "ComponentA"),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResult { Success = true, Output = childWikiPage });

        // Mock Wiki Gen (Step 3 - Parent)
        var parentWikiPage = new WikiPage { Title = "RootDoc", Content = "Summary of Root" };

        // Default setup for any call
        _mockWikiGenService.Setup(x => x.GenerateParentPageAsync(
                It.IsAny<ModuleNode>(),
                It.IsAny<List<WikiPage>>(),
                It.IsAny<string>(),
                It.IsAny<AudienceType>()))
            .ReturnsAsync(new WikiPage { Title = "GenericDoc" });

        // Specific setup for Root verification
        _mockWikiGenService.Setup(x => x.GenerateParentPageAsync(
                It.Is<ModuleNode>(n => n.Name == "Root"),
                It.Is<List<WikiPage>>(l => l.Any(p => p.Title == "ChildDoc")), // Ensure child page is passed
                It.IsAny<string>(),
                It.IsAny<AudienceType>()))
            .ReturnsAsync(parentWikiPage);

        // Mock Final Synthesis (Step 4)
        // _mockCoordinator.Setup(x => x.CoordinateTaskAsync(It.Is<AgentTask>(t => t.Type == "Synthesizer"), It.IsAny<CancellationToken>()))
        //    .ReturnsAsync(new AgentResponse { Success = true, Output = new WikiStructure() });
        // Note: If we change pipeline to use WikiGenService for structure, we might remove Synthesizer agent or adapt it.
        // For now, let's assume the pipeline still returns a WikiStructure at the end.

        // Act
        var result = await _pipeline.GenerateDocumentationAsync("dummy/path", new DocumentationOptions());

        // Assert
        // Verify Child Documenter was called
        _mockCoordinator.Verify(
            x => x.CoordinateTaskAsync(It.Is<AgentTask>(t => t.Type == "Documenter"), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        // Verify Parent Generation was called with Child info
        _mockWikiGenService.Verify(x => x.GenerateParentPageAsync(
            It.Is<ModuleNode>(n => n.Name == "Root"),
            It.Is<List<WikiPage>>(l => l.Count > 0),
            It.IsAny<string>(),
            It.IsAny<AudienceType>()), Times.Once);
    }
}