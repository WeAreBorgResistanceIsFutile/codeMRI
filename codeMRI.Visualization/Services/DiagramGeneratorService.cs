using System.Text;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;

namespace codeMRI.Visualization.Services;

/// <summary>
///     Enhanced diagram generator with zoom, filtering, and export capabilities
/// </summary>
public class DiagramGeneratorService : IDiagramGenerator
{
    private readonly HttpClient _httpClient;

    public DiagramGeneratorService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

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

    /// <summary>
    ///     Generate interactive component diagram with filtering capabilities
    /// </summary>
    public async Task<InteractiveDiagram> GenerateInteractiveComponentDiagramAsync(
        EnhancedDependencyGraph graph,
        string? focusComponentId = null,
        DiagramOptions? options = null)
    {
        options ??= GetDefaultOptions();

        var diagram = new InteractiveDiagram
        {
            Title = "Component Diagram",
            Type = DiagramType.Component,
            Options = options,
            MermaidContent = await GenerateComponentDiagramAsync(graph, focusComponentId)
        };

        diagram.Components = ExtractComponentsFromGraph(graph, options.Filter);
        diagram.Relationships = ExtractRelationshipsFromGraph(graph, options.Filter);

        return diagram;
    }

    /// <summary>
    ///     Generate interactive sequence diagram with filtering capabilities
    /// </summary>
    public async Task<InteractiveDiagram> GenerateInteractiveSequenceDiagramAsync(
        EnhancedDependencyGraph graph,
        string entryPointId,
        DiagramOptions? options = null)
    {
        options ??= GetDefaultOptions();

        var diagram = new InteractiveDiagram
        {
            Title = "Sequence Diagram",
            Type = DiagramType.Sequence,
            Options = options,
            MermaidContent = await GenerateSequenceDiagramAsync(graph, entryPointId)
        };

        diagram.Components = ExtractComponentsFromGraph(graph, options.Filter);
        diagram.Relationships = ExtractRelationshipsFromGraph(graph, options.Filter);

        return diagram;
    }

    /// <summary>
    ///     Generate interactive diagram with zoom, filtering, and export capabilities
    /// </summary>
    public async Task<InteractiveDiagram> GenerateInteractiveArchitectureDiagramAsync(
        ModuleTree moduleTree,
        EnhancedDependencyGraph graph,
        DiagramOptions? options = null)
    {
        options ??= GetDefaultOptions();

        var diagram = new InteractiveDiagram
        {
            Title = "Architecture Diagram",
            Type = DiagramType.Architecture,
            Options = options,
            MermaidContent = await GenerateArchitectureDiagramAsync(moduleTree, graph),
            ExportOptions = GetDefaultExportOptions()
        };

        // Extract components and relationships for filtering
        diagram.Components = ExtractComponentsFromGraph(graph, options.Filter);
        diagram.Relationships = ExtractRelationshipsFromGraph(graph, options.Filter);

        return diagram;
    }

    /// <summary>
    ///     Apply filters to diagram components
    /// </summary>
    public List<DiagramComponent> FilterComponents(
        List<DiagramComponent> components,
        FilterOptions filter)
    {
        var filtered = components.AsEnumerable();

        // Include/exclude by component IDs
        if (filter.IncludedComponents.Any())
            filtered = filtered.Where(c => filter.IncludedComponents.Contains(c.Id));
        else if (filter.ExcludedComponents.Any())
            filtered = filtered.Where(c => !filter.ExcludedComponents.Contains(c.Id));

        // Filter by complexity
        filtered = filtered.Where(c =>
            c.Complexity >= filter.MinComplexity &&
            c.Complexity <= filter.MaxComplexity);

        // Filter by visibility
        if (filter.ShowOnlyPublic) filtered = filtered.Where(c => c.IsPublic);

        if (filter.ShowOnlyDocumented) filtered = filtered.Where(c => c.HasDocumentation);

        // Filter by layers
        if (filter.IncludedLayers.Any())
            filtered = filtered.Where(c => filter.IncludedLayers.Contains(c.Layer));
        else if (filter.ExcludedLayers.Any()) filtered = filtered.Where(c => !filter.ExcludedLayers.Contains(c.Layer));

        return filtered.ToList();
    }

