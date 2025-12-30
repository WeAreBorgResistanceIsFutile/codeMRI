using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services.MessageComposition;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace codeMRI.Core.Services;

/// <summary>
///     Implements parent documentation revision based on child module insights.
///     Following Algorithm 1 from the CodeWiki paper, this service refines parent
///     documentation after child modules have been fully documented.
/// </summary>
public class DocumentationRevisionService : IDocumentationRevisionService
{
    private readonly ILLMServiceFacade _llmFacade;
    private readonly ILogger<DocumentationRevisionService> _logger;
    private readonly CodeWikiOptions _options;

    public DocumentationRevisionService(ILLMServiceFacade llmFacade, ILogger<DocumentationRevisionService> logger,
        IOptions<CodeWikiOptions> options)
    {
        _llmFacade = llmFacade;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<WikiPage> ReviseParentDocumentationAsync(
        WikiPage parentPage,
        ModuleNode parentModule,
        List<WikiPage> childPages,
        string language = "English",
        CancellationToken cancellationToken = default)
    {
        if (childPages == null || childPages.Count == 0)
        {
            _logger.LogDebug("No child pages for revision of {ModuleName}, returning original", parentModule.Name);
            return parentPage;
        }

        _logger.LogInformation(
            "Revising parent page {ModuleName} with insights from {ChildCount} child pages",
            parentModule.Name, childPages.Count);

        var prompt = PromptTemplates.ReviseParentDocumentationPrompt(
            parentPage,
            parentModule,
            childPages,
            language);

        try
        {
            string revisedContent;
            var systemPrompt = "You are an expert technical writer specializing in documentation refinement.";

            // Use facade to execute - it will automatically handle chunking if content is large
            var llmResponse = await _llmFacade.ExecuteAsync(
                systemPrompt: systemPrompt,
                textToProcess: prompt,
                history: null,
                options: null,
                cancellationToken: cancellationToken);
            
            revisedContent = llmResponse.Content;

            // Create revised page preserving metadata
            var revisedPage = new WikiPage
            {
                Id = parentPage.Id,
                Title = parentPage.Title,
                Content = CleanRevisedContent(revisedContent, parentPage.Title),
                RelevantFiles = parentPage.RelevantFiles,
                Description = parentPage.Description,
                Metadata = parentPage.Metadata ?? new Dictionary<string, object>()
            };

            // Mark as revised
            revisedPage.Metadata["IsRevised"] = true;
            revisedPage.Metadata["RevisionChildCount"] = childPages.Count;

            _logger.LogInformation(
                "Successfully revised parent page {ModuleName} (original: {OriginalLen} chars, revised: {RevisedLen} chars)",
                parentModule.Name, parentPage.Content?.Length ?? 0, revisedPage.Content.Length);

            return revisedPage;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to revise parent page {ModuleName}, returning original", parentModule.Name);
            return parentPage;
        }
    }

    /// <summary>
    ///     Cleans the revised content to ensure consistent formatting.
    ///     Strips LLM preamble text and converts [[WikiLink]] syntax to markdown links.
    /// </summary>
    private string CleanRevisedContent(string content, string title)
    {
        if (string.IsNullOrWhiteSpace(content))
            return $"# {title}\n\n*Documentation pending.*";

        var cleaned = content.Trim();

        // Strip any LLM preamble text before the actual markdown content
        // LLMs often add conversational text like "Here's the refined documentation..."
        // We want to remove everything before the first markdown title or code fence
        cleaned = StripLLMPreamble(cleaned);

        // Remove markdown code fences if the LLM wrapped the entire output
        if (cleaned.StartsWith("```markdown")) cleaned = cleaned.Substring("```markdown".Length).TrimStart('\n', '\r');
        if (cleaned.EndsWith("```")) cleaned = cleaned.Substring(0, cleaned.Length - 3).TrimEnd();

        // Convert [[WikiLink]] syntax to proper markdown links
        cleaned = ConvertWikiLinksToMarkdown(cleaned);

        // Sanitize Mermaid diagrams to ensure labels are properly quoted
        cleaned = SanitizeMermaidDiagrams(cleaned);

        // Ensure it starts with the title
        if (!cleaned.StartsWith($"# {title}"))
        {
            if (cleaned.StartsWith("# "))
            {
                var firstNewline = cleaned.IndexOf('\n');
                if (firstNewline > 0) cleaned = cleaned.Substring(firstNewline).TrimStart('\n', '\r');
            }

            cleaned = $"# {title}\n\n{cleaned}";
        }

        return cleaned;
    }

    /// <summary>
    ///     Sanitizes Mermaid diagrams by quoting labels that contain problematic characters.
    /// </summary>
    private string SanitizeMermaidDiagrams(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;

        var lines = content.Split('\n');
        var result = new System.Text.StringBuilder();
        var isInMermaidBlock = false;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            if (trimmedLine.StartsWith("```mermaid"))
            {
                isInMermaidBlock = true;
                result.AppendLine(line);
                continue;
            }

            if (isInMermaidBlock && trimmedLine.StartsWith("```"))
            {
                isInMermaidBlock = false;
                result.AppendLine(line);
                continue;
            }

            if (isInMermaidBlock)
            {
                // Find and quote node labels: ID[Label], ID(Label), ((Label)), etc.
                // This regex looks for:
                // 1. A node identifier (optional if following an arrow)
                // 2. An opening bracket: [, (, ((, {, >
                // 3. The label content (not starting with a quote)
                // 4. A closing bracket: ], ), )), }, ]
                var nodeLabelPattern = @"([\[\(\{>]+)([^"" ][^\]\)\}]+?)([\]\)\}]+)";
                var updatedLine = System.Text.RegularExpressions.Regex.Replace(line, nodeLabelPattern, match =>
                {
                    var openBracket = match.Groups[1].Value;
                    var label = match.Groups[2].Value.Trim();
                    var closeBracket = match.Groups[3].Value;

                    // If label is already quoted or is very simple (only alphanumeric), we could leave it
                    // but quoting it is always safer for Mermaid.
                    
                    // Escape double quotes
                    var escapedLabel = label.Replace("\"", "\\\"");
                    return $"{openBracket}\"{escapedLabel}\"{closeBracket}";
                });

                // Also handle labels in arrows: A -->|Label| B
                var arrowPattern = @"(\|)([^"" ][^|]+?)(\|)";
                updatedLine = System.Text.RegularExpressions.Regex.Replace(updatedLine, arrowPattern, match =>
                {
                    var open = match.Groups[1].Value;
                    var label = match.Groups[2].Value.Trim();
                    var close = match.Groups[3].Value;

                    var escapedLabel = label.Replace("\"", "\\\"");
                    return $"{open}\"{escapedLabel}\"{close}";
                });

                result.AppendLine(updatedLine);
            }
            else
            {
                result.AppendLine(line);
            }
        }

        return result.ToString().TrimEnd();
    }

