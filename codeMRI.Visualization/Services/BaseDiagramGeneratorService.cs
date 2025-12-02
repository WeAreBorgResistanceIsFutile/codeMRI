using System.Text;
using codeMRI.Shared.Models;
using codeMRI.Visualization.Interfaces;

namespace codeMRI.Visualization.Services;

/// <summary>
///     Base diagram generator implementation
/// </summary>
public class BaseDiagramGeneratorService : IDiagramGenerator
{
    public Task<string> GenerateArchitectureDiagramAsync(ModuleTree moduleTree, EnhancedDependencyGraph graph)
    {
        var mermaid = new StringBuilder();
        mermaid.AppendLine("graph TB");

        // Add modules
        foreach (var module in GetModules(moduleTree)) mermaid.AppendLine($"    {module.Id}[{module.Name}]");

        // Add relationships
        foreach (var edge in graph.GetEdges()) mermaid.AppendLine($"    {edge.From} --> {edge.To}");

        return Task.FromResult(mermaid.ToString());
    }

    public Task<string> GenerateComponentDiagramAsync(EnhancedDependencyGraph graph, string? focusComponentId = null)
    {
        var mermaid = new StringBuilder();
        mermaid.AppendLine("graph LR");

        var nodes = graph.GetNodes().ToList();
        if (!string.IsNullOrEmpty(focusComponentId))
        {
            var focusNode = nodes.FirstOrDefault(n => n.ComponentId == focusComponentId);
            if (focusNode != null)
            {
                var relatedNodes = new[] { focusNode }
                    .Concat(graph.GetEdges().Where(e => e.To == focusComponentId).Select(e => graph.GetNode(e.From)))
                    .Concat(graph.GetEdges().Where(e => e.From == focusComponentId).Select(e => graph.GetNode(e.To)))
                    .Where(n => n != null)
                    .Cast<GraphNode>()
                    .Distinct()
                    .ToList();

                nodes = relatedNodes;
            }
        }

        foreach (var node in nodes) mermaid.AppendLine($"    {node.ComponentId}[{node.ComponentId}]");

        foreach (var edge in graph.GetEdges())
            if (nodes.Any(n => n.ComponentId == edge.From) && nodes.Any(n => n.ComponentId == edge.To))
                mermaid.AppendLine($"    {edge.From} --> {edge.To}");

        return Task.FromResult(mermaid.ToString());
    }

    public Task<string> GenerateSequenceDiagramAsync(EnhancedDependencyGraph graph, string entryPointId)
    {
        var mermaid = new StringBuilder();
        mermaid.AppendLine("sequenceDiagram");

        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(entryPointId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (visited.Contains(current)) continue;
            visited.Add(current);

            var outgoingEdges = graph.GetEdges().Where(e => e.From == current);
            foreach (var edge in outgoingEdges)
            {
                mermaid.AppendLine($"    {current} ->> {edge.To}: {edge.Type}");
                if (!visited.Contains(edge.To)) queue.Enqueue(edge.To);
            }
        }

        return Task.FromResult(mermaid.ToString());
    }

    public Task<string> GenerateDataFlowDiagramAsync(EnhancedDependencyGraph graph, string focusComponentId)
    {
        var mermaid = new StringBuilder();
        mermaid.AppendLine("graph LR");

        var focusNode = graph.GetNode(focusComponentId);
        if (focusNode != null)
        {
            mermaid.AppendLine($"    {focusComponentId}((Data))");

            foreach (var edge in graph.GetEdges().Where(e => e.To == focusComponentId))
                mermaid.AppendLine($"    {edge.From} --> {focusComponentId}");

            foreach (var edge in graph.GetEdges().Where(e => e.From == focusComponentId))
                mermaid.AppendLine($"    {focusComponentId} --> {edge.To}");
        }

        return Task.FromResult(mermaid.ToString());
    }

    private IEnumerable<ModuleNode> GetModules(ModuleTree tree)
    {
        var modules = new List<ModuleNode>();
        if (tree.Root != null) CollectModules(tree.Root, modules);
        return modules;
    }

    private void CollectModules(ModuleNode node, List<ModuleNode> modules)
    {
        modules.Add(node);
        foreach (var child in node.Children) CollectModules(child, modules);
    }
}