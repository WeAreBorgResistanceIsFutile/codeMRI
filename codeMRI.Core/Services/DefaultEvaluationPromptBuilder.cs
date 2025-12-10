using System.Text.Json;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;

namespace codeMRI.Core.Services;

/// <summary>
/// Default implementation of prompt building for documentation evaluation.
/// Uses a comprehensive prompt with scoring criteria and JSON format instructions.
/// </summary>
public class DefaultEvaluationPromptBuilder : IEvaluationPromptBuilder
{
    public string BuildPrompt(RubricRequirement requirement, WikiStructure documentationStructure)
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

Respond with strict JSON format ONLY. Do not include markdown formatting, explanations, or any other text.
{{
    ""requirement_id"": ""{requirement.Title}"",
    ""score"": 0.0,
    ""reasoning"": ""Brief explanation"",
    ""evidence"": [""doc_section_1"", ""doc_section_2""]
}}";
    }

    private string FormatDocumentationStructure(WikiStructure structure)
    {
        return JsonSerializer.Serialize(structure, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
