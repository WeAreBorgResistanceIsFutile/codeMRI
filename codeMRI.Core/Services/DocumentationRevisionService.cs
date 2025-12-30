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
    private readonly IMarkdownRepairService _markdownRepairService;
    private readonly IMermaidRepairService _mermaidRepairService;

    public DocumentationRevisionService(ILLMServiceFacade llmFacade, ILogger<DocumentationRevisionService> logger,
        IOptions<CodeWikiOptions> options, IMarkdownRepairService markdownRepairService, IMermaidRepairService mermaidRepairService)
    {
        _llmFacade = llmFacade;
        _logger = logger;
        _options = options.Value;
        _markdownRepairService = markdownRepairService;
        _mermaidRepairService = mermaidRepairService;
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

        // Use Markdown repair service for initial cleanup and link conversion
        var cleaned = _markdownRepairService.ExtractMarkdown(content);
        cleaned = _markdownRepairService.RepairMarkdown(cleaned);

        // Use Mermaid repair service for diagram sanitization
        cleaned = _mermaidRepairService.RepairMermaid(cleaned);

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
}