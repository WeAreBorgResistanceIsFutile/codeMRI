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
    private readonly ILogger<ResumableDecompositionDecorator> _logger;

    public ResumableDecompositionDecorator(
        IHierarchicalDecompositionService inner,
        IWikiRepository wikiRepo,
        ILogger<ResumableDecompositionDecorator> logger)
    {
        _inner = inner;
        _wikiRepo = wikiRepo;
        _logger = logger;
    }

    public async Task<ModuleTree> DecomposeHierarchicallyAsync(string repositoryPath, EnhancedDependencyGraph graph, CancellationToken cancellationToken = default)
    {
        var state = await _wikiRepo.GetIngestionProcessingStateAsync(repositoryPath);
        
        if (!string.IsNullOrEmpty(state?.SerializedGraph))
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
        // TODO: Implement logic to build ModuleTree object from Graph Module nodes if needed.
        // If we move everyone to use the graph directly, this might become obsolete.
        return new ModuleTree(); 
    }
}
