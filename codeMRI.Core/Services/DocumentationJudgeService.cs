using System.Text.Json;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services;

public class DocumentationJudgeService : IDocumentationJudgeService
{
    private readonly ILLMClient _llmClient;
    private readonly ILogger<DocumentationJudgeService> _logger;

    public DocumentationJudgeService(
        ILogger<DocumentationJudgeService> logger,
        ILLMClient llmClient)
    {
        _logger = logger;
        _llmClient = llmClient;
    }

    public async Task<RequirementAssessment> EvaluateRequirementAsync(
        RubricRequirement requirement,
        WikiStructure documentationStructure,
        CancellationToken cancellationToken = default,
        string? model = null)
    {
        _logger.LogInformation("Evaluating requirement: {RequirementTitle} (Model: {Model})", requirement.Title, model ?? "Default");

        var prompt = BuildEvaluationPrompt(requirement, documentationStructure);

        var response = await _llmClient.ChatAsync(
            "You are a technical documentation evaluator.",
            prompt,
            new List<ChatMessage>(),
            model);

        var assessment = ParseAssessmentFromResponse(response, requirement);

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

        // Evaluate with each judge model
        foreach (var requirement in requirements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var modelAssessments = new List<ModelAssessment>();

            foreach (var modelName in judgeModels)
                try
                {
                    var assessment = await EvaluateRequirementWithModelAsync(
                        requirement, documentationStructure, modelName, cancellationToken);

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
                }

            // Aggregate assessments
            var aggregatedAssessment = AggregateAssessments(modelAssessments, requirement);
            assessments.Add(aggregatedAssessment);
        }

        _logger.LogInformation("Completed evaluation of {RequirementCount} requirements", requirements.Count);
        return assessments;
    }

    private async Task<RequirementAssessment> EvaluateRequirementWithModelAsync(
        RubricRequirement requirement,
        WikiStructure documentationStructure,
        string modelName,
        CancellationToken cancellationToken)
    {
        return await EvaluateRequirementAsync(requirement, documentationStructure, cancellationToken, modelName);
    }

    private RequirementAssessment AggregateAssessments(
        List<ModelAssessment> modelAssessments,
        RubricRequirement requirement)
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
                Evidence = new List<string>()
            };

        var scores = modelAssessments.Select(m => m.Score).ToList();
        var meanScore = scores.Average();
        var variance = scores.Select(s => Math.Pow(s - meanScore, 2)).Average();
        var standardDeviation = Math.Sqrt(variance);

        return new RequirementAssessment
        {
            RequirementId = requirement.Title,
            RequirementTitle = requirement.Title,
            MeanScore = meanScore,
            StandardDeviation = standardDeviation,
            IndividualScores = scores,
            Reasoning = modelAssessments.Select(m => m.Reasoning).ToList(),
            Evidence = modelAssessments.SelectMany(m => m.Evidence).ToList()
        };
    }

    private string BuildEvaluationPrompt(RubricRequirement requirement, WikiStructure documentationStructure)
    {
        return $@"
You are evaluating technical documentation against a specific requirement.

Requirement: {requirement.Title}
Description: {requirement.Description}

Documentation Structure (use search tool to explore):
{FormatDocumentationStructure(documentationStructure)}

Task:
1. Search the documentation for content related to this requirement
2. Determine if the requirement is adequately satisfied (score 0-1)
3. Provide brief reasoning (max 50 words)
4. Identify specific evidence sections

Scoring Criteria:
Score 1 if:
- The documentation clearly addresses this requirement
- Information is accurate and complete
- Examples are provided where appropriate
- Content is easy to find and understand

Score 0 if:
- Requirement is not mentioned
- Information is incomplete or unclear
- Critical details are missing
- Content is difficult to locate

Respond with JSON format:
{{
    ""requirement_id"": ""{requirement.Title}"",
    ""score"": 0.0,
    ""reasoning"": ""Brief explanation"",
    ""evidence"": [""doc_section_1"", ""doc_section_2""]
}}";
    }

    private string FormatDocumentationStructure(WikiStructure structure)
    {
        // Simple formatting - in real implementation would be more sophisticated
        return JsonSerializer.Serialize(structure, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private RequirementAssessment ParseAssessmentFromResponse(string response, RubricRequirement requirement)
    {
        try
        {
            // Clean up response if wrapped in markdown code blocks
            if (response.Contains("```"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(response, @"```(?:json)?\s*(.*?)\s*```", System.Text.RegularExpressions.RegexOptions.Singleline);
                if (match.Success)
                {
                    response = match.Groups[1].Value;
                }
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var assessment = JsonSerializer.Deserialize<JudgeResponse>(response, options);

            if (assessment == null)
            {
                _logger.LogWarning("Failed to parse assessment from LLM response");
                return CreateDefaultAssessment(requirement);
            }

            return new RequirementAssessment
            {
                RequirementId = requirement.Title,
                RequirementTitle = requirement.Title,
                MeanScore = assessment.Score,
                StandardDeviation = 0.0, // Single assessment, no deviation
                IndividualScores = new List<double> { assessment.Score },
                Reasoning = new List<string> { assessment.Reasoning },
                Evidence = assessment.Evidence ?? new List<string>()
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing assessment JSON from LLM response");
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

public interface IDocumentationJudgeService
{
    Task<RequirementAssessment> EvaluateRequirementAsync(
        RubricRequirement requirement,
        WikiStructure documentationStructure,
        CancellationToken cancellationToken = default,
        string? model = null);

    Task<List<RequirementAssessment>> EvaluateRequirementsAsync(
        List<RubricRequirement> requirements,
        WikiStructure documentationStructure,
        List<string> judgeModels,
        CancellationToken cancellationToken = default);
}

public class RequirementAssessment
{
    public string RequirementId { get; set; } = string.Empty;
    public string RequirementTitle { get; set; } = string.Empty;
    public double MeanScore { get; set; }
    public double StandardDeviation { get; set; }
    public List<double> IndividualScores { get; set; } = new();
    public List<string> Reasoning { get; set; } = new();
    public List<string> Evidence { get; set; } = new();
}

public class ModelAssessment
{
    public string ModelName { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public List<string> Evidence { get; set; } = new();
}

internal class JudgeResponse
{
    public string RequirementId { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public List<string>? Evidence { get; set; }
}