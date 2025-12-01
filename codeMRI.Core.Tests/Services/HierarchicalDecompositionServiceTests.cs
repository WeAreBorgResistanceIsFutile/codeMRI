using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace codeMRI.Core.Tests.Services
{
    public class HierarchicalDecompositionServiceTests
    {
        private readonly Mock<ILogger<HierarchicalDecompositionService>> _loggerMock;
        private readonly Mock<IEnhancedDependencyGraphService> _graphServiceMock;
        private readonly Mock<IArchitecturalPatternService> _patternServiceMock;
        private readonly HierarchicalDecompositionService _service;

        public HierarchicalDecompositionServiceTests()
        {
            _loggerMock = new Mock<ILogger<HierarchicalDecompositionService>>();
            _graphServiceMock = new Mock<IEnhancedDependencyGraphService>();
            _patternServiceMock = new Mock<IArchitecturalPatternService>();
            
            _service = new HierarchicalDecompositionService(
                _loggerMock.Object,
                _graphServiceMock.Object,
                _patternServiceMock.Object);
        }

        [Fact]
        public async Task DecomposeHierarchicallyAsync_ShouldCreateModules_BasedOnArchitectureAndLouvain()
        {
            // Arrange
            var graph = new EnhancedDependencyGraph();
            graph.AddNode("Controller1", new NodeMetadata { Type = "Controller", EstimatedTokens = 100 });
            graph.AddNode("Service1", new NodeMetadata { Type = "Service", EstimatedTokens = 100 });
            graph.AddNode("Helper1", new NodeMetadata { Type = "Helper", EstimatedTokens = 50 });
            
            // Helper1 depends on nothing, Controller1 -> Service1
            graph.AddEdge("Controller1", "Service1", EdgeType.Dependency, 1.0);

            _graphServiceMock.Setup(x => x.GetComponentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CodeComponent>());
            _graphServiceMock.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(graph);

            _patternServiceMock.Setup(x => x.DetermineLayer(It.Is<GraphNode>(n => n.ComponentId == "Controller1")))
                .Returns(ArchitecturalLayerType.Presentation);
            _patternServiceMock.Setup(x => x.DetermineLayer(It.Is<GraphNode>(n => n.ComponentId == "Service1")))
                .Returns(ArchitecturalLayerType.Application);
            _patternServiceMock.Setup(x => x.DetermineLayer(It.Is<GraphNode>(n => n.ComponentId == "Helper1")))
                .Returns(ArchitecturalLayerType.Unknown); // Will trigger Louvain for Helper1

            _patternServiceMock.Setup(x => x.RecognizePattern(It.IsAny<ModuleNode>(), It.IsAny<EnhancedDependencyGraph>()))
                .Returns(new ArchitecturalPattern { Type = ArchitecturalPatternType.Unknown });

            // Act
            var result = await _service.DecomposeHierarchicallyAsync("repo/path");

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Nodes);
            
            // We expect modules for Presentation, Application, and a cluster for Helper1
            bool hasPresentation = false;
            bool hasApplication = false;
            bool hasCluster = false;

            foreach(var node in result.Nodes.Values)
            {
                if (node.Name == "Presentation") hasPresentation = true;
                if (node.Name == "Application") hasApplication = true;
                if (node.Name.StartsWith("Component_")) hasCluster = true;
            }

            Assert.True(hasPresentation, "Should have Presentation module");
            Assert.True(hasApplication, "Should have Application module");
            Assert.True(hasCluster, "Should have clustered module for unknown layer");
        }

        [Fact]
        public async Task DecomposeHierarchicallyAsync_ShouldCalculateQualityMetrics()
        {
            // Arrange
            var graph = new EnhancedDependencyGraph();
            graph.AddNode("CompA", new NodeMetadata { EstimatedTokens = 100 });
            graph.AddNode("CompB", new NodeMetadata { EstimatedTokens = 100 });
            graph.AddEdge("CompA", "CompB", EdgeType.Dependency, 1.0);

            _graphServiceMock.Setup(x => x.GetComponentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CodeComponent>());
            _graphServiceMock.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(graph);

            _patternServiceMock.Setup(x => x.DetermineLayer(It.IsAny<GraphNode>()))
                .Returns(ArchitecturalLayerType.Unknown); // Force Louvain

             _patternServiceMock.Setup(x => x.RecognizePattern(It.IsAny<ModuleNode>(), It.IsAny<EnhancedDependencyGraph>()))
                .Returns(new ArchitecturalPattern { Type = ArchitecturalPatternType.Unknown });

            // Act
            var result = await _service.DecomposeHierarchicallyAsync("repo/path");

            // Assert
            foreach(var node in result.Nodes.Values)
            {
                if (node.Id == "root") continue;
                Assert.NotNull(node.QualityMetrics);
                // Just check if metrics were touched (default is 0, but we initialize them)
                // Cohesion/Coupling might be 0 or 1 depending on clustering
            }
        }
    }
}