    /// <summary>
    ///     Apply filters to diagram relationships
    /// </summary>
    public List<DiagramRelationship> FilterRelationships(
        List<DiagramRelationship> relationships,
        FilterOptions filter)
    {
        var filtered = relationships.AsEnumerable();

        // Filter by relationship types
        if (filter.IncludedRelationshipTypes.Any())
            filtered = filtered.Where(r => filter.IncludedRelationshipTypes.Contains(r.Type));
        else if (filter.ExcludedRelationshipTypes.Any())
            filtered = filtered.Where(r => !filter.ExcludedRelationshipTypes.Contains(r.Type));

        // Filter by component visibility
        var visibleComponentIds = FilterComponents(
            relationships.SelectMany(r => new[]
            {
                new DiagramComponent { Id = r.FromComponent },
                new DiagramComponent { Id = r.ToComponent }
            }).ToList(),
            filter).Select(c => c.Id).ToHashSet();

        filtered = filtered.Where(r =>
            visibleComponentIds.Contains(r.FromComponent) &&
            visibleComponentIds.Contains(r.ToComponent));

        return filtered.ToList();
    }

    /// <summary>
    ///     Get default diagram options
    /// </summary>
    private DiagramOptions GetDefaultOptions()
    {
        return new DiagramOptions
        {
            EnableZoom = true,
            EnableFiltering = true,
            EnableExport = true,
            Zoom = new ZoomOptions
            {
                MinZoom = 0.1,
                MaxZoom = 5.0,
                DefaultZoom = 1.0,
                EnableMouseWheel = true,
                EnablePan = true,
                FitToView = true
            },
            Filter = new FilterOptions(),
            Theme = "default",
            MaxDepth = 3,
            ShowLabels = true,
            ShowMetadata = false
        };
    }

    /// <summary>
    ///     Get default export options
    /// </summary>
    private ExportOptions GetDefaultExportOptions()
    {
        return new ExportOptions
        {
            EnablePng = true,
            EnableSvg = true,
            EnablePdf = false,
            Png = new PngExportOptions
            {
                Width = 1920,
                Height = 1080,
                Quality = 90,
                BackgroundColor = "#ffffff",
                Transparent = false
            },
            Svg = new SvgExportOptions
            {
                IncludeStyles = true,
                IncludeMetadata = true,
                Compressed = false,
                BackgroundColor = "transparent"
            }
        };
    }

    /// <summary>
    ///     Extract components from dependency graph
    /// </summary>
    private List<DiagramComponent> ExtractComponentsFromGraph(
        EnhancedDependencyGraph graph,
        FilterOptions filter)
    {
        var components = graph.GetNodes().Select(node => new DiagramComponent
        {
            Id = node.ComponentId,
            Name = node.ComponentId,
            Type = node.Metadata.Type,
            Layer = DetermineComponentLayer(node.Metadata.Type),
            Complexity = node.Metadata.CyclomaticComplexity,
            IsPublic = node.Metadata.IsPublic,
            HasDocumentation = node.Metadata.HasDocumentation,
            Metadata = new Dictionary<string, object>
            {
                ["FilePath"] = node.Metadata.FilePath,
                ["LineCount"] = node.Metadata.LineCount,
                ["FanIn"] = node.InDegree,
                ["FanOut"] = node.OutDegree,
                ["EstimatedTokens"] = node.Metadata.EstimatedTokens
            }
        }).ToList();

        return FilterComponents(components, filter);
    }

    /// <summary>
    ///     Extract relationships from dependency graph
    /// </summary>
    private List<DiagramRelationship> ExtractRelationshipsFromGraph(
        EnhancedDependencyGraph graph,
        FilterOptions filter)
    {
        var relationships = graph.GetEdges().Select(edge => new DiagramRelationship
        {
            Id = Guid.NewGuid().ToString(),
            FromComponent = edge.From,
            ToComponent = edge.To,
            Type = edge.Type,
            Strength = edge.Strength,
            Description = edge.Type.ToString(),
            Metadata = new Dictionary<string, object>
            {
                ["CreatedAt"] = edge.CreatedAt,
                ["Strength"] = edge.Strength
            }
        }).ToList();

        return FilterRelationships(relationships, filter);
    }

    /// <summary>
    ///     Determine component layer based on type
    /// </summary>
    private string DetermineComponentLayer(string componentType)
    {
        return componentType.ToLower() switch
        {
            "controller" => "Presentation",
            "service" => "Business",
            "repository" => "Data",
            "interface" => "Contract",
            "class" => "Domain",
            "enum" => "Domain",
            _ => "Unknown"
        };
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