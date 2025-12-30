using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.RegularExpressions;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services.MessageComposition;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services;

public partial class DocumentationJudgeService : IDocumentationJudgeService
{
    private const string SystemPrompt = "You are a technical documentation evaluator.";

    private static readonly JsonSerializerOptions JsonParsingOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    // Metrics
    private readonly Counter<long> _evaluationCounter;
    private readonly Histogram<double> _evaluationDuration;
    private readonly Counter<long> _failedModelsCounter;

    private readonly ILLMServiceFacade _llmFacade;
    private readonly ILogger<DocumentationJudgeService> _logger;
    private readonly Meter _meter;
    private readonly IEvaluationPromptBuilder _defaultPromptBuilder;
    private readonly RagEvaluationPromptBuilder _ragPromptBuilder;
    private readonly Histogram<double> _scoreDistribution;
    private readonly Histogram<double> _standardDeviationHistogram;

    public DocumentationJudgeService(
        ILogger<DocumentationJudgeService> logger,
        ILLMServiceFacade llmFacade,
        IMeterFactory meterFactory,
        IEvaluationPromptBuilder promptBuilder,
        RagEvaluationPromptBuilder ragPromptBuilder)
    {
        _logger = logger;
        _llmFacade = llmFacade;
        _meter = meterFactory.Create("CodeMRI.Judge");
        _defaultPromptBuilder = promptBuilder;
        _ragPromptBuilder = ragPromptBuilder;

        // Initialize metrics
        _evaluationCounter = _meter.CreateCounter<long>(
            "codemri.judge.evaluations.total",
            description: "Total number of requirement evaluations performed");

        _evaluationDuration = _meter.CreateHistogram<double>(
            "codemri.judge.evaluation.duration",
            "ms",
            "Duration of requirement evaluation in milliseconds");

        _scoreDistribution = _meter.CreateHistogram<double>(
            "codemri.judge.scores",
            description: "Distribution of evaluation scores (0-1)");

        _failedModelsCounter = _meter.CreateCounter<long>(
            "codemri.judge.models.failed",
            description: "Number of failed model evaluations");

        _standardDeviationHistogram = _meter.CreateHistogram<double>(
            "codemri.judge.standard_deviation",
            description: "Standard deviation of scores across multiple judges");
    }

