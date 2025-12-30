namespace codeMRI.Core.Interfaces;

/// <summary>
///     Service for extracting and repairing Markdown content from potentially malformed LLM responses
/// </summary>
public interface IMarkdownRepairService
{
    /// <summary>
    ///     Extracts Markdown content from a potentially malformed response
    /// </summary>
    /// <param name="response">The response containing Markdown</param>
    /// <returns>Extracted Markdown content</returns>
    string ExtractMarkdown(string response);

    /// <summary>
    ///     Attempts to repair common Markdown syntax errors
    /// </summary>
    /// <param name="markdown">Potentially malformed Markdown</param>
    /// <returns>Repaired Markdown content</returns>
    string RepairMarkdown(string markdown);
}
