using codeMRI.Core.Interfaces;
using NUnit.Framework;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class HierarchicalDecompositionLanguageTests
{
    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<HierarchicalDecompositionService>>();
        _graphServiceMock = new Mock<IEnhancedDependencyGraphService>();
        _patternServiceMock = new Mock<IArchitecturalPatternService>();
        _progressServiceMock = new Mock<IProgressService>();

        _progressServiceMock
            .Setup(p => p.WithScalingAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Func<Task>>()))
            .Returns<double, double, Func<Task>>(async (start, width, op) => await op());

        _service = new HierarchicalDecompositionService(
            _loggerMock.Object,
            _graphServiceMock.Object,
            _patternServiceMock.Object,
            _progressServiceMock.Object);

        // Default Pattern Service Setup
        _patternServiceMock.Setup(x => x.DetermineLayer(It.IsAny<GraphNode>()))
            .Returns(ArchitecturalLayerType.Unknown);
        _patternServiceMock.Setup(x => x.RecognizePattern(It.IsAny<ModuleNode>(), It.IsAny<EnhancedDependencyGraph>()))
            .Returns(new ArchitecturalPattern { Type = ArchitecturalPatternType.Unknown });
    }

    private Mock<ILogger<HierarchicalDecompositionService>> _loggerMock;
    private Mock<IEnhancedDependencyGraphService> _graphServiceMock;
    private Mock<IArchitecturalPatternService> _patternServiceMock;
    private Mock<IProgressService> _progressServiceMock;
    private HierarchicalDecompositionService _service;

    [Test]
    [TestCase("Program.cs", "public static void Main(string[] args)", "C#")]
    [TestCase("Main.java", "public static void main(String[] args)", "Java")]
    [TestCase("app.py", "if __name__ == \"__main__\":", "Python")]
    [TestCase("index.js", "app.listen(3000)", "JavaScript")]
    [TestCase("main.c", "int main()", "C")]
    [TestCase("main.cpp", "int main()", "C++")]
    [TestCase("server.ts", "bootstrap()", "TypeScript")]
    public async Task DecomposeHierarchicallyAsync_ShouldIdentifyEntryPoints_ForSupportedLanguages(
        string fileName, string contentSnippet, string language)
    {
        // Arrange
        var graph = new EnhancedDependencyGraph();
        var metadata = new NodeMetadata
        {
            Type = "File",
            Language = language,
            FilePath = fileName,
            ContentSnippet = contentSnippet // We assume we'll add this field or similar logic
        };

        graph.AddNode(fileName, metadata);

        _graphServiceMock.Setup(x => x.GetComponentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CodeComponent>());
        _graphServiceMock.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);
        _graphServiceMock.Setup(x =>
                x.AnalyzeGraphAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphAnalysisResult());

        // Act
        var result = await _service.DecomposeHierarchicallyAsync("test/repo", null!);

        // Assert
        var entryNode = result.Nodes.Values.FirstOrDefault(n => n.Components.Contains(fileName));
        Assert.That(entryNode, Is.Not.Null);

        // Check if it was marked as containing an entry point in metadata
        Assert.That(entryNode.Metadata.ContainsKey("HasEntryPoint"), Is.True, "Metadata should contain HasEntryPoint");
        Assert.That(entryNode.Metadata["HasEntryPoint"], Is.EqualTo("true"),
            $"File {fileName} should be identified as EntryPoint");
    }

    [Test]
    public async Task DecomposeHierarchicallyAsync_ShouldHandleMixedLanguageRepositories()
    {
        // Arrange
        var graph = new EnhancedDependencyGraph();

        // C++ Core
        graph.AddNode("core.cpp",
            new NodeMetadata { Language = "C++", FilePath = "core.cpp", ContentSnippet = "int main() {}" });

        // Python Wrapper
        graph.AddNode("wrapper.py",
            new NodeMetadata { Language = "Python", FilePath = "wrapper.py", ContentSnippet = "import core" });

        // Dependency
        graph.AddEdge("wrapper.py", "core.cpp", EdgeType.Dependency, 1.0);

        _graphServiceMock.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);
        _graphServiceMock.Setup(x =>
                x.AnalyzeGraphAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphAnalysisResult());

        // Act
        var result = await _service.DecomposeHierarchicallyAsync("test/repo", null!);

        // Assert
        var cppNode = result.Nodes.Values.First(n => n.Components.Contains("core.cpp"));
        var pyNode = result.Nodes.Values.First(n => n.Components.Contains("wrapper.py"));

        // Check Entry Point detection
        Assert.That(cppNode.Metadata.GetValueOrDefault("HasEntryPoint"), Is.EqualTo("true"));

        // Python wrapper might not be an entry point if it just imports, but let's check structure
        Assert.That(result.Nodes.Count, Is.GreaterThanOrEqualTo(1));
    }
}