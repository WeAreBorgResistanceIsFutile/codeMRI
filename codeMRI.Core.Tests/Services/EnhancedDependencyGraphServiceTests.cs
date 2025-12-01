using Microsoft.Extensions.Logging;
using Moq;
using codeMRI.Core.Services;
using codeMRI.Core.Interfaces;
using NUnit.Framework;

namespace codeMRI.Core.Tests.Services
{
    [TestFixture]
    public class EnhancedDependencyGraphServiceTests
    {
        private Mock<ILogger<EnhancedDependencyGraphService>> _mockLogger;
        private EnhancedDependencyGraphService _service;
        
        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<EnhancedDependencyGraphService>>();
            _service = new EnhancedDependencyGraphService(_mockLogger.Object);
        }
        
        [Test]
        public async Task BuildGraphAsync_WithSimpleComponents_ShouldCreateValidGraph()
        {
            // Arrange
            var components = CreateSimpleTestComponents();
            
            // Act
            var graph = await _service.BuildGraphAsync(components);
            
            // Assert
            Assert.That(graph, Is.Not.Null);
            Assert.That(graph.NodeCount, Is.EqualTo(3));
            Assert.That(graph.EdgeCount, Is.GreaterThanOrEqualTo(2));
        }
        
        [Test]
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
            Assert.That(graph, Is.Not.Null);
            Assert.That(graph.NodeCount, Is.EqualTo(1000));
            Assert.That(duration.TotalSeconds, Is.LessThan(5), $"Graph construction took {duration.TotalSeconds}s, should be under 5s");
        }
        
        [Test]
        public async Task AnalyzeGraphAsync_WithSimpleGraph_ShouldCalculatePageRank()
        {
            // Arrange
            var components = CreateSimpleTestComponents();
            var graph = await _service.BuildGraphAsync(components);
            
            // Act
            var analysis = await _service.AnalyzeGraphAsync(graph);
            
            // Assert
            Assert.That(analysis.PageRankScores, Is.Not.Null);
            Assert.That(analysis.PageRankScores.Count, Is.EqualTo(3));
            foreach (var score in analysis.PageRankScores.Values)
            {
                Assert.That(score, Is.GreaterThan(0.0));
                Assert.That(score, Is.LessThanOrEqualTo(1.0));
            }
        }
        
        [Test]
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
            Assert.That(entryPoints, Is.Not.Null);
            Assert.That(entryPoints, Does.Contain("MainController"));
        }
        
        [Test]
        public async Task CalculateImportanceScoresAsync_ShouldReturnPositiveScores()
        {
            // Arrange
            var components = CreateSimpleTestComponents();
            var graph = await _service.BuildGraphAsync(components);
            
            // Act
            var importanceScores = await _service.CalculateImportanceScoresAsync(graph);
            
            // Assert
            Assert.That(importanceScores, Is.Not.Null);
            Assert.That(importanceScores.Count, Is.EqualTo(3));
            Assert.That(importanceScores.Values, Has.All.GreaterThan(0));
        }
        
        [Test]
        public void BuildGraphAsync_WithCancellationToken_ShouldRespectCancellation()
        {
            // Arrange
            var components = CreateLargeTestComponents(10000);
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            
            // Act & Assert
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await _service.BuildGraphAsync(components, cts.Token));
        }
        
        [Test]
        public async Task BuildGraphAsync_ShouldCalculateComplexityMetrics()
        {
            // Arrange
            var components = CreateComplexTestComponents();
            
            // Act
            var graph = await _service.BuildGraphAsync(components);
            
            // Assert
            var node = graph.GetNode("ComplexComponent");
            Assert.That(node, Is.Not.Null);
            Assert.That(node.Metadata.CyclomaticComplexity, Is.GreaterThan(0));
            Assert.That(node.Metadata.NestingDepth, Is.GreaterThanOrEqualTo(0));
            Assert.That(node.Metadata.FanIn, Is.GreaterThanOrEqualTo(0));
            Assert.That(node.Metadata.FanOut, Is.GreaterThanOrEqualTo(0));
        }
        
        [Test]
        public async Task EstimateTokensAsync_ShouldReturnReasonableEstimates()
        {
            // Arrange
            var components = CreateSimpleTestComponents();
            var graph = await _service.BuildGraphAsync(components);
            
            // Act
            var tokenEstimates = await _service.EstimateTokensAsync(graph);
            
            // Assert
            Assert.That(tokenEstimates, Is.Not.Null);
            Assert.That(tokenEstimates.Count, Is.EqualTo(3));
            Assert.That(tokenEstimates.Values, Has.All.GreaterThan(0));
        }
        
        [Test]
        public async Task DecomposeHierarchicallyAsync_ShouldCreateModuleTree()
        {
            // Arrange
            var components = CreateLargeTestComponents(100);
            var graph = await _service.BuildGraphAsync(components);
            
            // Act
            var moduleTree = await _service.DecomposeHierarchicallyAsync(graph, maxTokensPerModule: 1000);
            
            // Assert
            Assert.That(moduleTree, Is.Not.Null);
            Assert.That(moduleTree.Root, Is.Not.Null);
            Assert.That(moduleTree.GetAllLeaves().Count, Is.GreaterThan(0));
            
            // All leaves should be under token threshold
            foreach (var leaf in moduleTree.GetAllLeaves())
            {
                Assert.That(leaf.EstimatedTokens, Is.LessThanOrEqualTo(1000), 
                    $"Leaf {leaf.Id} has {leaf.EstimatedTokens} tokens, exceeding threshold of 1000");
            }
        }
        
        [Test]
        public async Task PartitionByDirectoryStructure_ShouldGroupByPath()
        {
            // Arrange
            var components = CreateDirectoryBasedComponents();
            var graph = await _service.BuildGraphAsync(components);
            
            // Act
            var partitions = await _service.PartitionByDirectoryStructureAsync(graph);
            
            // Assert
            Assert.That(partitions, Is.Not.Null);
            Assert.That(partitions.Count, Is.GreaterThanOrEqualTo(2)); // Should have at least "Controllers" and "Services"
            Assert.That(partitions.ContainsKey("Controllers"), Is.True);
            Assert.That(partitions.ContainsKey("Services"), Is.True);
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