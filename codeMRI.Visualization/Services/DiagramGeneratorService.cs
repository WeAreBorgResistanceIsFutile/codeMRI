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

        var nodes = graph.GetNodes().ToList();
        var printedNodes = new HashSet<string>();

        // Strategy 1: Recursively render ModuleTree structure
        if (moduleTree.Root != null)
        {
            RenderModule(moduleTree.Root, mermaid, printedNodes);
        }

        // Strategy 2: Group remaining nodes by Layer/Pattern
        var remainingNodes = nodes.Where(n => !printedNodes.Contains(n.ComponentId)).ToList();
        
        if (remainingNodes.Any())
        {
            var groupedNodes = new Dictionary<string, List<GraphNode>>();

            foreach (var node in remainingNodes)
            {
                string group = "Default";
                if (node.Metadata.Properties.TryGetValue("Module", out var moduleObj) && moduleObj is string moduleName)
                {
                    group = moduleName;
                }
                else
                {
                    // Fallback to Layer
                    string layer = node.Metadata.Layer;
                    if (string.IsNullOrEmpty(layer))
                    {
                        layer = DetermineComponentLayer(node.Metadata.Type);
                    }
                    
                    if (layer != "Unknown")
                    {
                        group = layer;
                    }
                }
                
                if (!groupedNodes.ContainsKey(group))
                    groupedNodes[group] = new List<GraphNode>();
                
                groupedNodes[group].Add(node);
            }

            // Generate subgraphs for remaining nodes
            foreach (var group in groupedNodes)
            {
                if (group.Key != "Default")
                {
                    mermaid.AppendLine($"    subgraph {group.Key}");
                }

                foreach (var node in group.Value)
                {
                    mermaid.AppendLine($"        {node.ComponentId}[{node.ComponentId}]");
                }

                if (group.Key != "Default")
                {
                    mermaid.AppendLine("    end");
                }
            }
        }

        // Add relationships
        foreach (var edge in graph.GetEdges())
        {
            mermaid.AppendLine($"    {edge.From} --> {edge.To}");
        }

        return Task.FromResult(mermaid.ToString());
    }

    private void RenderModule(ModuleNode module, StringBuilder sb, HashSet<string> printedNodes)
    {
        // Only render if it has content or is meaningful
        bool hasContent = module.Components.Count > 0 || module.Children.Count > 0;
        
        if (hasContent)
        {
            sb.AppendLine($"    subgraph {module.Id}[{module.Name}]");
            
            // Render Components
            foreach (var compId in module.Components)
            {
                sb.AppendLine($"        {compId}[{compId}]");
                printedNodes.Add(compId);
            }

            // Render Children
            foreach (var child in module.Children)
            {
                RenderModule(child, sb, printedNodes);
            }
            
            sb.AppendLine("    end");
        }
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

        // Limit depth or count to prevent infinite loops/huge diagrams
        int maxSteps = 20;
        int steps = 0;

        while (queue.Count > 0 && steps < maxSteps)
        {
            var current = queue.Dequeue();
            
            // We allow revisiting for sequence flow, but need to be careful. 
            // For a static graph walk, we just list dependencies as calls.
            if (visited.Contains(current)) continue;
            visited.Add(current);

            var outgoingEdges = graph.GetEdges().Where(e => e.From == current);
            foreach (var edge in outgoingEdges)
            {
                string arrow = "->>"; // Solid line with arrow
                if (edge.Type == EdgeType.Call || edge.Type == EdgeType.MethodCall)
                {
                    arrow = "->>"; 
                }
                else
                {
                    arrow = "-->>"; // Dotted line for loose dependencies? Or just stick to solid.
                }

                mermaid.AppendLine($"    {current} {arrow} {edge.To}: {edge.Type}");
                if (!visited.Contains(edge.To)) queue.Enqueue(edge.To);
            }
            steps++;
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
            // Focus node as data/process center
            mermaid.AppendLine($"    {focusComponentId}(({focusComponentId}))");

            // Incoming Data
            foreach (var edge in graph.GetEdges().Where(e => e.To == focusComponentId))
                mermaid.AppendLine($"    {edge.From} --> {focusComponentId}");

            // Outgoing Data
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
            "api" => "Presentation",
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
