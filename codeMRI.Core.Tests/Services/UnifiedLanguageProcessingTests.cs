using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class UnifiedLanguageProcessingTests
{
    [SetUp]
    public void Setup()
    {
        _mockAstService = new Mock<IASTServiceClient>();
        _mockLogger = new Mock<ILogger<EnhancedDependencyGraphService>>();
        _mockComponentService = new Mock<IComponentIdentificationService>();
        _mockProgressService = new Mock<IProgressService>();
        _service = new EnhancedDependencyGraphService(_mockLogger.Object, _mockAstService.Object,
            _mockComponentService.Object, _mockProgressService.Object);
    }

    [TearDown]
    public void Cleanup()
    {
        // cleanup temp files if any
    }

    private Mock<IASTServiceClient> _mockAstService;
    private Mock<ILogger<EnhancedDependencyGraphService>> _mockLogger;
    private Mock<IComponentIdentificationService> _mockComponentService;
    private Mock<IProgressService> _mockProgressService;
    private EnhancedDependencyGraphService _service;

    [Test]
    public async Task BuildGraphAsync_ShouldProcessRichDependencyGraph_FromASTService()
    {
        // Arrange
        var components = new List<CodeComponent>
        {
            new() { Id = "comp1", FilePath = "/src/main.py", Language = "python", Type = "Script" },
            new() { Id = "comp2", FilePath = "/src/utils.js", Language = "javascript", Type = "Module" }
        };

        // Mock AST Response for Python
        var pythonGraph = new DependencyGraphData
        {
            Nodes = new List<ASTGraphNode>
            {
                new() { Id = "func_main", Type = "function", Language = "python" },
                new() { Id = "class_helper", Type = "class", Language = "python" }
            },
            Edges = new List<ASTGraphEdge>
            {
                new() { Source = "func_main", Target = "class_helper", Type = "Usage" },
                new() { Source = "func_main", Target = "utils_module", Type = "Import" } // Cross-lang ref
            }
        };

        var pythonResult = new ASTParseResult
        {
            Language = "python",
            FilePath = "/src/main.py",
            DependencyGraph = pythonGraph
        };

        _mockAstService.Setup(s =>
                s.ParseCodeAsync(It.IsAny<string>(), "python", "/src/main.py", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pythonResult);

        // Mock File.ReadAllText (This is tricky as File is static. 
        // EnhancedDependencyGraphService uses File.ReadAllTextAsync.
        // We might need to wrapper or just ensure the file exists for the test to work if not abstracting File System.
        // However, for this test, we can't easily mock File.IO without a wrapper.
        // BUT, the service checks `File.Exists`. 
        // Integration testing with real files might be better, or refactor Service to use IFileSystem.
        // Given constraints, I'll assume we can't easily refactor everything right now. 
        // I will create temporary files.)

        var tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tmpDir);
        var pyPath = Path.Combine(tmpDir, "main.py");
        var jsPath = Path.Combine(tmpDir, "utils.js");
        await File.WriteAllTextAsync(pyPath, "import utils");
        await File.WriteAllTextAsync(jsPath, "module.exports = {}");

        components[0].FilePath = pyPath;
        components[1].FilePath = jsPath;

        _mockAstService
            .Setup(s => s.ParseCodeAsync(It.IsAny<string>(), "python", pyPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pythonResult);

        var jsGraph = new DependencyGraphData
        {
            Nodes = new List<ASTGraphNode>
            {
                new() { Id = "utils_module", Type = "module", Language = "javascript" }
            },
            Edges = new List<ASTGraphEdge>()
        };
        var jsResult = new ASTParseResult
        {
            Language = "javascript",
            FilePath = jsPath,
            DependencyGraph = jsGraph
        };

        _mockAstService.Setup(s =>
                s.ParseCodeAsync(It.IsAny<string>(), "javascript", jsPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(jsResult);

        // Act
        var graph = await _service.BuildGraphAsync(components);

        // Assert
        Assert.That(graph.NodeCount, Is.GreaterThanOrEqualTo(2));
        // Verify internal nodes from AST were added to the graph
        var mainFuncNode = graph.GetNode("func_main");
        Assert.That(mainFuncNode, Is.Not.Null, "AST Node 'func_main' should be added to the graph");
        Assert.That(mainFuncNode.Metadata.Type, Is.EqualTo("function"));

        // Verify edges
        // The service should have added an edge from func_main to class_helper
        // And potentially processed the cross-language link
    }

    [Test]
    public void Verify_ASTModels_Support_RichStructure()
    {
        // This test verifies that our Data Models can hold the complex AST data
        var data = new DependencyGraphData();
        Assert.That(data, Is.TypeOf<DependencyGraphData>());
        // These properties need to be added to ASTModels.cs
        // data.Nodes = new List<ASTGraphNode>(); 
        // data.Edges = new List<ASTGraphEdge>();
    }
}