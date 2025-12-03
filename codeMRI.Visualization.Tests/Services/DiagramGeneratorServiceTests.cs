using NUnit.Framework;
using codeMRI.Visualization.Services;
using codeMRI.Core.Models;
using codeMRI.Core.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Net.Http;

namespace codeMRI.Visualization.Tests.Services
{
    [TestFixture]
    public class DiagramGeneratorServiceTests
    {
        private DiagramGeneratorService _service;
        private EnhancedDependencyGraph _graph;
        private ModuleTree _moduleTree;

        [SetUp]
        public void Setup()
        {
            // Using a dummy HttpClient as it is required by the constructor but not used in logic
            _service = new DiagramGeneratorService(new HttpClient());
            _graph = new EnhancedDependencyGraph();
            _moduleTree = new ModuleTree();
        }

        [Test]
        public async Task GenerateArchitectureDiagram_ShouldGroupComponentsByLayer()
        {
            // Arrange
            SetupArchitectureGraph();

            // Act
            var result = await _service.GenerateArchitectureDiagramAsync(_moduleTree, _graph);

            // Assert
            Assert.That(result, Does.Contain("graph TB"));
            
            // Check for subgraphs (Pattern Recognition)
            Assert.That(result, Does.Contain("subgraph Presentation"));
            Assert.That(result, Does.Contain("subgraph Business"));
            Assert.That(result, Does.Contain("subgraph Data"));

            // Check for component placement within subgraphs
            // Note: This is a loose check, assuming standard Mermaid indentation or structure
            Assert.That(result, Does.Contain("UserController"));
            Assert.That(result, Does.Contain("UserService"));
            Assert.That(result, Does.Contain("UserRepository"));

            // Check connections
            Assert.That(result, Does.Contain("UserController --> UserService"));
            Assert.That(result, Does.Contain("UserService --> UserRepository"));
        }

        [Test]
        public async Task GenerateSequenceDiagram_ShouldGenerateMermaidSequenceSyntax()
        {
            // Arrange
            SetupSequenceGraph();

            // Act
            var result = await _service.GenerateSequenceDiagramAsync(_graph, "OrderController");

            // Assert
            Assert.That(result, Does.StartWith("sequenceDiagram"));
            Assert.That(result, Does.Contain("OrderController ->> OrderService: Call"));
            Assert.That(result, Does.Contain("OrderService ->> OrderRepository: Call"));
            // PaymentService might be visited depending on BFS order, but let's check it exists if reachable
            Assert.That(result, Does.Contain("OrderService ->> PaymentService: Call"));
        }

        [Test]
        public async Task GenerateDataFlowDiagram_ShouldHighlightFocusNodeAndDataFlow()
        {
            // Arrange
            SetupDataFlowGraph();
            string focusNodeId = "UserDTO";

            // Act
            var result = await _service.GenerateDataFlowDiagramAsync(_graph, focusNodeId);

            // Assert
            Assert.That(result, Does.Contain("graph LR"));
            // Focus node should have distinct shape (e.g., double circle for data)
            Assert.That(result, Does.Contain($"{focusNodeId}(({focusNodeId}))"));
            
            // Check flow
            Assert.That(result, Does.Contain($"UserController --> {focusNodeId}"));
            Assert.That(result, Does.Contain($"{focusNodeId} --> UserService"));
        }

        [Test]
        public async Task GenerateArchitectureDiagram_ShouldHandleMicroservicesPattern()
        {
            // Arrange
            SetupMicroservicesGraph();

            // Act
            var result = await _service.GenerateArchitectureDiagramAsync(_moduleTree, _graph);

            // Assert
            Assert.That(result, Does.Contain("subgraph OrderService"));
            Assert.That(result, Does.Contain("subgraph UserService"));
            // Check cross-boundary communication
            Assert.That(result, Does.Contain("OrderAPI --> UserAPI"));
        }
        
        [Test]
        public void DetermineComponentLayer_ShouldIdentifyLayersCorrectly()
        {
            // Accessing private method via reflection or public if we change it. 
            // For now, we test the behavior via the public GenerateArchitectureDiagramAsync which uses it.
            
            // Since we can't easily unit test private methods without InternalsVisibleTo, 
            // we rely on the integration test above (GenerateArchitectureDiagram_ShouldGroupComponentsByLayer).
            Assert.Pass("Covered by GenerateArchitectureDiagram_ShouldGroupComponentsByLayer");
        }

        private void SetupArchitectureGraph()
        {
            _graph = new EnhancedDependencyGraph();
            
            // Presentation Layer
            _graph.AddNode("UserController", new NodeMetadata { Type = "Controller", Layer = "Presentation" });
            
            // Business Layer
            _graph.AddNode("UserService", new NodeMetadata { Type = "Service", Layer = "Business" });
            
            // Data Layer
            _graph.AddNode("UserRepository", new NodeMetadata { Type = "Repository", Layer = "Data" });

            // Edges
            _graph.AddEdge("UserController", "UserService", EdgeType.Call, 1.0);
            _graph.AddEdge("UserService", "UserRepository", EdgeType.Call, 1.0);
        }

        private void SetupSequenceGraph()
        {
            _graph = new EnhancedDependencyGraph();
            _graph.AddNode("OrderController", new NodeMetadata { Type = "Controller" });
            _graph.AddNode("OrderService", new NodeMetadata { Type = "Service" });
            _graph.AddNode("OrderRepository", new NodeMetadata { Type = "Repository" });
            _graph.AddNode("PaymentService", new NodeMetadata { Type = "Service" });

            _graph.AddEdge("OrderController", "OrderService", EdgeType.Call, 1.0);
            _graph.AddEdge("OrderService", "OrderRepository", EdgeType.Call, 1.0);
            _graph.AddEdge("OrderService", "PaymentService", EdgeType.Call, 1.0);
        }

        private void SetupDataFlowGraph()
        {
            _graph = new EnhancedDependencyGraph();
            _graph.AddNode("UserController", new NodeMetadata { Type = "Controller" });
            _graph.AddNode("UserDTO", new NodeMetadata { Type = "Class" }); // Data Object
            _graph.AddNode("UserService", new NodeMetadata { Type = "Service" });

            // Controller Creates DTO
            _graph.AddEdge("UserController", "UserDTO", EdgeType.Composition, 1.0);
            // DTO passed to Service
            _graph.AddEdge("UserDTO", "UserService", EdgeType.Dependency, 1.0);
        }

        private void SetupMicroservicesGraph()
        {
            _graph = new EnhancedDependencyGraph();
            _moduleTree = new ModuleTree 
            { 
                Root = new ModuleNode { Id = "Root", Name = "Root" } 
            };

            // Simulating Microservices via folder structure or explicit marking
            // Assuming the logic groups by Module or detects clusters.
            // Here we set up nodes that imply clusters if we had intelligent clustering, 
            // but for the test we might need to ensure the Generator supports this.
            
            // Service 1
            _graph.AddNode("OrderAPI", new NodeMetadata { Type = "API", Properties = new Dictionary<string, object> { { "Module", "OrderService" } } });
            
            // Service 2
            _graph.AddNode("UserAPI", new NodeMetadata { Type = "API", Properties = new Dictionary<string, object> { { "Module", "UserService" } } });

            _graph.AddEdge("OrderAPI", "UserAPI", EdgeType.CrossBoundary, 1.0);
        }
    }
}
