using System.Text;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;

namespace codeMRI.Core.Services;

public class DocumentationSynthesisService : IDocumentationSynthesisService
{
    private readonly ILLMClient _llmClient;

    public DocumentationSynthesisService(ILLMClient llmClient)
    {
        _llmClient = llmClient;
    }

    public async Task<WikiPage> SynthesizeParentPageAsync(ModuleNode module, List<WikiPage> childPages,
        string language = "English")
    {
        // If no children, perform a simple generation
        if (childPages == null || !childPages.Any())
        {
            var simplePrompt = $"Generate a brief overview for the module '{module.Name}'. It has no child modules detected.";
            if (!string.IsNullOrEmpty(module.Description))
            {
                simplePrompt += $"\nDescription: {module.Description}";
            }
            var simpleContent = await _llmClient.ChatAsync("You are a documentation assistant.", simplePrompt,
                new List<ChatMessage>());
            
            return new WikiPage
            {
                Id = Guid.NewGuid().ToString(),
                Title = module.Name,
                Content = simpleContent,
                RelevantFiles = new List<string>()
            };
        }

        // 1. Theme Analysis
        var summariesBuilder = new StringBuilder();
        foreach (var page in childPages)
        {
            summariesBuilder.AppendLine($"Module: {page.Title}");
            summariesBuilder.AppendLine($"Summary: {ExtractSummary(page.Content)}");
            summariesBuilder.AppendLine("---");
        }

        var themePrompt = $@"
Analyze the following child module summaries for the parent module '{module.Name}'.
Identify common architectural themes, design patterns, and cross-cutting concerns.

Child Modules:
{summariesBuilder}

Output a concise list of themes and patterns.";

        var themes = await _llmClient.ChatAsync("You are a software architect.", themePrompt, new List<ChatMessage>());

        // 2. Architectural Overview Synthesis
        var synthesisPrompt = $@"
Synthesize a comprehensive architectural overview for the parent module '{module.Name}' (Level {module.Level}).
Language: {language}

Module Description: {module.Description ?? "N/A"}

Identified Themes & Patterns:
{themes}

Child Modules:
{summariesBuilder}

Instructions:
1. Create a high-level overview.
2. Explain how the child modules collaborate to fulfill the parent module's responsibilities.
3. Highlight the identified themes and patterns.
4. Provide a usage guide or feature summary if applicable.
";

        var overviewContent = await _llmClient.ChatAsync("You are a technical documentation expert.", synthesisPrompt,
            new List<ChatMessage>());

        return new WikiPage
        {
            Id = Guid.NewGuid().ToString(),
            Title = module.Name,
            Content = overviewContent,
            RelevantFiles = new List<string>()
        };
    }

    private string ExtractSummary(string content)
    {
        if (string.IsNullOrEmpty(content)) return "";
        // Try to find the first paragraph or a summary section
        var idx = content.IndexOf("\n\n");
        if (idx > 0) return content.Substring(0, idx);
        
        // If no double newline, just take the first 200 chars
        return content.Length > 200 ? content.Substring(0, 200) + "..." : content;
    }
}