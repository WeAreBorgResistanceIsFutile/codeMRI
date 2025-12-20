using System.Text.RegularExpressions;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services;

/// <summary>
///     Implements hierarchical summarization with entity anchoring.
/// </summary>
public class HierarchicalSummaryService : IHierarchicalSummaryService
{
    // Regex patterns for entity extraction
    private static readonly Regex ClassNamePattern = new(
        @"`([A-Z][a-zA-Z0-9]*(?:Service|Repository|Controller|Factory|Handler|Manager|Provider|Client|Builder|Validator|Processor|Analyzer|Generator|Agent))`",
        RegexOptions.Compiled);

    private static readonly Regex FunctionNamePattern = new(
        @"`([A-Z][a-zA-Z0-9]*(?:Async)?)\(`",
        RegexOptions.Compiled);

    private static readonly Regex PatternNamePattern = new(
        @"\b(Repository|Factory|Singleton|Observer|Strategy|Command|CQRS|MVC|MVP|MVVM|Microservice|Event[- ]?Driven|Pub[- ]?Sub|Queue|Pipeline)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DependencyPattern = new(
        @"\b(PostgreSQL|MySQL|MongoDB|Redis|RabbitMQ|Kafka|Elasticsearch|Stripe|Twilio|AWS|Azure|GCP|Docker|Kubernetes)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ILLMClient _llmClient;
    private readonly ILogger<HierarchicalSummaryService> _logger;

    public HierarchicalSummaryService(ILLMClient llmClient, ILogger<HierarchicalSummaryService> logger)
    {
        _llmClient = llmClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public ExtractedEntities ExtractKeyEntities(WikiPage page)
    {
        if (page == null || string.IsNullOrWhiteSpace(page.Content))
            return new ExtractedEntities();

        return ExtractEntitiesFromContent(page.Content);
    }

    /// <inheritdoc />
    public ExtractedEntities ExtractKeyEntities(List<WikiPage> pages)
    {
        if (pages == null || !pages.Any())
            return new ExtractedEntities();

        var combined = new ExtractedEntities();

        foreach (var page in pages)
        {
            var entities = ExtractKeyEntities(page);
            combined.ClassNames.AddRange(entities.ClassNames);
            combined.FunctionNames.AddRange(entities.FunctionNames);
            combined.PatternNames.AddRange(entities.PatternNames);
            combined.DependencyNames.AddRange(entities.DependencyNames);
        }

        // Deduplicate
        combined.ClassNames = combined.ClassNames.Distinct().Take(20).ToList();
        combined.FunctionNames = combined.FunctionNames.Distinct().Take(20).ToList();
        combined.PatternNames = combined.PatternNames.Distinct().Take(10).ToList();
        combined.DependencyNames = combined.DependencyNames.Distinct().Take(10).ToList();

        _logger.LogDebug(
            "Extracted {ClassCount} classes, {FuncCount} functions, {PatternCount} patterns, {DepCount} dependencies from {PageCount} pages",
            combined.ClassNames.Count, combined.FunctionNames.Count,
            combined.PatternNames.Count, combined.DependencyNames.Count, pages.Count);

        return combined;
    }

    /// <inheritdoc />
    public async Task<ModuleSummary> SummarizeModuleAsync(
        WikiPage page,
        int maxWords = 200,
        CancellationToken cancellationToken = default)
    {
        if (page == null || string.IsNullOrWhiteSpace(page.Content))
            return new ModuleSummary
            {
                ModuleName = page?.Title ?? "Unknown",
                CorePurpose = "No content available"
            };

        var entities = ExtractKeyEntities(page);

        var prompt = PromptTemplates.SummarizeModulePrompt(page, entities, maxWords);

        try
        {
            var response = await _llmClient.ChatAsync(
                "You are a technical documentation summarizer. Be extremely concise.",
                prompt,
                new List<ChatMessage>(),
                null,
                cancellationToken);

            return ParseSummaryResponse(response, page, entities);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to summarize module {ModuleName}", page.Title);

            // Fallback: Create summary from first paragraph
            return new ModuleSummary
            {
                ModuleId = page.Id,
                ModuleName = page.Title,
                CorePurpose = ExtractFirstParagraph(page.Content),
                KeyFunctions = entities.FunctionNames.Take(5).ToList(),
                Dependencies = entities.DependencyNames.ToList(),
                OriginalCharCount = page.Content.Length
            };
        }
    }

    private ExtractedEntities ExtractEntitiesFromContent(string content)
    {
        var entities = new ExtractedEntities();

        // Extract class names
        foreach (Match match in ClassNamePattern.Matches(content))
        {
            var name = match.Groups[1].Value;
            if (!entities.ClassNames.Contains(name) && name.Length > 3)
                entities.ClassNames.Add(name);
        }

        // Extract function names
        foreach (Match match in FunctionNamePattern.Matches(content))
        {
            var name = match.Groups[1].Value;
            if (!entities.FunctionNames.Contains(name) && name.Length > 3)
                entities.FunctionNames.Add(name);
        }

        // Extract pattern names
        foreach (Match match in PatternNamePattern.Matches(content))
        {
            var name = match.Groups[1].Value;
            if (!entities.PatternNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                entities.PatternNames.Add(name);
        }

        // Extract dependency names
        foreach (Match match in DependencyPattern.Matches(content))
        {
            var name = match.Groups[1].Value;
            if (!entities.DependencyNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                entities.DependencyNames.Add(name);
        }

        return entities;
    }

    private ModuleSummary ParseSummaryResponse(string response, WikiPage page, ExtractedEntities entities)
    {
        // Simple parsing - the LLM should return structured text
        // Extract first paragraph as core purpose
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var corePurpose = lines.FirstOrDefault()?.Trim() ?? "Summary unavailable";

        return new ModuleSummary
        {
            ModuleId = page.Id,
            ModuleName = page.Title,
            CorePurpose = corePurpose.Length > 300 ? corePurpose.Substring(0, 297) + "..." : corePurpose,
            KeyFunctions = entities.FunctionNames.Take(5).ToList(),
            Dependencies = entities.DependencyNames.ToList(),
            ArchitecturalPattern = entities.PatternNames.FirstOrDefault() ?? "Not identified",
            OriginalCharCount = page.Content.Length
        };
    }

    private static string ExtractFirstParagraph(string content)
    {
        if (string.IsNullOrEmpty(content)) return "";

        var lines = content.Split('\n');
        var paragraphLines = new List<string>();
        var started = false;

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith('#')) continue;
            if (string.IsNullOrWhiteSpace(line))
            {
                if (started) break;
                continue;
            }

            started = true;
            paragraphLines.Add(line.Trim());
        }

        var result = string.Join(" ", paragraphLines);
        return result.Length > 200 ? result.Substring(0, 197) + "..." : result;
    }
}