    public async Task<RequirementAssessment> EvaluateRequirementAsync(
        RubricRequirement requirement,
        WikiStructure documentationStructure,
        CancellationToken cancellationToken = default,
        string? model = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var tags = new TagList
        {
            { "requirement", requirement.Title },
            { "model", model ?? "default" }
        };

        string prompt = await _ragPromptBuilder.BuildPromptAsync(requirement, documentationStructure);

        const int maxRetries = 1;
        
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var llmResponse = await _llmFacade.ExecuteAsync(
                    systemPrompt: SystemPrompt,
                    textToProcess: prompt,
                    history: null,
                    options: new MessageCompositionOptions { ModelName = model, UseRag = true},
                    cancellationToken: cancellationToken);

                var assessment = ParseAssessmentFromResponse(llmResponse.Content, requirement);

                stopwatch.Stop();

                // Record metrics
                _evaluationCounter.Add(1, tags);
                _evaluationDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
                _scoreDistribution.Record(assessment.MeanScore, tags);

                _logger.LogInformation("Requirement assessment completed with score: {Score}", assessment.MeanScore);

                return assessment;
            }
            catch (JsonException ex)
            {
                if (attempt == maxRetries)
                {
                    _logger.LogError(ex, "Failed to parse assessment JSON after {Retries} retries", maxRetries);
                    
                    stopwatch.Stop();
                    // Record failure metric
                    _evaluationCounter.Add(1, tags); 
                    // Should we record failure as 0 score? Yes, default assessment does that.
                    
                    return CreateDefaultAssessment(requirement);
                }

                _logger.LogWarning(ex, "JSON parsing failed (Attempt {Attempt}/{Max}), retrying with strict instruction.", attempt + 1, maxRetries);
                
                // Add strong instruction for the retry
                prompt += "\n\nIMPORTANT: The previous response failed to parse as JSON. You MUST return valid, strict JSON format only. Do not include markdown formatting or extra text.";
            }
        }
        
        return CreateDefaultAssessment(requirement); // Should not be reached
    }

    public async Task<List<RequirementAssessment>> EvaluateRequirementsAsync(
        List<RubricRequirement> requirements,
        WikiStructure documentationStructure,
        List<string> judgeModels,
        int maxConcurrency = 5,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Evaluating {RequirementCount} requirements using {JudgeCount} judges (Concurrency: {Concurrency})",
            requirements.Count, judgeModels.Count, maxConcurrency);

        var assessments = new List<RequirementAssessment>();
        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var tasks = new List<Task<RequirementAssessment>>();

        foreach (var requirement in requirements)
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var modelAssessments = new List<ModelAssessment>();
                    var failedModels = new List<string>();

                    foreach (var modelName in judgeModels)
                        try
                        {
                            var assessment = await EvaluateRequirementAsync(
                                requirement, documentationStructure, cancellationToken, modelName);

                            modelAssessments.Add(new ModelAssessment
                            {
                                ModelName = modelName,
                                Score = assessment.MeanScore,
                                Reasoning = string.Join("; ", assessment.Reasoning),
                                Evidence = assessment.Evidence
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to evaluate with model: {ModelName}", modelName);
                            failedModels.Add(modelName);

                            // Record failed model metric
                            _failedModelsCounter.Add(1, new TagList
                            {
                                { "requirement", requirement.Title },
                                { "model", modelName }
                            });
                        }

                    var aggregatedAssessment = AggregateAssessments(modelAssessments, requirement, failedModels);

                    // Record standard deviation metric if multiple judges
                    if (modelAssessments.Count > 1)
                        _standardDeviationHistogram.Record(aggregatedAssessment.StandardDeviation, new TagList
                        {
                            { "requirement", requirement.Title },
                            { "judge_count", modelAssessments.Count }
                        });

                    return aggregatedAssessment;
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));

        var results = await Task.WhenAll(tasks);
        assessments.AddRange(results);

        _logger.LogInformation("Completed evaluation of {RequirementCount} requirements", requirements.Count);
        return assessments;
    }

    [GeneratedRegex(@"```(?:json)?\s*(.*?)\s*```", RegexOptions.Singleline)]
    private static partial Regex MarkdownJsonBlockRegex();

    private RequirementAssessment AggregateAssessments(
        List<ModelAssessment> modelAssessments,
        RubricRequirement requirement,
        List<string> failedModels)
    {
        if (!modelAssessments.Any())
            return new RequirementAssessment
            {
                RequirementId = requirement.Title,
                RequirementTitle = requirement.Title,
                MeanScore = 0.0,
                StandardDeviation = 0.0,
                IndividualScores = new List<double>(),
                Reasoning = new List<string> { "No assessments available" },
                Evidence = new List<string>(),
                FailedModels = failedModels
            };

        var scores = modelAssessments.Select(m => m.Score).ToList();
        var meanScore = scores.Average();
        var variance = scores.Select(s => Math.Pow(s - meanScore, 2)).Average();
        var standardDeviation = Math.Sqrt(variance);

        return new RequirementAssessment
        {
            RequirementId = requirement.Title,
            RequirementTitle = requirement.Title,
            MeanScore = Math.Clamp(meanScore, 0.0, 1.0),
            StandardDeviation = standardDeviation,
            IndividualScores = scores,
            Reasoning = modelAssessments.Select(m => m.Reasoning).ToList(),
            Evidence = modelAssessments.SelectMany(m => m.Evidence).ToList(),
            FailedModels = failedModels
        };
    }


    private RequirementAssessment ParseAssessmentFromResponse(string response, RubricRequirement requirement)
    {
        // 1. Try to extract from markdown blocks
        if (response.Contains("```"))
        {
            var match = MarkdownJsonBlockRegex().Match(response);
            if (match.Success)
            {
                response = match.Groups[1].Value;
            }
            else
            {
                // Fallback: strip leading ```json or ``` if present, to help with truncated responses
                var trimmed = response.TrimStart();
                if (trimmed.StartsWith("```"))
                {
                    var newlineIndex = trimmed.IndexOf('\n');
                    if (newlineIndex >= 0)
                        response = trimmed.Substring(newlineIndex + 1);
                    else if (trimmed.Length >= 3)
                        response = trimmed.Substring(3);
                }
            }
        }

        // 2. Fallback: Use bracket counting or simple index finding to extract valid JSON
        var extractedJson = ExtractValidJson(response);
        if (!string.IsNullOrWhiteSpace(extractedJson)) response = extractedJson;

        var assessment = JsonSerializer.Deserialize<JudgeResponse>(response, JsonParsingOptions);

        if (assessment == null || !IsValidAssessment(assessment))
        {
            _logger.LogWarning("Failed to parse valid assessment from LLM response");
            return CreateDefaultAssessment(requirement);
        }

        return new RequirementAssessment
        {
            RequirementId = requirement.Title,
            RequirementTitle = requirement.Title,
            MeanScore = Math.Clamp(assessment.Score, 0.0, 1.0),
            StandardDeviation = 0.0,
            IndividualScores = new List<double> { Math.Clamp(assessment.Score, 0.0, 1.0) },
            Reasoning = new List<string> { assessment.Reasoning ?? "No reasoning provided" },
            Evidence = assessment.Evidence?.Select(e => e.ToString()).ToList() ?? new List<string>()
        };
    }

    /// <summary>
    ///     Extracts a valid JSON object by finding the first '{' and the last '}'.
    ///     This is more robust against chatty introductions and conclusions.
    /// </summary>
    private static string? ExtractValidJson(string response)
    {
        var startIdx = response.IndexOf('{');
        var endIdx = response.LastIndexOf('}');

        if (startIdx >= 0 && endIdx > startIdx) return response.Substring(startIdx, endIdx - startIdx + 1);
        return null;
    }

    /// <summary>
    ///     Validates that a parsed assessment has reasonable values.
    ///     We allow scores slightly outside 0-1 range since we clamp them anyway.
    /// </summary>
    private static bool IsValidAssessment(JudgeResponse response)
    {
        return response.Score >= -1.0 && response.Score <= 2.0;
        // Allow for clamping
    }

    private RequirementAssessment CreateDefaultAssessment(RubricRequirement requirement)
    {
        return new RequirementAssessment
        {
            RequirementId = requirement.Title,
            RequirementTitle = requirement.Title,
            MeanScore = 0.0,
            StandardDeviation = 0.0,
            IndividualScores = new List<double> { 0.0 },
            Reasoning = new List<string> { "Failed to evaluate requirement" },
            Evidence = new List<string>()
        };
    }
}