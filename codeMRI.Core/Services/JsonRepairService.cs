using System.Text.Json;

namespace codeMRI.Core.Services;

/// <summary>
///     Service for extracting and repairing JSON from potentially malformed LLM responses
/// </summary>
public class JsonRepairService : Interfaces.IJsonRepairService
{
    public string? ExtractJsonString(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return response;

        var trimmed = response.Trim();

        // Remove markdown code blocks
        trimmed = RemoveMarkdownCodeBlocks(trimmed);

        // Find JSON object boundaries using brace counting
        var firstBrace = trimmed.IndexOf('{');
        if (firstBrace < 0)
            return trimmed; // No JSON found, return as-is

        var lastBrace = FindMatchingClosingBrace(trimmed, firstBrace);
        if (lastBrace < 0)
            return trimmed; // No matching brace, return as-is

        // Extract the JSON object
        return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
    }

    private static string RemoveMarkdownCodeBlocks(string text)
    {
        var trimmed = text.Trim();

        // Remove markdown code blocks
        if (trimmed.StartsWith("```json"))
        {
            trimmed = trimmed.Substring(7); // Remove ```json
        }
        else if (trimmed.StartsWith("```"))
        {
            trimmed = trimmed.Substring(3); // Remove ```
        }

        if (trimmed.EndsWith("```"))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - 3); // Remove trailing ```
        }

        return trimmed.Trim();
    }

    private static int FindMatchingClosingBrace(string text, int startIndex)
    {
        var braceCount = 0;
        var inString = false;
        var escapeNext = false;

        for (var i = startIndex; i < text.Length; i++)
        {
            var c = text[i];

            if (escapeNext)
            {
                escapeNext = false;
                continue;
            }

            if (c == '\\')
            {
                escapeNext = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (!inString)
            {
                if (c == '{')
                {
                    braceCount++;
                }
                else if (c == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        return i; // Found matching closing brace
                    }
                }
            }
        }

        return -1; // No matching brace found
    }

    public T? ExtractAndDeserialize<T>(string response, JsonSerializerOptions? options = null)
        where T : class
    {
        try
        {
            // Extract JSON string
            var jsonString = ExtractJsonString(response);
            if (string.IsNullOrWhiteSpace(jsonString))
                return null;

            // Repair JSON
            jsonString = RepairJson(jsonString);

            // Deserialize
            options ??= new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };

            return JsonSerializer.Deserialize<T>(jsonString, options);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public string RepairJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json;

        // Remove trailing commas before closing braces and brackets
        json = System.Text.RegularExpressions.Regex.Replace(json, @",\s*}", "}");
        json = System.Text.RegularExpressions.Regex.Replace(json, @",\s*]", "]");

        return json;
    }
}
