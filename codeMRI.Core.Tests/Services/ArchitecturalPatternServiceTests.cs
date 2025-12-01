using System.Collections.Generic;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Moq;
using NUnit.Framework;

namespace codeMRI.Core.Tests.Services
{
    [TestFixture]
    public class ArchitecturalPatternServiceTests
    {
        private ArchitecturalPatternService _service;
        private EnhancedDependencyGraph _graph;

        [SetUp]
        public void Setup()
        {
            _service = new ArchitecturalPatternService();
            _graph = new EnhancedDependencyGraph();
        }

        [Test]
        public void DetermineLayer_ShouldReturnPresentation_ForController()
        {
            var node = new GraphNode
            {
                ComponentId = "UserController",
                Metadata = new NodeMetadata { Type = "Controller" }
            };

            var result = _service.DetermineLayer(node);

            Assert.That(result, Is.EqualTo(ArchitecturalLayerType.Presentation));
        }

        [Test]
        public void DetermineLayer_ShouldReturnData_ForRepository()
        {
            var node = new GraphNode
            {
                ComponentId = "UserRepository",
                Metadata = new NodeMetadata { Type = "Repository" }
            };

            var result = _service.DetermineLayer(node);

            Assert.That(result, Is.EqualTo(ArchitecturalLayerType.Data));
        }

        [Test]
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

            Assert.That(pattern.Type, Is.EqualTo(ArchitecturalPatternType.Layered));
            Assert.That(pattern.Confidence, Is.GreaterThan(0.5));
        }

        [Test]
        public void RecognizePattern_ShouldDetectMicroservices_WhenIndependentModulesExist()
        {
             // Setup independent modules with APIs
            _graph.AddNode("OrderApi", new NodeMetadata { Type = "API" });
            _graph.AddNode("PaymentApi", new NodeMetadata { Type = "API" });
            // No direct dependencies, or CrossBoundary dependencies

            var module = new ModuleNode
            {
                Id = "System",
                Components = new HashSet<string> { "OrderApi", "PaymentApi" }
            };
            
            // Start with unknown to ensure logic runs
            
            var pattern = _service.RecognizePattern(module, _graph);
            
            Assert.That(pattern.Type, Is.EqualTo(ArchitecturalPatternType.Microservices));
        }

        [Test]
        public void RecognizePattern_ShouldDetectEventDriven_WhenEventComponentsExist()
        {
            _graph.AddNode("OrderPublisher", new NodeMetadata { Type = "Publisher" });
            _graph.AddNode("OrderCreatedEvent", new NodeMetadata { Type = "Event" });
            _graph.AddNode("InventorySubscriber", new NodeMetadata { Type = "Subscriber" });

            _graph.AddEdge("OrderPublisher", "OrderCreatedEvent", EdgeType.Dependency, 1.0);
            _graph.AddEdge("InventorySubscriber", "OrderCreatedEvent", EdgeType.Dependency, 1.0);

            var module = new ModuleNode
            {
                Id = "EventSystem",
                Components = new HashSet<string> { "OrderPublisher", "OrderCreatedEvent", "InventorySubscriber" }
            };

            var pattern = _service.RecognizePattern(module, _graph);

            Assert.That(pattern.Type, Is.EqualTo(ArchitecturalPatternType.EventDriven));
        }
    }
}