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

            // Get type structure for schema-aware repair
            var targetType = typeof(T);
            
            // Repair JSON with schema awareness
            jsonString = RepairJsonWithSchema(jsonString, targetType);

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

    private static string RepairJsonWithSchema(string json, Type targetType)
    {
        // First pass: Basic repair (trailing commas)
        json = System.Text.RegularExpressions.Regex.Replace(json, @",\s*}", "}");
        json = System.Text.RegularExpressions.Regex.Replace(json, @",\s*]", "]");

        // Get expected properties from target type
        var expectedProperties = targetType.GetProperties()
            .Where(p => p.CanWrite)
            .Select(p => new PropertyInfo 
            { 
                Name = p.Name.ToLowerInvariant(), 
                Type = p.PropertyType,
                OriginalName = p.Name
            })
            .ToList();

        // Second pass: Schema-aware repair
        json = RepairIncompleteJsonWithSchema(json, expectedProperties);

        return json;
    }

    private class PropertyInfo
    {
        public string Name { get; set; } = "";
        public Type Type { get; set; } = typeof(object);
        public string OriginalName { get; set; } = "";
    }

    private static string RepairIncompleteJsonWithSchema(string json, List<PropertyInfo> expectedProperties)
    {
        var result = new System.Text.StringBuilder();
        var inString = false;
        var escapeNext = false;
        var openBraces = 0;
        var currentPropertyName = new System.Text.StringBuilder();
        var collectingPropertyName = false;
        var afterColon = false;

        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];

            // Handle escape sequences
            if (escapeNext)
            {
                result.Append(c);
                if (collectingPropertyName && inString)
                    currentPropertyName.Append(c);
                escapeNext = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                result.Append(c);
                if (collectingPropertyName)
                    currentPropertyName.Append(c);
                escapeNext = true;
                continue;
            }

            // Handle quotes
            if (c == '"')
            {
                if (!inString)
                {
                    inString = true;
                    result.Append(c);
                    
                    // Check if this might be a property name (after { or ,)
                    var trimmed = result.ToString().TrimEnd('"');
                    var lastChar = trimmed.Length > 0 ? trimmed[trimmed.Length - 1] : ' ';
                    if (lastChar == '{' || lastChar == ',')
                    {
                        collectingPropertyName = true;
                        currentPropertyName.Clear();
                    }
                }
                else
                {
                    inString = false;
                    result.Append(c);
                    
                    if (collectingPropertyName)
                    {
                        collectingPropertyName = false;
                        // Property name collected, check if it's valid
                    }
                }
                continue;
            }

            // If in string, append and track property name if collecting
            if (inString)
            {
                result.Append(c);
                if (collectingPropertyName)
                    currentPropertyName.Append(c);
                continue;
            }

            // Track structural characters
            if (c == '{')
            {
                openBraces++;
                result.Append(c);
            }
            else if (c == '}')
            {
                openBraces--;
                result.Append(c);
            }
            else if (c == ':')
            {
                result.Append(c);
                afterColon = true;
            }
            else if (c == ',')
            {
                result.Append(c);
                afterColon = false;
            }
            else
            {
                result.Append(c);
            }
        }

        // Close any unclosed strings
        if (inString)
        {
            // Clean up trailing comma or special characters from unclosed string
            var currentResultString = result.ToString();
            var lastNonWhitespace = currentResultString.Length - 1;
            while (lastNonWhitespace >= 0 && char.IsWhiteSpace(currentResultString[lastNonWhitespace]))
                lastNonWhitespace--;
            
            // If the string ends with a comma, it's likely an incomplete string value
            if (lastNonWhitespace >= 0 && currentResultString[lastNonWhitespace] == ',')
            {
                result.Length = lastNonWhitespace; // Remove the comma
            }
            
            result.Append('"');
        }

        var resultStr = result.ToString();

        // Remove incomplete trailing content using schema information
        resultStr = RemoveIncompleteTrailingContent(resultStr, expectedProperties);

        // Close unclosed braces
        while (openBraces > 0)
        {
            resultStr += "}";
            openBraces--;
        }

        return resultStr;
    }

    private static string RemoveIncompleteTrailingContent(string json, List<PropertyInfo> expectedProperties)
    {
        // Find the last complete property-value pair
        var inString = false;
        var escapeNext = false;
        var braceDepth = 0;
        var bracketDepth = 0;
        var lastCompletePosition = -1;
        var currentState = "seeking"; // seeking, inPropertyName, afterColon, inValue

        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];

            if (escapeNext)
            {
                escapeNext = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escapeNext = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                
                if (!inString && currentState == "inValue" && braceDepth == 1 && bracketDepth == 0)
                {
                    // Completed a string value
                    lastCompletePosition = i + 1;
                    currentState = "seeking";
                }
                continue;
            }

            if (inString)
                continue;

            if (c == '{')
            {
                braceDepth++;
            }
            else if (c == '}')
            {
                braceDepth--;
                if (braceDepth == 0)
                    lastCompletePosition = i + 1;
            }
            else if (c == '[')
            {
                bracketDepth++;
            }
            else if (c == ']')
            {
                bracketDepth--;
                if (bracketDepth == 0 && braceDepth == 1)
                    lastCompletePosition = i + 1;
            }
            else if (c == ':')
            {
                currentState = "inValue";
            }
            else if (c == ',')
            {
                currentState = "seeking";
            }
            else if ((char.IsDigit(c) || c == '-') && currentState == "inValue" && bracketDepth == 0)
            {
                // Start of number
                var numStart = i;
                while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '.' || json[i] == '-' || json[i] == '+' || json[i] == 'e' || json[i] == 'E'))
                    i++;
                i--;
                
                if (braceDepth == 1)
                {
                    lastCompletePosition = i + 1;
                    currentState = "seeking";
                }
            }
            else if ((c == 't' || c == 'f' || c == 'n') && currentState == "inValue" && bracketDepth == 0)
            {
                // Could be true, false, or null
                var wordStart = i;
                while (i < json.Length && char.IsLetter(json[i]))
                    i++;
                i--;
                
                if (braceDepth == 1)
                {
                    lastCompletePosition = i + 1;
                    currentState = "seeking";
                }
            }
        }

        // Trim to last complete position if we found incomplete content
        if (lastCompletePosition > 0 && lastCompletePosition < json.Length)
        {
            return json.Substring(0, lastCompletePosition);
        }

        return json;
    }

    public string RepairJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json;

        // First pass: Remove trailing commas
        json = System.Text.RegularExpressions.Regex.Replace(json, @",\s*}", "}");
        json = System.Text.RegularExpressions.Regex.Replace(json, @",\s*]", "]");

        // Second pass: Handle incomplete/truncated JSON
        json = RepairIncompleteJson(json);

        return json;
    }

    private static string RepairIncompleteJson(string json)
    {
        var result = new System.Text.StringBuilder();
        var inString = false;
        var escapeNext = false;
        var openBraces = 0;
        var openBrackets = 0;
        var lastNonWhitespaceIndex = -1;

        // Track state for incomplete property detection
        var currentPropertyStarted = false;
        
        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];

            // Track escape sequences
            if (escapeNext)
            {
                result.Append(c);
                escapeNext = false;
                if (!char.IsWhiteSpace(c))
                    lastNonWhitespaceIndex = result.Length - 1;
                continue;
            }

            if (c == '\\' && inString)
            {
                result.Append(c);
                escapeNext = true;
                continue;
            }

            // Track strings
            if (c == '"')
            {
                inString = !inString;
                result.Append(c);
                lastNonWhitespaceIndex = result.Length - 1;
                continue;
            }

            // If we're in a string, just append
            if (inString)
            {
                result.Append(c);
                if (!char.IsWhiteSpace(c))
                    lastNonWhitespaceIndex = result.Length - 1;
                continue;
            }

            // Track braces and brackets
            if (c == '{')
            {
                openBraces++;
                result.Append(c);
                lastNonWhitespaceIndex = result.Length - 1;
            }
            else if (c == '}')
            {
                openBraces--;
                result.Append(c);
                lastNonWhitespaceIndex = result.Length - 1;
            }
            else if (c == '[')
            {
                openBrackets++;
                result.Append(c);
                lastNonWhitespaceIndex = result.Length - 1;
            }
            else if (c == ']')
            {
                openBrackets--;
                result.Append(c);
                lastNonWhitespaceIndex = result.Length - 1;
            }
            else if (c == ':')
            {
                result.Append(c);
                currentPropertyStarted = true;
                lastNonWhitespaceIndex = result.Length - 1;
            }
            else if (c == ',')
            {
                result.Append(c);
                currentPropertyStarted = false;
                lastNonWhitespaceIndex = result.Length - 1;
            }
            else
            {
                result.Append(c);
                if (!char.IsWhiteSpace(c))
                    lastNonWhitespaceIndex = result.Length - 1;
            }
        }

        // Close any unclosed strings
        if (inString)
        {
            result.Append('"');
            lastNonWhitespaceIndex = result.Length - 1;
        }

        // Remove incomplete trailing property if present
        // Look for patterns like: ..."propertyName or ..., "incomplete
        var resultStr = result.ToString();
        
        // Find last complete value position (before any incomplete trailing content)
        var cleanPoint = FindLastCompleteValue(resultStr);
        if (cleanPoint > 0 && cleanPoint < resultStr.Length)
        {
            resultStr = resultStr.Substring(0, cleanPoint);
        }

        // Close any unclosed brackets and braces
        while (openBrackets > 0)
        {
            resultStr += "]";
            openBrackets--;
        }

        while (openBraces > 0)
        {
            resultStr += "}";
            openBraces--;
        }

        return resultStr;
    }

    private static int FindLastCompleteValue(string json)
    {
        var inString = false;
        var escapeNext = false;
        var braceDepth = 0;
        var bracketDepth = 0;
        var lastCompleteValueEnd = -1;
        var afterColon = false;

        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];

            if (escapeNext)
            {
                escapeNext = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escapeNext = true;
                continue;
            }

            if (c == '"')
            {
                if (!inString)
                {
                    inString = true;
                }
                else
                {
                    inString = false;
                    // Completed a string value
                    if (afterColon || braceDepth == 1)
                    {
                        lastCompleteValueEnd = i + 1;
                        afterColon = false;
                    }
                }
                continue;
            }

            if (inString)
                continue;

            if (c == '{')
            {
                braceDepth++;
            }
            else if (c == '}')
            {
                braceDepth--;
                if (braceDepth >= 0)
                    lastCompleteValueEnd = i + 1;
            }
            else if (c == '[')
            {
                bracketDepth++;
            }
            else if (c == ']')
            {
                bracketDepth--;
                if (bracketDepth >= 0 && braceDepth > 0)
                    lastCompleteValueEnd = i + 1;
            }
            else if (c == ':')
            {
                afterColon = true;
            }
            else if (c == ',')
            {
                afterColon = false;
            }
            else if (char.IsDigit(c) || c == 't' || c == 'f' || c == 'n')
            {
                // Could be number, true, false, or null
                // Try to consume the complete value
                var valueStart = i;
                while (i < json.Length && (char.IsLetterOrDigit(json[i]) || json[i] == '.' || json[i] == '-' || json[i] == '+' || json[i] == 'e' || json[i] == 'E'))
                {
                    i++;
                }
                i--; // Back up one since loop will increment

                if (afterColon || braceDepth == 1)
                {
                    lastCompleteValueEnd = i + 1;
                    afterColon = false;
                }
            }
        }

        return lastCompleteValueEnd;
    }
}
