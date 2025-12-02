using codeMRI.Core.Interfaces;
using codeMRI.Shared.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services;

public class JudgeAgentService : IJudgeAgent
{
    private readonly ILLMClient _llmClient;
    private readonly ILogger<IJudgeAgent> _logger;

    public JudgeAgentService(ILogger<IJudgeAgent> logger, ILLMClient llmClient)
    {
        _logger = logger;
        _llmClient = llmClient;
    }

    public async Task<RequirementScore> EvaluateRequirementAsync(WikiPage page, RubricRequirement? requirement)
    {
        if (page == null) throw new ArgumentNullException(nameof(page));
        if (requirement == null) throw new ArgumentNullException(nameof(requirement));

        _logger.LogInformation("Starting evaluation for requirement: {Requirement}", requirement.Title);

        try
        {
            var prompt = BuildEvaluationPrompt(page.Content, requirement.Description);
            var response = await _llmClient.ChatAsync(
                "You are a documentation evaluation assistant.",
                prompt,
                new List<ChatMessage>());

            var result = ParseEvaluationResponse(response, requirement.Title);

            _logger.LogInformation("Evaluation completed for {Requirement}: Score={Score}",
                requirement.Title, result.Score);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM evaluation failed for requirement: {Requirement}", requirement.Title);
            return new RequirementScore
            {
                RequirementId = requirement.Title,
                Score = 0.0,
                Reasoning = $"LLM evaluation failed: {ex.Message}"
            };
        }
    }

    private static string BuildEvaluationPrompt(string documentationContent, string requirementDescription)
    {
        return $"""
                Read this documentation:
                {documentationContent}

                Does it satisfy this requirement: {requirementDescription}?

                Answer Yes/No and explain.
                """;
    }

    private static RequirementScore ParseEvaluationResponse(string response, string requirementId)
    {
        if (string.IsNullOrWhiteSpace(response))
            return new RequirementScore
            {
                RequirementId = requirementId,
                Score = 0.0,
                Reasoning = "Empty response received from LLM"
            };

        var normalizedResponse = response.Trim().ToLowerInvariant();

        // Parse binary score (0 or 1) based on Yes/No answer
        var score = 0.0;
        var reasoning = response.Trim();

        if (normalizedResponse.StartsWith("yes") ||
            normalizedResponse.StartsWith("yes,") ||
            normalizedResponse.StartsWith("yes:") ||
            normalizedResponse.StartsWith("yes ") ||
            normalizedResponse.StartsWith("yes-"))
        {
            score = 1.0;
            // Extract reasoning after the "Yes" part
            reasoning = ExtractReasoning(response, "yes");
        }
        else if (normalizedResponse.StartsWith("no") ||
                 normalizedResponse.StartsWith("no,") ||
                 normalizedResponse.StartsWith("no:") ||
                 normalizedResponse.StartsWith("no ") ||
                 normalizedResponse.StartsWith("no-"))
        {
            score = 0.0;
            reasoning = ExtractReasoning(response, "no");
        }

        return new RequirementScore
        {
            RequirementId = requirementId,
            Score = score,
            Reasoning = reasoning
        };
    }

    private static string ExtractReasoning(string response, string prefix)
    {
        var normalizedPrefix = prefix.ToLowerInvariant();
        var responseLower = response.ToLowerInvariant();

        // Find the position after the prefix
        var prefixIndex = responseLower.IndexOf(normalizedPrefix);
        if (prefixIndex >= 0)
        {
            var reasoningStart = prefixIndex + prefix.Length;

            // Skip any punctuation or whitespace immediately after the prefix
            while (reasoningStart < response.Length &&
                   (char.IsPunctuation(response[reasoningStart]) ||
                    char.IsWhiteSpace(response[reasoningStart])))
                reasoningStart++;

            if (reasoningStart < response.Length)
            {
                var reasoning = response.Substring(reasoningStart).Trim();
                return string.IsNullOrWhiteSpace(reasoning) ? response.Trim() : reasoning;
            }
        }

        return response.Trim();
    }
}