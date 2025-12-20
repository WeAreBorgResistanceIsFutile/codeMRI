namespace codeMRI.Core.Interfaces;

/// <summary>
///     Service for orchestrating multi-model documentation generation with ensemble synthesis.
/// </summary>
public interface IMultiModelOrchestrationService
{
    /// <summary>
    ///     Generates documentation using multiple models in parallel and synthesizes the results.
    /// </summary>
    /// <param name="systemPrompt">System prompt for the models</param>
    /// <param name="userPrompt">User prompt containing the documentation request</param>
    /// <param name="history">Conversation history</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Synthesized result with uncertainty metrics</returns>
    Task<MultiModelResult> GenerateWithEnsembleAsync(
        string systemPrompt,
        string userPrompt,
        List<ChatMessage> history,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Result from multi-model ensemble generation containing synthesized content and metadata.
/// </summary>
public class MultiModelResult
{
    /// <summary>
    ///     Final synthesized content from the judge model combining the best elements.
    /// </summary>
    public string SynthesizedContent { get; set; } = string.Empty;

    /// <summary>
    ///     Individual outputs from each model in the ensemble.
    /// </summary>
    public List<ModelOutput> ModelOutputs { get; set; } = new();

    /// <summary>
    ///     Agreement score (0.0-1.0) indicating how similar the model outputs were.
    ///     Higher score indicates stronger consensus.
    /// </summary>
    public double AgreementScore { get; set; }

    /// <summary>
    ///     Uncertainty metric (0.0-1.0) derived from model disagreement.
    ///     Lower agreement leads to higher uncertainty.
    /// </summary>
    public double Uncertainty { get; set; }

    /// <summary>
    ///     List of models that participated in generation.
    /// </summary>
    public List<string> ParticipatingModels { get; set; } = new();
}

/// <summary>
///     Output from a single model in an ensemble.
/// </summary>
public class ModelOutput
{
    /// <summary>
    ///     Name of the model that generated this output.
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    ///     Generated content from this model.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    ///     Confidence score for this output (future use).
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    ///     Time taken to generate this output.
    /// </summary>
    public TimeSpan GenerationTime { get; set; }
}