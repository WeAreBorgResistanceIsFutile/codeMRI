using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class HierarchicalDecompositionProgressTests
{
    private Mock<ILogger<HierarchicalDecompositionService>> _loggerMock;
    private Mock<IEnhancedDependencyGraphService> _graphServiceMock;
    private Mock<IArchitecturalPatternService> _patternServiceMock;

    private IProgressService _progressService;
    private HierarchicalDecompositionService _service;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<HierarchicalDecompositionService>>();
        _graphServiceMock = new Mock<IEnhancedDependencyGraphService>();
        _patternServiceMock = new Mock<IArchitecturalPatternService>();

        _progressService = new ProgressService();

        _service = new HierarchicalDecompositionService(
            _loggerMock.Object,
            _graphServiceMock.Object,
            _patternServiceMock.Object,
            _progressService);
            
         _patternServiceMock.Setup(x => x.DetermineLayer(It.IsAny<GraphNode>()))
            .Returns(ArchitecturalLayerType.Unknown);
         _patternServiceMock.Setup(x => x.RecognizePattern(It.IsAny<ModuleNode>(), It.IsAny<EnhancedDependencyGraph>()))
            .Returns(new ArchitecturalPattern { Type = ArchitecturalPatternType.Unknown });
    }

    [Test]
    public async Task DecomposeHierarchicallyAsync_ShouldReportProgress_AcrossIdentificationAndGraphBuilding()
    {
        // Arrange
        var capturedProgress = new List<ProgressInfo>();
        // Using real service instance from Setup

        var components = new List<CodeComponent> { new() { Id = "c1" } };
        var graph = new EnhancedDependencyGraph();
        graph.AddNode("c1", new NodeMetadata());

        // Setup GetComponentsAsync to report progress
        _graphServiceMock.Setup(x => x.GetComponentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((path, token) => 
            {
                // Simulate identification progress 0 -> 100
                _progressService.Report(new ProgressInfo { Phase = "Identification", Percentage = 0, Message = "Start ID" });
                _progressService.Report(new ProgressInfo { Phase = "Identification", Percentage = 50, Message = "Half ID" });
                _progressService.Report(new ProgressInfo { Phase = "Identification", Percentage = 100, Message = "End ID" });
            })
            .ReturnsAsync(components);

        // Setup BuildGraphAsync to report progress
        _graphServiceMock.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
             .Callback<List<CodeComponent>, CancellationToken>((comps, token) => 
            {
                // Simulate build progress 0 -> 100
                _progressService.Report(new ProgressInfo { Phase = "Graph Construction", Percentage = 0, Message = "Start Build" });
                _progressService.Report(new ProgressInfo { Phase = "Graph Construction", Percentage = 50, Message = "Half Build" });
                _progressService.Report(new ProgressInfo { Phase = "Graph Construction", Percentage = 100, Message = "End Build" });
            })
            .ReturnsAsync(graph);
            
        _graphServiceMock.Setup(x => x.AnalyzeGraphAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphAnalysisResult());

        // Act
        // Act
        _progressService.SetHandler(p => capturedProgress.Add(p));
        await _service.DecomposeHierarchicallyAsync("test/repo");

        // Assert
        Assert.That(capturedProgress, Is.Not.Empty);
        
        // Verify Identification Phase (Scaled 0-20%)
        Assert.That(capturedProgress.Any(p => p.Phase == "Identification" && p.Percentage == 0), Is.True);
        Assert.That(capturedProgress.Any(p => p.Phase == "Identification" && p.Percentage == 10), Is.True); // 50 * 0.2
        Assert.That(capturedProgress.Any(p => p.Phase == "Identification" && p.Percentage == 20), Is.True); // 100 * 0.2
        
        // Verify Graph Construction Phase (Scaled 20-100% -> Start at 20, Max 100)
        // Wait, range is 20 + (pct * 0.8).
        // 0 -> 20 + 0 = 20
        // 50 -> 20 + 40 = 60
        // 100 -> 20 + 80 = 100
        
        Assert.That(capturedProgress.Any(p => p.Phase == "Graph Construction" && p.Percentage == 20), Is.True);
        Assert.That(capturedProgress.Any(p => p.Phase == "Graph Construction" && p.Percentage == 60), Is.True);
        Assert.That(capturedProgress.Any(p => p.Phase == "Graph Construction" && p.Percentage == 100), Is.True);
    }
}
