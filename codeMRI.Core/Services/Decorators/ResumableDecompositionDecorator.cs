using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services.Decorators;

/// <summary>
///     Decorator for IHierarchicalDecompositionService that adds resumability
/// </summary>
public class ResumableDecompositionDecorator : IHierarchicalDecompositionService
{
    private readonly IHierarchicalDecompositionService _inner;
    private readonly IWikiRepository _wikiRepo;
    private readonly ILLMInvocationContext _invocationContext;
    private readonly ILogger<ResumableDecompositionDecorator> _logger;

    public ResumableDecompositionDecorator(
        IHierarchicalDecompositionService inner,
        IWikiRepository wikiRepo,
        ILLMInvocationContext invocationContext,
        ILogger<ResumableDecompositionDecorator> logger)
    {
        _inner = inner;
        _wikiRepo = wikiRepo;
        _invocationContext = invocationContext;
        _logger = logger;
    }

    public async Task<ModuleTree> DecomposeHierarchicallyAsync(string repositoryPath, EnhancedDependencyGraph graph, CancellationToken cancellationToken = default)
    {
        var state = await _wikiRepo.GetIngestionProcessingStateAsync(repositoryPath);
        
        if (!string.IsNullOrEmpty(state?.SerializedGraph) && !_invocationContext.Force)
        {
            _logger.LogInformation("Resuming decomposition: Found existing unified graph in state.");
            GraphSerializer.DeserializeInto(state.SerializedGraph, graph);
            
            // If the graph already contains Module nodes, we can skip decomposition logic
            if (graph.GetNodes().Any(n => n.Metadata.Type == "Module"))
            {
                _logger.LogInformation("Skipping decomposition: Graph already enriched with Module nodes.");
                
                // Reconstruct ModuleTree for compatibility if needed
                // For now, we delegate to a lightweight tree reconstruction helper if we still need the ModuleTree object
                return ReconstructModuleTreeFromGraph(graph);
            }
        }

        var result = await _inner.DecomposeHierarchicallyAsync(repositoryPath, graph, cancellationToken);
        
        // Save the enriched graph back to state
        if (state == null) state = new IngestionProcessingState();
        state.SerializedGraph = GraphSerializer.Serialize(graph);
        await _wikiRepo.SaveIngestionProcessingStateAsync(repositoryPath, state);
        
        return result;
    }

    private ModuleTree ReconstructModuleTreeFromGraph(EnhancedDependencyGraph graph)
    {
        var tree = new ModuleTree();
        var moduleNodes = graph.GetNodes()
            .Where(n => n.Metadata.Type == "Module")
            .ToList();

        var idToModule = new Dictionary<string, ModuleNode>();

        // 1. Create all ModuleNode objects
        foreach (var graphNode in moduleNodes)
        {
            var meta = graphNode.Metadata;
            var module = new ModuleNode
            {
                Id = graphNode.ComponentId,
                Name = graphNode.ComponentId == "root" ? "Repository" : graphNode.ComponentId, // Fallback naming
                EstimatedTokens = meta.EstimatedTokens,
                ComplexityScore = meta.CyclomaticComplexity,
                Metadata = new Dictionary<string, string>()
            };

            // Map properties back from the generic dictionary
            if (meta.Properties.TryGetValue("Level", out var level))
                module.Level = ConvertToInt(level);
            if (meta.Properties.TryGetValue("IsLeaf", out var isLeaf))
                module.IsLeaf = ConvertToBool(isLeaf);
            if (meta.Properties.TryGetValue("Description", out var desc))
                module.Description = desc?.ToString();

            if (meta.Properties.TryGetValue("Cohesion", out var coh))
                module.QualityMetrics.Cohesion = ConvertToDouble(coh);
            if (meta.Properties.TryGetValue("Coupling", out var coup))
                module.QualityMetrics.Coupling = ConvertToDouble(coup);
            if (meta.Properties.TryGetValue("MaintainabilityIndex", out var mi))
                module.QualityMetrics.MaintainabilityIndex = ConvertToDouble(mi);

            idToModule[module.Id] = module;
            if (module.Id != "root")
            {
                tree.AddNode(module);
            }
            else
            {
                tree.Root = module;
            }
        }

        // 2. Establish hierarchy and components
        foreach (var edge in graph.GetEdges())
        {
            if (edge.Type == EdgeType.ChildOf)
            {
                // From (Child) -> To (Parent)
                if (idToModule.TryGetValue(edge.From, out var child) && idToModule.TryGetValue(edge.To, out var parent))
                {
                    parent.AddChild(child);
                }
            }
            else if (edge.Type == EdgeType.Contains)
            {
                // From (Module) -> To (Component)
                if (idToModule.TryGetValue(edge.From, out var module))
                {
                    module.Components.Add(edge.To);
                }
            }
        }

        return tree;
    }

    private static int ConvertToInt(object value)
    {
        if (value is int i) return i;
        if (value is long l) return (int)l;
        if (value is System.Text.Json.JsonElement je) return je.GetInt32();
        return 0;
    }

    private static bool ConvertToBool(object value)
    {
        if (value is bool b) return b;
        if (value is System.Text.Json.JsonElement je) return je.GetBoolean();
        return false;
    }

    private static double ConvertToDouble(object value)
    {
        if (value is double d) return d;
        if (value is float f) return f;
        if (value is decimal m) return (double)m;
        if (value is int i) return i;
        if (value is long l) return l;
        if (value is System.Text.Json.JsonElement je) return je.GetDouble();
        return 0;
    }
}
