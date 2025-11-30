using Microsoft.Extensions.Logging;
using Moq;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Services;

namespace codeMRI.Core.Tests.Services
{
    public class EnhancedDependencyGraphServiceTests
    {
        private readonly Mock<ILogger<EnhancedDependencyGraphService>> _mockLogger;
        private readonly EnhancedDependencyGraphService _service;
        
        public EnhancedDependencyGraphServiceTests()
        {
            _mockLogger = new Mock<ILogger<EnhancedDependencyGraphService>>();
            _service = new EnhancedDependencyGraphService(_mockLogger.Object);
        }
        
        [Fact]
        public async Task BuildGraphAsync_WithSimpleComponents_ShouldCreateValidGraph()
        {
            // Arrange
            var components = CreateSimpleTestComponents();
            
            // Act
            var graph = await _service.BuildGraphAsync(components);
            
            // Assert
            Assert.NotNull(graph);
            Assert.Equal(3, graph.NodeCount);
            Assert.True(graph.EdgeCount >= 2);
        }
        
        [Fact]
        public async Task BuildGraphAsync_WithLargeDataset_ShouldCompleteEfficiently()
        {
            // Arrange
            var components = CreateLargeTestComponents(1000);
            var startTime = DateTime.UtcNow;
            
            // Act
            var graph = await _service.BuildGraphAsync(components);
            var endTime = DateTime.UtcNow;
            var duration = endTime - startTime;
            
            // Assert
            Assert.NotNull(graph);
            Assert.Equal(1000, graph.NodeCount);
            Assert.True(duration.TotalSeconds < 5, $"Graph construction took {duration.TotalSeconds}s, should be under 5s");
        }
        
        [Fact]
        public async Task AnalyzeGraphAsync_WithSimpleGraph_ShouldCalculatePageRank()
        {
            // Arrange
            var components = CreateSimpleTestComponents();
            var graph = await _service.BuildGraphAsync(components);
            
            // Act
            var analysis = await _service.AnalyzeGraphAsync(graph);
            
            // Assert
            Assert.NotNull(analysis.PageRankScores);
            Assert.Equal(3, analysis.PageRankScores.Count);
            Assert.All(analysis.PageRankScores.Values, score => Assert.True(score > 0 && score <= 1));
        }
        
        [Fact]
        public async Task IdentifyEntryPointsAsync_WithSimpleGraph_ShouldFindZeroInDegreeNodes()
        {
            // Arrange
            var components = CreateSimpleTestComponents();
            var graph = await _service.BuildGraphAsync(components);
            
            // Debug: Let's see what we have
            var allNodes = graph.GetNodes().ToList();
            var zeroInDegreeNodes = graph.GetZeroInDegreeNodes().ToList();
            
            // Act
            var entryPoints = await _service.IdentifyEntryPointsAsync(graph);
            
            // Assert
            Assert.NotNull(entryPoints);
            Assert.Contains("MainController", entryPoints);
        }
        
        [Fact]
        public async Task CalculateImportanceScoresAsync_ShouldReturnPositiveScores()
        {
            // Arrange
            var components = CreateSimpleTestComponents();
            var graph = await _service.BuildGraphAsync(components);
            
            // Act
            var importanceScores = await _service.CalculateImportanceScoresAsync(graph);
            
            // Assert
            Assert.NotNull(importanceScores);
            Assert.Equal(3, importanceScores.Count);
            Assert.All(importanceScores.Values, score => Assert.True(score > 0));
        }
        
        [Fact]
        public async Task BuildGraphAsync_WithCancellationToken_ShouldRespectCancellation()
        {
            // Arrange
            var components = CreateLargeTestComponents(10000);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            
            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _service.BuildGraphAsync(components, cts.Token));
        }
        
        [Fact]
        public async Task BuildGraphAsync_ShouldCalculateComplexityMetrics()
        {
            // Arrange
            var components = CreateComplexTestComponents();
            
            // Act
            var graph = await _service.BuildGraphAsync(components);
            
            // Assert
            var node = graph.GetNode("ComplexComponent");
            Assert.NotNull(node);
            Assert.True(node.Metadata.CyclomaticComplexity > 0);
            Assert.True(node.Metadata.NestingDepth >= 0);
            Assert.True(node.Metadata.FanIn >= 0);
            Assert.True(node.Metadata.FanOut >= 0);
        }
        
        [Fact]
        public async Task EstimateTokensAsync_ShouldReturnReasonableEstimates()
        {
            // Arrange
            var components = CreateSimpleTestComponents();
            var graph = await _service.BuildGraphAsync(components);
            
            // Act
            var tokenEstimates = await _service.EstimateTokensAsync(graph);
            
            // Assert
            Assert.NotNull(tokenEstimates);
            Assert.Equal(3, tokenEstimates.Count);
            Assert.All(tokenEstimates.Values, tokens => Assert.True(tokens > 0));
        }
        
