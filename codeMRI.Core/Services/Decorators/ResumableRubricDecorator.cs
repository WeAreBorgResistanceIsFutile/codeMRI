using System.Text.Json.Serialization;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace codeMRI.Core.Services.Decorators;

/// <summary>
///     Decorator for IRubricGenerationService that adds resumability
/// </summary>
public class ResumableRubricDecorator : IRubricGenerationService
{
    private readonly IRubricGenerationService _inner;
    private readonly IWikiRepository _wikiRepo;
    private readonly ILogger<ResumableRubricDecorator> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ResumableRubricDecorator(
        IRubricGenerationService inner,
        IWikiRepository wikiRepo,
        ILogger<ResumableRubricDecorator> logger)
    {
        _inner = inner;
        _wikiRepo = wikiRepo;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNameCaseInsensitive = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };
    }

    public async Task<EvaluationRubric> GenerateRubricAsync(WikiStructure documentationStructure, RepositoryInfo repositoryInfo, CancellationToken cancellationToken = default)
    {
        var state = await _wikiRepo.GetIngestionProcessingStateAsync(repositoryInfo.RepoPath);
        
        if (!string.IsNullOrEmpty(state?.SerializedRubric))
        {
            try
            {
                _logger.LogInformation("Resuming rubric generation: Found existing rubric in state.");
                var rubric = JsonSerializer.Deserialize<EvaluationRubric>(state.SerializedRubric, _jsonOptions);
                if (rubric != null) return rubric;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize existing rubric. Regenerating...");
            }
        }

        var result = await _inner.GenerateRubricAsync(documentationStructure, repositoryInfo, cancellationToken);
        
        // Save to state
        if (state == null) state = new IngestionProcessingState();
        state.SerializedRubric = JsonSerializer.Serialize(result, _jsonOptions);
        await _wikiRepo.SaveIngestionProcessingStateAsync(repositoryInfo.RepoPath, state);
        
        return result;
    }

    public async Task<EvaluationRubric> GenerateConsensusRubricAsync(WikiStructure documentationStructure, RepositoryInfo repositoryInfo, List<string> modelNames, CancellationToken cancellationToken = default)
    {
        // Consensus rubric might have different logic, but for resumability we can treat it similarly if desired.
        // For now, delegating to inner or reusing the same cache if appropriate.
        return await _inner.GenerateConsensusRubricAsync(documentationStructure, repositoryInfo, modelNames, cancellationToken);
    }
}
