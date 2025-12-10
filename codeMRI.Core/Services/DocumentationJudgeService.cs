using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.RegularExpressions;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
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
    
    [GeneratedRegex(@"```(?:json)?\s*(.*?)\s*```", RegexOptions.Singleline)]
    private static partial Regex MarkdownJsonBlockRegex();
    
    private readonly ILLMClient _llmClient;
    private readonly ILogger<DocumentationJudgeService> _logger;
    private readonly Meter _meter;
    private readonly IEvaluationPromptBuilder _promptBuilder;
    
    // Metrics
    private readonly Counter<long> _evaluationCounter;
    private readonly Histogram<double> _evaluationDuration;
    private readonly Histogram<double> _scoreDistribution;
    private readonly Counter<long> _failedModelsCounter;
    private readonly Histogram<double> _standardDeviationHistogram;

    public DocumentationJudgeService(
        ILogger<DocumentationJudgeService> logger,
        ILLMClient llmClient,
        IMeterFactory meterFactory,
        IEvaluationPromptBuilder promptBuilder)
    {
        _logger = logger;
        _llmClient = llmClient;
        _meter = meterFactory.Create("CodeMRI.Judge");
        _promptBuilder = promptBuilder;
        
        // Initialize metrics
        _evaluationCounter = _meter.CreateCounter<long>(
            "codemri.judge.evaluations.total",
            description: "Total number of requirement evaluations performed");
        
        _evaluationDuration = _meter.CreateHistogram<double>(
            "codemri.judge.evaluation.duration",
            unit: "ms",
            description: "Duration of requirement evaluation in milliseconds");
        
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
        
        _logger.LogInformation("Evaluating requirement: {RequirementTitle} (Model: {Model})", requirement.Title, model ?? "Default");

        var prompt = _promptBuilder.BuildPrompt(requirement, documentationStructure);

        var response = await _llmClient.ChatAsync(
            SystemPrompt,
            prompt,
            new List<ChatMessage>(),
            model,
            cancellationToken);

        var assessment = ParseAssessmentFromResponse(response, requirement);

        stopwatch.Stop();
        
        // Record metrics
        _evaluationCounter.Add(1, tags);
        _evaluationDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
        _scoreDistribution.Record(assessment.MeanScore, tags);
        
        _logger.LogInformation("Requirement assessment completed with score: {Score}", assessment.MeanScore);

        return assessment;
    }

    public async Task<List<RequirementAssessment>> EvaluateRequirementsAsync(
        List<RubricRequirement> requirements,
        WikiStructure documentationStructure,
        List<string> judgeModels,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Evaluating {RequirementCount} requirements using {JudgeCount} judges",
            requirements.Count, judgeModels.Count);

        var assessments = new List<RequirementAssessment>();

        foreach (var requirement in requirements)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
            {
                _standardDeviationHistogram.Record(aggregatedAssessment.StandardDeviation, new TagList
                {
                    { "requirement", requirement.Title },
                    { "judge_count", modelAssessments.Count }
                });
            }
            
            assessments.Add(aggregatedAssessment);
        }

        _logger.LogInformation("Completed evaluation of {RequirementCount} requirements", requirements.Count);
        return assessments;
    }

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
        try
        {
            // 1. Try to extract from markdown blocks
            if (response.Contains("```"))
            {
                var match = MarkdownJsonBlockRegex().Match(response);
                if (match.Success)
                {
                    response = match.Groups[1].Value;
                }
            }
            
            // 2. Fallback: Find first '{' and last '}' to handle chatty responses
            var startIdx = response.IndexOf('{');
            var endIdx = response.LastIndexOf('}');
            
            if (startIdx >= 0 && endIdx > startIdx)
            {
                response = response.Substring(startIdx, endIdx - startIdx + 1);
            }

            var assessment = JsonSerializer.Deserialize<JudgeResponse>(response, JsonParsingOptions);

            if (assessment == null)
            {
                _logger.LogWarning("Failed to parse assessment from LLM response");
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
                Evidence = assessment.Evidence ?? new List<string>()
            };
        }
        catch (JsonException ex)
        {
            var preview = response.Length > 500 ? response.Substring(0, 500) + "..." : response;
            _logger.LogError(ex, "Error parsing assessment JSON from LLM response. Raw response (first 500 chars): {ResponsePreview}", preview);
            return CreateDefaultAssessment(requirement);
        }
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