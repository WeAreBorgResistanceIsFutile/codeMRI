namespace codeMRI.Core.Interfaces;

/// <summary>
///     Service for extracting and repairing Mermaid diagrams from potentially malformed LLM responses
/// </summary>
public interface IMermaidRepairService
{
    /// <summary>
    ///     Extracts valid Mermaid diagram from a potentially malformed response
    /// </summary>
    /// <param name="response">The response containing Mermaid code</param>
    /// <returns>Extracted Mermaid code or null if extraction fails</returns>
    string? ExtractMermaid(string response);

    /// <summary>
    ///     Attempts to repair common Mermaid syntax errors
    /// </summary>
    /// <param name="mermaid">Potentially malformed Mermaid code</param>
    /// <returns>Repaired Mermaid code</returns>
    string RepairMermaid(string mermaid);
}
