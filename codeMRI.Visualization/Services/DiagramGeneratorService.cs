using System.Text;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Visualization.Interfaces;

namespace codeMRI.Visualization.Services
{
    public class DiagramGeneratorService : IDiagramGenerator
    {
        public async Task<string> GenerateArchitectureDiagramAsync(ModuleTree moduleTree, EnhancedDependencyGraph graph)
        {
            var sb = new StringBuilder();
            sb.AppendLine("graph TD");

            // Traverse modules to create subgraphs
            foreach (var child in moduleTree.Root.Children)
            {
                AppendModule(sb, child);
            }

            // Add Edges
            // We only want edges that cross interesting boundaries or key dependencies
            // For now, add all edges between visible components
            var visibleComponents = new HashSet<string>();
            CollectComponents(moduleTree.Root, visibleComponents);

            foreach (var edge in graph.GetEdges())
            {
                if (visibleComponents.Contains(edge.From) && visibleComponents.Contains(edge.To))
                {
                    sb.AppendLine($"    {SanitizeId(edge.From)} --> {SanitizeId(edge.To)}");
                }
            }

            return await Task.FromResult(sb.ToString());
        }

        public async Task<string> GenerateDataFlowDiagramAsync(EnhancedDependencyGraph graph, string focusComponentId)
        {
            var sb = new StringBuilder();
            sb.AppendLine("graph LR");

            var focusNode = graph.GetNode(focusComponentId);
            if (focusNode != null)
            {
                sb.AppendLine($"    {SanitizeId(focusComponentId)}[\"{focusComponentId}\"]");

                // Incoming data (InEdges)
                foreach (var sourceId in focusNode.InEdges)
                {
                     // Attempt to guess if it's data flow. 
                     // For now, visualize all incoming as Potential Input
                     sb.AppendLine($"    {SanitizeId(sourceId)} --> {SanitizeId(focusComponentId)}");
                }

                // Outgoing data (OutEdges)
                foreach (var targetId in focusNode.OutEdges)
                {
                    // Visualize all outgoing as Potential Output or Call
                    sb.AppendLine($"    {SanitizeId(focusComponentId)} --> {SanitizeId(targetId)}");
                }
            }

            return await Task.FromResult(sb.ToString());
        }

        private void AppendModule(StringBuilder sb, ModuleNode module)
        {
            sb.AppendLine($"    subgraph {SanitizeId(module.Id)}\"{module.Name}\"");
            
            foreach (var componentId in module.Components)
            {
                sb.AppendLine($"        {SanitizeId(componentId)}");
            }

            foreach (var child in module.Children)
            {
                AppendModule(sb, child);
            }

            sb.AppendLine("    end");
        }

        private void CollectComponents(ModuleNode module, HashSet<string> components)
        {
            foreach (var c in module.Components) components.Add(c);
            foreach (var child in module.Children) CollectComponents(child, components);
        }

        private string SanitizeId(string id)
        {
            return id.Replace(" ", "_").Replace(".", "_").Replace("-", "_");
        }

        public async Task<string> GenerateComponentDiagramAsync(EnhancedDependencyGraph graph, string? focusComponentId = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("classDiagram");

            var nodesToInclude = new HashSet<string>();

            if (!string.IsNullOrEmpty(focusComponentId))
            {
                var focusNode = graph.GetNode(focusComponentId);
                if (focusNode != null)
                {
                    nodesToInclude.Add(focusComponentId);
                    foreach (var neighbor in focusNode.OutEdges) nodesToInclude.Add(neighbor);
                    foreach (var neighbor in focusNode.InEdges) nodesToInclude.Add(neighbor);
                }
            }
            else
            {
                // If no focus, maybe include top-level or all (dangerous if huge)
                // For MVP, let's include all nodes if count < 50, else top 20 by PageRank?
                // Let's stick to a limit.
                foreach(var node in graph.GetNodes().Take(20)) nodesToInclude.Add(node.ComponentId);
            }

            foreach (var nodeId in nodesToInclude)
            {
                var node = graph.GetNode(nodeId);
                if (node != null)
                {
                    sb.AppendLine($"    class {SanitizeId(nodeId)}");
                    if (node.Metadata.Type == "Interface")
                    {
                        sb.AppendLine($"    <<Interface>> {SanitizeId(nodeId)}");
                    }
                }
            }

            foreach (var edge in graph.GetEdges())
            {
                if (nodesToInclude.Contains(edge.From) && nodesToInclude.Contains(edge.To))
                {
                    string arrow = "-->";
                    switch (edge.Type)
                    {
                        case EdgeType.Inheritance: arrow = "--|>"; break;
                        case EdgeType.Implementation: arrow = "..|>"; break;
                        case EdgeType.Composition: arrow = "*--"; break;
                        case EdgeType.Dependency: arrow = "..>"; break;
                    }
                    sb.AppendLine($"    {SanitizeId(edge.From)} {arrow} {SanitizeId(edge.To)}");
                }
            }

            return await Task.FromResult(sb.ToString());
        }

        public async Task<string> GenerateSequenceDiagramAsync(EnhancedDependencyGraph graph, string entryPointId)
        {
            var sb = new StringBuilder();
            sb.AppendLine("sequenceDiagram");
            sb.AppendLine("    autonumber");

            var visitedEdges = new HashSet<(string, string)>();
            var participants = new HashSet<string>();

            // DFS to traverse calls
            await TraverseSequenceAsync(graph, entryPointId, sb, visitedEdges, participants, 0, 5);

            return sb.ToString();
        }

        private Task TraverseSequenceAsync(
            EnhancedDependencyGraph graph, 
            string currentId, 
            StringBuilder sb, 
            HashSet<(string, string)> visitedEdges,
            HashSet<string> participants,
            int depth,
            int maxDepth)
        {
            if (depth >= maxDepth) return Task.CompletedTask;

            var node = graph.GetNode(currentId);
            if (node == null) return Task.CompletedTask;

            if (!participants.Contains(currentId))
            {
                // We can add participant definitions if needed, but Mermaid auto-detects.
                // Just keeping track.
                participants.Add(currentId);
            }

            foreach (var targetId in node.OutEdges)
            {
                if (visitedEdges.Contains((currentId, targetId))) continue;
                
                // We need to find the specific edge to check type
                // But OutEdges is just a list of IDs.
                // The graph stores edges in _edges dictionary with key (from, to).
                // We can't access _edges directly on graph (private), but we can iterate GetEdges() or check logic.
                // Wait, GetEdges() returns all edges.
                // This is inefficient. But for MVP...
                // Or maybe just assume MethodCall if we are traversing for Sequence Diagram.
                
                // Better: The service interface for EnhancedDependencyGraph doesn't expose GetEdge(from, to).
                // I will iterate graph.GetEdges() once to build a lookup or just iterate here (slow).
                
                // Let's look for the edge in the full list
                var edge = graph.GetEdges().FirstOrDefault(e => e.From == currentId && e.To == targetId);
                if (edge != null)
                {
                     string label = "Call";
                     if (edge.Type == EdgeType.MethodCall) label = "Call";
                     else if (edge.Type == EdgeType.Dependency) label = "Depends";
                     else continue; // Skip non-call/dependency edges for sequence?

                     visitedEdges.Add((currentId, targetId));

                     sb.AppendLine($"    {SanitizeId(currentId)}->>{SanitizeId(targetId)}: {label}");
                     
                     TraverseSequenceAsync(graph, targetId, sb, visitedEdges, participants, depth + 1, maxDepth);
                }
            }
            
            return Task.CompletedTask;
        }
    }
}