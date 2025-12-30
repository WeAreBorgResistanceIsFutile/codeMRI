using System.Text.Json;

namespace codeMRI.Core.Interfaces;

/// <summary>
///     Service for extracting and repairing JSON from potentially malformed LLM responses
/// </summary>
public interface IJsonRepairService
{
    /// <summary>
    ///     Extracts and deserializes JSON from a potentially malformed LLM response
    /// </summary>
    /// <typeparam name="T">The type to deserialize to</typeparam>
    /// <param name="response">The LLM response containing JSON</param>
    /// <param name="options">Optional JsonSerializerOptions</param>
    /// <returns>Deserialized object or null if extraction/parsing fails</returns>
    T? ExtractAndDeserialize<T>(string response, JsonSerializerOptions? options = null)
        where T : class;

    /// <summary>
    ///     Extracts valid JSON string from a potentially malformed response
    /// </summary>
    /// <param name="response">The response containing JSON</param>
    /// <returns>Extracted JSON string or null if extraction fails</returns>
    string? ExtractJsonString(string response);

    /// <summary>
    ///     Attempts to repair common JSON syntax errors
    /// </summary>
    /// <param name="json">Potentially malformed JSON</param>
    /// <returns>Repaired JSON string</returns>
    string RepairJson(string json);
}
