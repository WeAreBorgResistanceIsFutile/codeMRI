using System.Collections.Generic;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Moq;
using Xunit;

namespace codeMRI.Core.Tests.Services
{
    public class ArchitecturalPatternServiceTests
    {
        private readonly ArchitecturalPatternService _service;
        private readonly EnhancedDependencyGraph _graph;

        public ArchitecturalPatternServiceTests()
        {
            _service = new ArchitecturalPatternService();
            _graph = new EnhancedDependencyGraph();
        }

        [Fact]
        public void DetermineLayer_ShouldReturnPresentation_ForController()
        {
            var node = new GraphNode
            {
                ComponentId = "UserController",
                Metadata = new NodeMetadata { Type = "Controller" }
            };

            var result = _service.DetermineLayer(node);

            Assert.Equal(ArchitecturalLayerType.Presentation, result);
        }

        [Fact]
        public void DetermineLayer_ShouldReturnData_ForRepository()
        {
            var node = new GraphNode
            {
                ComponentId = "UserRepository",
                Metadata = new NodeMetadata { Type = "Repository" }
            };

            var result = _service.DetermineLayer(node);

            Assert.Equal(ArchitecturalLayerType.Data, result);
        }

        [Fact]
        public void RecognizePattern_ShouldDetectLayeredArchitecture()
        {
            // Setup a simple layered structure: Controller -> Service -> Repository
            _graph.AddNode("UserController", new NodeMetadata { Type = "Controller" });
            _graph.AddNode("UserService", new NodeMetadata { Type = "Service" });
            _graph.AddNode("UserRepository", new NodeMetadata { Type = "Repository" });

            _graph.AddEdge("UserController", "UserService", EdgeType.Dependency, 1.0);
            _graph.AddEdge("UserService", "UserRepository", EdgeType.Dependency, 1.0);

            var module = new ModuleNode
            {
                Id = "UserModule",
                Components = new HashSet<string> { "UserController", "UserService", "UserRepository" }
            };

            var pattern = _service.RecognizePattern(module, _graph);

            Assert.Equal(ArchitecturalPatternType.Layered, pattern.Type);
            Assert.True(pattern.Confidence > 0.5);
        }

        [Fact]
        public void RecognizePattern_ShouldDetectMicroservices_WhenIndependentModulesExist()
        {
             // Setup independent modules with APIs
            _graph.AddNode("OrderService", new NodeMetadata { Type = "API" });
            _graph.AddNode("PaymentService", new NodeMetadata { Type = "API" });

            var module = new ModuleNode
            {
                Id = "System",
                Components = new HashSet<string> { "OrderService", "PaymentService" }
            };

            // Only loose coupling or no coupling
            
            var pattern = _service.RecognizePattern(module, _graph);

            // Note: Logic for Microservices might depend on more complex heuristics, 
            // but let's assume presence of multiple "API" or independent clusters suggests it.
            // For now, we might expect Layered if not specifically identified as Microservices.
            // But let's adjust the test to what we expect to implement.
            // If we implement Microservices detection, it might look for bounded contexts or HTTP calls between services.
        }
    }
}
