using System;
using System.Collections.Generic;
using System.Text.Json;

namespace codeMRI.Frontend.Services
{
    public static class BenchmarkFormatter
    {
        public static string GetDisplayName(string configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration))
                return string.Empty;

            try
            {
                // Try strictly parsing as JSON object
                if (!configuration.TrimStart().StartsWith("{"))
                    return configuration;

                using var doc = JsonDocument.Parse(configuration);
                var root = doc.RootElement;

                if (root.TryGetProperty("DocumentationModel", out var docModel))
                {
                    return docModel.GetString() ?? "Unknown Model";
                }
                
                return "Custom Configuration";
            }
            catch (JsonException)
            {
                // Not valid JSON, return original string
                return configuration;
            }
        }

        public static Dictionary<string, object> ParseConfiguration(string configuration)
        {
            var result = new Dictionary<string, object>();
            
            if (string.IsNullOrWhiteSpace(configuration))
                return result;

            try
            {
                if (!configuration.TrimStart().StartsWith("{"))
                    return result;

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var dictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(configuration, options);
                
                if (dictionary != null)
                {
                    // Recursively handle JsonElement if needed, but for simple display:
                    foreach (var kvp in dictionary)
                    {
                        if (kvp.Value is JsonElement element)
                        {
                            result[kvp.Key] = element.ToString();
                        }
                        else
                        {
                            result[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            catch (JsonException)
            {
                 // Ignore parsing errors, return empty
            }

            return result;
        }
    }
}
