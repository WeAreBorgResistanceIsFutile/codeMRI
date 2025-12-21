using System.Diagnostics;
using System.Text;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace codeMRI.Core.Services;

/// <summary>
///     Orchestrates multi-model documentation generation with ensemble synthesis.
///     Generates content with multiple models in parallel and synthesizes results.
/// </summary>
public class MultiModelOrchestrationService : IMultiModelOrchestrationService
{
    private readonly EnsembleConfig _config;
    private readonly ILLMClient _llmClient;
    private readonly ILogger<MultiModelOrchestrationService> _logger;
    private readonly CodeWikiOptions _options;
    private readonly IModelRoutingService _routingService;

    public MultiModelOrchestrationService(
        ILLMClient llmClient,
        IModelRoutingService routingService,
        EnsembleConfig config,
        ILogger<MultiModelOrchestrationService> logger,
        IOptions<CodeWikiOptions> options)
    {
        _llmClient = llmClient;
        _routingService = routingService;
        _config = config;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<MultiModelResult> GenerateWithEnsembleAsync(
        string systemPrompt,
        string userPrompt,
        List<ChatMessage> history,
        CancellationToken cancellationToken = default)
    {
        if (!_config.EnableEnsembleGeneration || _config.EnsembleModels.Count == 0)
        {
            _logger.LogWarning("Ensemble generation is disabled or no models configured, falling back to single model");

            // Fall back to single model generation
            var singleResult = await _llmClient.ChatAsync(systemPrompt, userPrompt, history, null, cancellationToken);
            return new MultiModelResult
            {
                SynthesizedContent = singleResult,
                ModelOutputs = new List<ModelOutput>(),
                AgreementScore = 1.0,
                Uncertainty = 0.0,
                ParticipatingModels = new List<string> { "default" }
            };
        }

        _logger.LogInformation("Starting ensemble generation with {Count} models", _config.EnsembleModels.Count);

        // Generate with all models in parallel
        var generateTasks = _config.EnsembleModels.Select(model =>
            GenerateWithModelAsync(model, systemPrompt, userPrompt, history, cancellationToken)
        ).ToList();

        var outputs = await Task.WhenAll(generateTasks);
        var successfulOutputs = outputs.Where(o => o != null).ToList()!;

        if (successfulOutputs.Count == 0)
        {
            _logger.LogError("All ensemble models failed to generate content");
            throw new InvalidOperationException("All ensemble models failed to generate content");
        }

        _logger.LogInformation("Generated content with {Success}/{Total} models successfully",
            successfulOutputs.Count, _config.EnsembleModels.Count);

        // Calculate agreement score
        var agreementScore = CalculateAgreementScore(successfulOutputs!);
        _logger.LogInformation("Agreement score: {Score:F2}", agreementScore);

        // Synthesize outputs using judge model
        var synthesizedContent = await SynthesizeOutputsAsync(
            successfulOutputs!, systemPrompt, userPrompt, cancellationToken);

        // Calculate uncertainty based on agreement
        var uncertainty = CalculateUncertainty(agreementScore, successfulOutputs.Count);

        return new MultiModelResult
        {
            SynthesizedContent = synthesizedContent,
            ModelOutputs = successfulOutputs!,
            AgreementScore = agreementScore,
            Uncertainty = uncertainty,
            ParticipatingModels = successfulOutputs.Select(o => o!.ModelName).ToList()
        };
    }

    /// <summary>
    ///     Generates content with a specific model and tracks timing.
    /// </summary>
    private async Task<ModelOutput?> GenerateWithModelAsync(
        string modelName,
        string systemPrompt,
        string userPrompt,
        List<ChatMessage> history,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Generating with model: {Model}", modelName);

            var content = await _llmClient.ChatAsync(
                systemPrompt, userPrompt, history, modelName, cancellationToken);

            stopwatch.Stop();

            _logger.LogDebug("Model {Model} completed in {Duration}ms",
                modelName, stopwatch.ElapsedMilliseconds);

            return new ModelOutput
            {
                ModelName = modelName,
                Content = content,
                GenerationTime = stopwatch.Elapsed,
                ConfidenceScore = 1.0 // Future: extract from model response
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "Model {Model} failed after {Duration}ms",
                modelName, stopwatch.ElapsedMilliseconds);
            return null;
        }
    }

