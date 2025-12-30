using codeMRI.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services;

/// <summary>
///     Routes documentation tasks to specialized models based on task type.
/// </summary>
public class ModelRoutingService : IModelRoutingService
{
    private readonly ModelRoutingConfig _config;
    private readonly ILogger<ModelRoutingService> _logger;

    public ModelRoutingService(
        ModelRoutingConfig config,
        ILogger<ModelRoutingService> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <inheritdoc />
    public string? SelectModelForTask(DocumentationTaskType taskType)
    {
        if (!_config.EnableModelRouting)
        {
            _logger.LogDebug("Model routing is disabled, using default model");
            return null; // Fall back to default
        }

        var selectedModel = taskType switch
        {
            DocumentationTaskType.Synthesis => _config.SynthesisJudgeModel,
            DocumentationTaskType.Default => null,
            // CodeAnalysis and NaturalLanguage removed - bypassed by ensemble generation
            _ => null
        };

        if (selectedModel != null)
            _logger.LogInformation("Selected model {Model} for task type {TaskType}",
                selectedModel, taskType);

        return selectedModel;
    }
}

/// <summary>
///     Configuration for model routing (injected from Infrastructure layer).
/// </summary>
public class ModelRoutingConfig
{
    public bool EnableModelRouting { get; set; }
    public string? SynthesisJudgeModel { get; set; }
}