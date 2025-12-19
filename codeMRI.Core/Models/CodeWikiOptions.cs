namespace codeMRI.Core.Models;

public class CodeWikiOptions
{
    public int MaxDegreeOfParallelism { get; set; } = 5;
    
    /// <summary>
    /// Enable parent page revision after child documentation completes (Algorithm 1 from CodeWiki paper).
    /// When enabled, parent pages are refined with concrete details from child documentation.
    /// </summary>
    public bool EnableRevisionLoop { get; set; } = false;

    /// <summary>
    /// Only revise parent pages if there are at least this many child pages.
    /// Helps avoid unnecessary LLM calls for simple module structures.
    /// </summary>
    public int MinChildrenForRevision { get; set; } = 2;
}
