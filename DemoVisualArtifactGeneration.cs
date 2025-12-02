using codeMRI.Shared.Models;
using codeMRI.Visualization.Services;
using System;
using System.Threading.Tasks;

namespace codeMRI.Demo
{
    /// <summary>
    /// Demonstration of Visual Artifact Generation capabilities
    /// This shows how the DiagramGeneratorService creates Mermaid.js diagrams
    /// </summary>
    public class DemoVisualArtifactGeneration
    {
        public static async Task Main(string[] args)
        {
            var diagramGenerator = new DiagramGeneratorService();

            // Demo 1: Class Diagram
            Console.WriteLine("=== Class Diagram Demo ===");
            var classGraph = new EnhancedDependencyGraph();
            classGraph.AddNode("UserController", new NodeMetadata { Type = "Class" });
            classGraph.AddNode("UserService", new NodeMetadata { Type = "Class" });
            classGraph.AddNode("IUserService", new NodeMetadata { Type = "Interface" });
            classGraph.AddNode("DatabaseContext", new NodeMetadata { Type = "Class" });

            classGraph.AddEdge("UserController", "UserService", EdgeType.Dependency, 1.0);
            classGraph.AddEdge("UserService", "IUserService", EdgeType.Implementation, 1.0);
            classGraph.AddEdge("UserService", "DatabaseContext", EdgeType.Composition, 1.0);

            var classDiagram = await diagramGenerator.GenerateComponentDiagramAsync(classGraph, "UserController");
            Console.WriteLine(classDiagram);
            Console.WriteLine();

            // Demo 2: Sequence Diagram
            Console.WriteLine("=== Sequence Diagram Demo ===");
            var sequenceGraph = new EnhancedDependencyGraph();
            sequenceGraph.AddNode("Client", new NodeMetadata { Type = "Actor" });
            sequenceGraph.AddNode("APIController", new NodeMetadata { Type = "Class" });
            sequenceGraph.AddNode("BusinessService", new NodeMetadata { Type = "Class" });
            sequenceGraph.AddNode("Repository", new NodeMetadata { Type = "Class" });
            sequenceGraph.AddNode("Database", new NodeMetadata { Type = "Database" });

            sequenceGraph.AddEdge("Client", "APIController", EdgeType.Call, 1.0);
            sequenceGraph.AddEdge("APIController", "BusinessService", EdgeType.Call, 1.0);
            sequenceGraph.AddEdge("BusinessService", "Repository", EdgeType.Call, 1.0);
            sequenceGraph.AddEdge("Repository", "Database", EdgeType.Call, 1.0);

            var sequenceDiagram = await diagramGenerator.GenerateSequenceDiagramAsync(sequenceGraph, "Client");
            Console.WriteLine(sequenceDiagram);
            Console.WriteLine();

            // Demo 3: Architecture Diagram
            Console.WriteLine("=== Architecture Diagram Demo ===");
            var moduleTree = new ModuleTree();
            var root = new ModuleNode { Id = "webapp", Name = "Web Application" };
            var presentationLayer = new ModuleNode { Id = "presentation", Name = "Presentation Layer", Parent = root, Level = 1 };
            var businessLayer = new ModuleNode { Id = "business", Name = "Business Layer", Parent = root, Level = 1 };
            var dataLayer = new ModuleNode { Id = "data", Name = "Data Layer", Parent = root, Level = 1 };

            presentationLayer.Components.Add("UserController");
            presentationLayer.Components.Add("ProductController");
            businessLayer.Components.Add("UserService");
            businessLayer.Components.Add("ProductService");
            dataLayer.Components.Add("UserRepository");
            dataLayer.Components.Add("ProductRepository");

            root.Children.Add(presentationLayer);
            root.Children.Add(businessLayer);
            root.Children.Add(dataLayer);

            moduleTree.Root = root;
            moduleTree.Nodes[root.Id] = root;
            moduleTree.Nodes[presentationLayer.Id] = presentationLayer;
            moduleTree.Nodes[businessLayer.Id] = businessLayer;
            moduleTree.Nodes[dataLayer.Id] = dataLayer;

            var archGraph = new EnhancedDependencyGraph();
            archGraph.AddNode("UserController", new NodeMetadata { Type = "Controller" });
            archGraph.AddNode("ProductController", new NodeMetadata { Type = "Controller" });
            archGraph.AddNode("UserService", new NodeMetadata { Type = "Service" });
            archGraph.AddNode("ProductService", new NodeMetadata { Type = "Service" });
            archGraph.AddNode("UserRepository", new NodeMetadata { Type = "Repository" });
            archGraph.AddNode("ProductRepository", new NodeMetadata { Type = "Repository" });

            archGraph.AddEdge("UserController", "UserService", EdgeType.Call, 1.0);
            archGraph.AddEdge("ProductController", "ProductService", EdgeType.Call, 1.0);
            archGraph.AddEdge("UserService", "UserRepository", EdgeType.Call, 1.0);
            archGraph.AddEdge("ProductService", "ProductRepository", EdgeType.Call, 1.0);

            var archDiagram = await diagramGenerator.GenerateArchitectureDiagramAsync(moduleTree, archGraph);
            Console.WriteLine(archDiagram);
        }
    }
}