    /// <summary>
    ///     Strips any LLM preamble text that appears before the actual markdown content.
    ///     Removes everything before the first # title or ```markdown code fence.
    /// </summary>
    private string StripLLMPreamble(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;

        var lines = content.Split('\n');
        var startIndex = -1;

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmedLine = lines[i].TrimStart();
            
            // Found the start of actual markdown content
            if (trimmedLine.StartsWith("# ") || trimmedLine.StartsWith("```markdown"))
            {
                startIndex = i;
                break;
            }
        }

        // If we found a markdown start, remove everything before it
        if (startIndex > 0)
        {
            return string.Join('\n', lines.Skip(startIndex));
        }

        // No clear markdown start found, return as-is
        return content;
    }

    /// <summary>
    ///     Converts [[WikiLink]] syntax to proper markdown links.
    ///     [[PageName]] becomes [PageName](#PageName) for same-document anchors.
    /// </summary>
    private string ConvertWikiLinksToMarkdown(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;

        // Pattern to match [[WikiLink]] or [[Display Text|PageName]]
        var wikiLinkPattern = @"\[\[([^\]|]+)(?:\|([^\]]+))?\]\]";
        
        return System.Text.RegularExpressions.Regex.Replace(content, wikiLinkPattern, match =>
        {
            var linkTarget = match.Groups[1].Value.Trim();
            var displayText = match.Groups[2].Success ? match.Groups[2].Value.Trim() : linkTarget;
            
            // Convert to markdown link with anchor
            // For wiki-style links, we'll use lowercase-dash format for anchors
            var anchor = linkTarget.ToLower().Replace(' ', '-');
            return $"[{displayText}](#{anchor})";
        });
    }
}