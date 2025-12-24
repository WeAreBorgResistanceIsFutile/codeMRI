using System.Text.Json;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services;

/// <summary>
///     Service that transforms code structure (ModuleTree) into documentation-optimized navigation (WikiStructure)
///     using LLM analysis
/// </summary>
public class NavigationStructureService : INavigationStructureService
{
    private readonly ILLMClient _llmClient;
    private readonly ILogger<NavigationStructureService> _logger;

    public NavigationStructureService(
        ILLMClient llmClient,
        ILogger<NavigationStructureService> logger)
    {
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<WikiStructure> GenerateDocumentationStructureAsync(
        ModuleTree moduleTree,
        RepositoryInfo repositoryInfo,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating documentation-focused navigation structure for {RepoName}", repositoryInfo.Name);

        try
        {
            // Generate prompt
            var prompt = NavigationPromptTemplates.GenerateNavigationStructurePrompt(moduleTree, repositoryInfo);

            // Call LLM
            var response = await _llmClient.ChatAsync(
                "You are a technical documentation architect specializing in creating user-friendly navigation structures.",
                prompt,
                new List<ChatMessage>(),
                null,  // Use default model
                cancellationToken);

            _logger.LogInformation("LLM Response (first 500 chars): {Response}", 
                response.Length > 500 ? response.Substring(0, 500) + "..." : response);

            // Parse JSON response
            var structure = ParseNavigationResponse(response, repositoryInfo);

            _logger.LogInformation("Successfully generated navigation with {SectionCount} top-level sections", 
                structure.Sections.Count);
                
            // Log section details
            foreach (var section in structure.Sections)
            {
                var subsectionInfo = section.SubSections.Count > 0 
                    ? $" with {section.SubSections.Count} subsections: [{string.Join(", ", section.SubSections.Select(s => s.Title))}]"
                    : "";
                _logger.LogInformation("  Section: '{Title}' (moduleIds: {ModuleCount}){SubsectionInfo}", 
                    section.Title, section.ModuleIds.Count, subsectionInfo);
            }

            return structure;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate documentation structure, creating fallback");
        }

        return new WikiStructure();
    }

    private WikiStructure ParseNavigationResponse(string jsonResponse, RepositoryInfo repositoryInfo)
    {
        // Extract JSON from response (handle text before/after JSON)
        var cleanJson = ExtractJson(jsonResponse);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };

        try
        {
            var dto = JsonSerializer.Deserialize<NavigationStructureDto>(cleanJson, options)
                      ?? throw new InvalidOperationException("Failed to deserialize navigation structure");

            // Convert DTO to WikiStructure
            var structure = new WikiStructure
            {
                Title = dto.Title ?? $"{repositoryInfo.Name} Documentation",
                Description = dto.Description ?? $"Documentation for {repositoryInfo.Name}",
                RepoPath = repositoryInfo.RepoPath,
                Sections = dto.Sections?.Select(ConvertSection).ToList() ?? new List<WikiSection>(),
                Pages = new List<WikiPage>(),
                ModuleToSectionMap = dto.ModuleMapping ?? new Dictionary<string, string>()
            };

            return structure;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON response. Response: {Response}", cleanJson);
            throw new InvalidOperationException($"Invalid JSON in LLM response: {ex.Message}", ex);
        }
    }

    private string ExtractJson(string response)
    {
        var trimmed = response.Trim();
        
        // Remove markdown code blocks
        if (trimmed.StartsWith("```json"))
            trimmed = trimmed.Substring(7);
        else if (trimmed.StartsWith("```"))
            trimmed = trimmed.Substring(3);
            
        if (trimmed.EndsWith("```"))
            trimmed = trimmed.Substring(0, trimmed.Length - 3);
            
        trimmed = trimmed.Trim();

        // Find JSON object boundaries
        var firstBrace = trimmed.IndexOf('{');
        if (firstBrace == -1)
            throw new InvalidOperationException("No JSON object found in response");

        // Find matching closing brace
        var braceCount = 0;
        var lastBrace = firstBrace;
        var inString = false;
        var escapeNext = false;

        for (int i = firstBrace; i < trimmed.Length; i++)
        {
            var c = trimmed[i];

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
                    braceCount++;
                else if (c == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        lastBrace = i;
                        break;
                    }
                }
            }
        }

        if (braceCount != 0)
            throw new InvalidOperationException("Unmatched braces in JSON");

        return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
    }

    private WikiSection ConvertSection(SectionDto dto)
    {
        return new WikiSection
        {
            Id = dto.Id ?? Guid.NewGuid().ToString(),
            Title = dto.Title ?? "Untitled Section",
            PageRefs = new List<string>(), // Populated during content generation
            SubSections = dto.SubSections?.Select(ConvertSection).ToList() ?? new List<WikiSection>(),
            ModuleIds = dto.ModuleIds ?? new List<string>() // Store module IDs for content linking
        };
    }

    private List<ModuleNode> GetAllModules(ModuleNode node)
    {
        var modules = new List<ModuleNode> { node };
        foreach (var child in node.Children)
            modules.AddRange(GetAllModules(child));
        return modules;
    }

    // DTOs for JSON parsing
    private class NavigationStructureDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public List<SectionDto>? Sections { get; set; }
        public Dictionary<string, string>? ModuleMapping { get; set; }
    }

    private class SectionDto
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public List<string>? ModuleIds { get; set; }
        public List<SectionDto>? SubSections { get; set; }
    }
}