    /// <summary>
    ///     Calculates agreement score based on content similarity.
    ///     Uses simple length-based heuristic for now; can be enhanced with semantic similarity.
    /// </summary>
    private double CalculateAgreementScore(List<ModelOutput> outputs)
    {
        if (outputs.Count < 2)
            return 1.0;

        // Simple heuristic: calculate variance in content length as proxy for agreement
        // Lower variance = higher agreement
        var lengths = outputs.Select(o => o.Content.Length).ToList();
        var avgLength = lengths.Average();

        if (avgLength == 0)
            return 0.0;

        var variance = lengths.Select(l => Math.Pow(l - avgLength, 2)).Average();
        var coefficientOfVariation = Math.Sqrt(variance) / avgLength;

        // Convert CV to agreement score (0-1 range)
        // Lower CV = higher agreement
        var agreementScore = Math.Max(0, 1.0 - coefficientOfVariation);

        return Math.Min(1.0, agreementScore);
    }

    /// <summary>
    ///     Synthesizes multiple model outputs into a single coherent result using a judge model.
    /// </summary>
    private async Task<string> SynthesizeOutputsAsync(
        List<ModelOutput> outputs,
        string originalSystemPrompt,
        string originalUserPrompt,
        CancellationToken cancellationToken)
    {
        var judgeModel = _routingService.SelectModelForTask(DocumentationTaskType.Synthesis);

        _logger.LogInformation("Synthesizing {Count} outputs using judge model: {Model}",
            outputs.Count, judgeModel ?? "default");

        // If only one output, return it directly
        if (outputs.Count == 1) return outputs[0].Content;

        // Build synthesis prompt
        var synthesisSystemPrompt = BuildSynthesisSystemPrompt();
        var synthesisUserPrompt = BuildSynthesisUserPrompt(outputs, originalUserPrompt);

        string synthesizedContent;
        var threshold = (int)(_llmClient.ContextSize * 3.5);
        if (synthesisUserPrompt.Length > threshold)
        {
            _logger.LogInformation("Synthesis prompt too large ({Length}). Using findings-based synthesis.",
                synthesisUserPrompt.Length);
            synthesizedContent = await _llmClient.ChatWithFindingsAsync(
                synthesisSystemPrompt,
                "Synthesize the following documentation drafts into a single high-quality result.",
                synthesisUserPrompt,
                judgeModel,
                cancellationToken,
                _options.UseSemanticChunking);
        }
        else
        {
            synthesizedContent = await _llmClient.ChatAsync(
                synthesisSystemPrompt,
                synthesisUserPrompt,
                new List<ChatMessage>(),
                judgeModel,
                cancellationToken);
        }

        return synthesizedContent;
    }

    /// <summary>
    ///     Builds system prompt for the synthesis judge.
    /// </summary>
    private string BuildSynthesisSystemPrompt()
    {
        return
            @"You are an expert documentation synthesis judge. Your role is to combine multiple documentation drafts into a single, coherent, high-quality result.

Guidelines:
- Identify the BEST elements from each draft (accuracy, clarity, completeness, examples)
- Merge them into a cohesive, well-structured document
- Preserve technical accuracy and code details
- Maintain consistent tone and style
- Remove redundancies and contradictions
- If drafts disagree on technical facts, favor the most detailed and specific explanation

Output ONLY the synthesized documentation content. Do not include preambles like 'Here is the synthesized version' or meta-commentary.";
    }

    /// <summary>
    ///     Builds user prompt for synthesis containing all model outputs.
    /// </summary>
    private string BuildSynthesisUserPrompt(List<ModelOutput> outputs, string originalPrompt)
    {
        var promptBuilder = new StringBuilder();

        promptBuilder.AppendLine("Original Request:");
        promptBuilder.AppendLine(originalPrompt);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("---");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine(
            $"I have {outputs.Count} documentation drafts from different models. Please synthesize them into the best possible result.");
        promptBuilder.AppendLine();

        for (var i = 0; i < outputs.Count; i++)
        {
            promptBuilder.AppendLine($"## Draft {i + 1} (from {outputs[i].ModelName})");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine(outputs[i].Content);
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("---");
            promptBuilder.AppendLine();
        }

        promptBuilder.AppendLine("Synthesize the above drafts into a single, high-quality documentation page.");

        return promptBuilder.ToString();
    }

    /// <summary>
    ///     Calculates uncertainty based on agreement score and number of models.
    /// </summary>
    private double CalculateUncertainty(double agreementScore, int modelCount)
    {
        // Base uncertainty from disagreement
        var baseUncertainty = 1.0 - agreementScore;

        // Reduce uncertainty with more models (diversity reduces risk)
        var diversityFactor = Math.Max(0.5, 1.0 - (modelCount - 1) * 0.1);

        var uncertainty = baseUncertainty * diversityFactor;

        // Clamp to valid range
        return Math.Max(0.0, Math.Min(1.0, uncertainty));
    }
}

/// <summary>
///     Configuration for ensemble generation (injected from Infrastructure layer).
/// </summary>
public class EnsembleConfig
{
    public bool EnableEnsembleGeneration { get; set; }
    public List<string> EnsembleModels { get; set; } = new();
    public int MinimumAgreementThreshold { get; set; }
}