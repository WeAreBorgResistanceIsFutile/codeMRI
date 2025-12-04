using System.Globalization;
using System.Text.RegularExpressions;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
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
            var systemPrompt = BuildSystemPrompt();
            var userPrompt = BuildEvaluationPrompt(page, requirement);
            
            var response = await _llmClient.ChatAsync(systemPrompt, userPrompt, new List<ChatMessage>());
            var result = ParseEvaluationResponse(response, requirement.Title);

            _logger.LogInformation("Evaluation completed for {Requirement}: Score={Score}",
                requirement.Title, result.Score);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating requirement: {Requirement}", requirement.Title);
            return new RequirementScore
            {
                RequirementId = requirement.Title,
                Score = 0.5, // Default middle score for errors
                Reasoning = $"Error evaluating requirement: {ex.Message}"
            };
        }
    }

    private static string BuildSystemPrompt()
    {
        return """
            You are an expert technical documentation evaluator. Your task is to evaluate documentation against specific requirements.
            
            You must provide a binary decision (Yes/No) followed by your reasoning. However, if the documentation partially meets the requirement,
            you may provide a confidence score between 0.0 and 1.0.
            
            Guidelines:
            - Answer "Yes" if the documentation fully satisfies the requirement
            - Answer "No" if the documentation does not satisfy the requirement
            - Provide a percentage or decimal score (e.g., "75%", "0.6") for partial satisfaction
            - Always explain your reasoning clearly
            - Be objective and consistent in your evaluations
            """;
    }

    private static string BuildEvaluationPrompt(WikiPage page, RubricRequirement requirement)
    {
        return $"""
            Documentation Content:
            {page.Content}

            Requirement: {requirement.Description}

            Please evaluate if this documentation satisfies the requirement above.
            
            Answer with either "Yes" or "No" (or a confidence score like "75%") and provide detailed reasoning.
            """;
    }

    private static RequirementScore ParseEvaluationResponse(string response, string requirementId)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return new RequirementScore
            {
                RequirementId = requirementId,
                Score = 0.5,
                Reasoning = "No response provided"
            };
        }

        var trimmedResponse = response.Trim();
        
        // Try to extract percentage scores first (e.g., "75%", "90 percent")
        var percentageMatch = Regex.Match(trimmedResponse, @"(\d+(?:\.\d+)?)\s*%|(\d+(?:\.\d+)?)\s*percent", RegexOptions.IgnoreCase);
        if (percentageMatch.Success)
        {
            var percentageValue = percentageMatch.Groups[1].Success ? 
                percentageMatch.Groups[1].Value : 
                percentageMatch.Groups[2].Value;
            
            if (double.TryParse(percentageValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var percentage))
            {
                return new RequirementScore
                {
                    RequirementId = requirementId,
                    Score = Math.Clamp(percentage / 100.0, 0.0, 1.0),
                    Reasoning = trimmedResponse
                };
            }
        }

        // Try to extract decimal scores with various patterns
        // Pattern 1: "score: 0.85", "rating: 0.72", "score is 0.45"
        var decimalMatch = Regex.Match(trimmedResponse, @"(?:score|rating)\s*(?:is|=|:)?\s*(-?\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
        if (decimalMatch.Success)
        {
            var scoreText = decimalMatch.Groups[1].Value.TrimEnd('.');
            if (double.TryParse(scoreText, NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalScore))
            {
                return new RequirementScore
                {
                    RequirementId = requirementId,
                    Score = Math.Clamp(decimalScore, 0.0, 1.0),
                    Reasoning = trimmedResponse
                };
            }
        }

        // Pattern 2: "scores 0.7", "score of -0.2", "give it a score of 1.2" - look for score-related words followed by numbers
        var scoreContextMatch = Regex.Match(trimmedResponse, @"(?:scores?|gives?|rate[sd]?)\s+(?:it\s+)?(?:a\s+)?(?:score\s+)?(?:of\s+)?(-?\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
        if (scoreContextMatch.Success)
        {
            var scoreText = scoreContextMatch.Groups[1].Value.TrimEnd('.');
            if (double.TryParse(scoreText, NumberStyles.Float, CultureInfo.InvariantCulture, out var contextScore))
            {
                return new RequirementScore
                {
                    RequirementId = requirementId,
                    Score = Math.Clamp(contextScore, 0.0, 1.0),
                    Reasoning = trimmedResponse
                };
            }
        }

        // Pattern 3: Look for any decimal numbers between 0 and 1 (including negative for clamping)
        var decimalMatches = Regex.Matches(trimmedResponse, @"\b(-?\d+(?:\.\d+)?)\b");
        foreach (Match match in decimalMatches)
        {
            if (double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var standaloneScore))
            {
                // Only consider numbers that could be valid scores (0-1 range or close to it)
                if (Math.Abs(standaloneScore) <= 1.5) // Allow some tolerance for numbers like 1.2
                {
                    return new RequirementScore
                    {
                        RequirementId = requirementId,
                        Score = Math.Clamp(standaloneScore, 0.0, 1.0),
                        Reasoning = trimmedResponse
                    };
                }
            }
        }

        // Check for Yes/No answers
        var normalizedResponse = trimmedResponse.ToLowerInvariant();
        
        if (normalizedResponse.StartsWith("yes") || 
            normalizedResponse.Contains("\nyes") ||
            normalizedResponse.StartsWith("yes,") ||
            normalizedResponse.StartsWith("yes:") ||
            normalizedResponse.StartsWith("yes "))
        {
            return new RequirementScore
            {
                RequirementId = requirementId,
                Score = 1.0,
                Reasoning = ExtractReasoning(trimmedResponse, "yes")
            };
        }

        if (normalizedResponse.StartsWith("no") || 
            normalizedResponse.Contains("\nno") ||
            normalizedResponse.StartsWith("no,") ||
            normalizedResponse.StartsWith("no:") ||
            normalizedResponse.StartsWith("no "))
        {
            return new RequirementScore
            {
                RequirementId = requirementId,
                Score = 0.0,
                Reasoning = ExtractReasoning(trimmedResponse, "no")
            };
        }

        // Default case - couldn't parse a clear score
        return new RequirementScore
        {
            RequirementId = requirementId,
            Score = 0.5, // Middle score for ambiguous responses
            Reasoning = trimmedResponse
        };
    }

    private static string ExtractReasoning(string response, string prefix)
    {
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        // Look for the line with the Yes/No answer
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim().ToLowerInvariant();
            if (line.StartsWith(prefix))
            {
                // Extract reasoning from the same line after the prefix
                var sameLineReasoning = lines[i].Substring(prefix.Length).Trim();
                if (!string.IsNullOrWhiteSpace(sameLineReasoning))
                {
                    // Remove common punctuation at the start
                    sameLineReasoning = Regex.Replace(sameLineReasoning, @"^[,:.\-\s]+", "");
                    if (!string.IsNullOrWhiteSpace(sameLineReasoning))
                        return sameLineReasoning;
                }
                
                // Use subsequent lines as reasoning
                if (i + 1 < lines.Length)
                {
                    var subsequentLines = string.Join(" ", lines.Skip(i + 1).Select(l => l.Trim()));
                    return string.IsNullOrWhiteSpace(subsequentLines) ? response.Trim() : subsequentLines;
                }
            }
        }
        
        return response.Trim();
    }
}