        [Fact]
        public async Task DecomposeHierarchicallyAsync_ShouldCreateModuleTree()
        {
            // Arrange
            var components = CreateLargeTestComponents(100);
            var graph = await _service.BuildGraphAsync(components);
            
            // Act
            var moduleTree = await _service.DecomposeHierarchicallyAsync(graph, maxTokensPerModule: 1000);
            
            // Assert
            Assert.NotNull(moduleTree);
            Assert.NotNull(moduleTree.Root);
            Assert.True(moduleTree.GetAllLeaves().Count > 0);
            
            // All leaves should be under token threshold
            foreach (var leaf in moduleTree.GetAllLeaves())
            {
                Assert.True(leaf.EstimatedTokens <= 1000, 
                    $"Leaf {leaf.Id} has {leaf.EstimatedTokens} tokens, exceeding threshold of 1000");
            }
        }
        
        [Fact]
        public async Task PartitionByDirectoryStructure_ShouldGroupByPath()
        {
            // Arrange
            var components = CreateDirectoryBasedComponents();
            var graph = await _service.BuildGraphAsync(components);
            
            // Act
            var partitions = await _service.PartitionByDirectoryStructureAsync(graph);
            
            // Assert
            Assert.NotNull(partitions);
            Assert.True(partitions.Count >= 2); // Should have at least "Controllers" and "Services"
            Assert.True(partitions.ContainsKey("Controllers"));
            Assert.True(partitions.ContainsKey("Services"));
        }
        
        private List<CodeComponent> CreateSimpleTestComponents()
        {
            return new List<CodeComponent>
            {
                new CodeComponent 
                { 
                    Id = "MainController", 
                    Name = "MainController", 
                    Type = "Controller", 
                    LineCount = 100,
                    ComplexityScore = 5,
                    Dependencies = new List<string> { "ComponentB" } 
                },
                new CodeComponent 
                { 
                    Id = "ComponentB", 
                    Name = "ComponentB", 
                    Type = "Class", 
                    LineCount = 80,
                    ComplexityScore = 8,
                    Dependencies = new List<string> { "ComponentC" } 
                },
                new CodeComponent 
                { 
                    Id = "ComponentC", 
                    Name = "ComponentC", 
                    Type = "Class", 
                    LineCount = 60,
                    ComplexityScore = 3,
                    Dependencies = new List<string>() 
                }
            };
        }
        
        private List<CodeComponent> CreateLargeTestComponents(int count)
        {
            var components = new List<CodeComponent>();
            var random = new Random(42);
            
            for (int i = 0; i < count; i++)
            {
                var dependencies = new List<string>();
                
                if (i > 0 && random.NextDouble() < 0.3)
                {
                    dependencies.Add($"Component{i - 1}");
                }
                
                components.Add(new CodeComponent
                {
                    Id = $"Component{i}",
                    Name = $"Component{i}",
                    Type = "Class",
                    LineCount = random.Next(50, 500),
                    ComplexityScore = random.Next(1, 20),
                    Dependencies = dependencies
                });
            }
            
            return components;
        }
        
        private List<CodeComponent> CreateComplexTestComponents()
        {
            return new List<CodeComponent>
            {
                new CodeComponent 
                { 
                    Id = "ComplexComponent", 
                    Name = "ComplexComponent", 
                    Type = "Class", 
                    FilePath = "src/ComplexComponent.cs",
                    LineCount = 150,
                    ComplexityScore = 15,
                    Dependencies = new List<string> { "SimpleComponent" }
                },
                new CodeComponent 
                { 
                    Id = "SimpleComponent", 
                    Name = "SimpleComponent", 
                    Type = "Class", 
                    FilePath = "src/SimpleComponent.cs",
                    LineCount = 50,
                    ComplexityScore = 3,
                    Dependencies = new List<string>() 
                }
            };
        }
        
        private List<CodeComponent> CreateDirectoryBasedComponents()
        {
            return new List<CodeComponent>
            {
                new CodeComponent 
                { 
                    Id = "UserController", 
                    Name = "UserController", 
                    Type = "Controller", 
                    FilePath = "Controllers/UserController.cs",
                    Dependencies = new List<string> { "UserService" }
                },
                new CodeComponent 
                { 
                    Id = "ProductController", 
                    Name = "ProductController", 
                    Type = "Controller", 
                    FilePath = "Controllers/ProductController.cs",
                    Dependencies = new List<string> { "ProductService" }
                },
                new CodeComponent 
                { 
                    Id = "UserService", 
                    Name = "UserService", 
                    Type = "Service", 
                    FilePath = "Services/UserService.cs",
                    Dependencies = new List<string>() 
                },
                new CodeComponent 
                { 
                    Id = "ProductService", 
                    Name = "ProductService", 
                    Type = "Service", 
                    FilePath = "Services/ProductService.cs",
                    Dependencies = new List<string>() 
                }
            };
        }
    }
}