using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Shared.Models;
using ModuleTree = codeMRI.Core.Models.ModuleTree;
using ModuleNode = codeMRI.Core.Models.ModuleNode;

namespace codeMRI.Core.Services;

public class DocumentationGenerationPipeline : IDocumentationGenerationPipeline
{
    private readonly IComponentIdentificationService _componentService;
    private readonly IHierarchicalDecompositionService _decompositionService;
    private readonly ILogger<DocumentationGenerationPipeline> _logger;

    public DocumentationGenerationPipeline(
        IComponentIdentificationService componentService,
        IHierarchicalDecompositionService decompositionService,
        ILogger<DocumentationGenerationPipeline> logger)
    {
        _componentService = componentService;
        _decompositionService = decompositionService;
        _logger = logger;
    }

    public async Task<WikiStructure> GenerateDocumentationAsync(string repositoryPath, DocumentationOptions options)
    {
        _logger.LogInformation("Starting documentation generation for repository: {RepoPath}", repositoryPath);

        // Perform hierarchical decomposition
        var moduleTree = await _decompositionService.DecomposeHierarchicallyAsync(repositoryPath);
        
        // Analyze repository structure
        var structure = await _componentService.AnalyzeRepositoryAsync(repositoryPath);
        
        // Identify components
        var components = await _componentService.IdentifyComponentsAsync(repositoryPath);

        // Generate wiki structure with hierarchical sections
        var wikiStructure = new WikiStructure
        {
            Title = $"{structure.Name} Documentation",
            Description = $"Automated documentation for {structure.Name} ({structure.Language})"
        };

        // Add architecture section with hierarchical modules
        if (options.IncludeArchitecture)
        {
            wikiStructure.Sections.Add(new WikiSection
            {
                Id = "architecture",
                Title = "Architecture Overview",
                PageRefs = GenerateHierarchicalPageRefs(moduleTree.Root)
            });
        }

        // Add component documentation section
        wikiStructure.Sections.Add(new WikiSection
        {
            Id = "components",
            Title = "Component Details",
            PageRefs = components.Select(c => c.Id).ToList()
        });

        _logger.LogInformation("Documentation generation completed for repository: {RepoPath}", repositoryPath);
        return wikiStructure;
    }

    /// <summary>
    /// Generates hierarchical page references from the module tree
    /// </summary>
    private List<string> GenerateHierarchicalPageRefs(ModuleNode node)
    {
        var pageRefs = new List<string>();
        
        // Add module overview page
        if (node.Level > 0) // Skip root
        {
            pageRefs.Add($"module-{node.Id}-overview");
        }

        // Add child module pages
        foreach (var child in node.Children)
        {
            pageRefs.AddRange(GenerateHierarchicalPageRefs(child));
        }

        return pageRefs;
    }

    public Task<WikiPage> GenerateComponentDocumentationAsync(CodeComponent component, RepositoryStructure context)
    {
        _logger.LogInformation("Generating documentation for component: {ComponentId}", component.Id);

        var content = GenerateComponentContent(component, context);

        return Task.FromResult(new WikiPage
        {
            Id = component.Id,
            Title = component.Name,
            Description = $"Documentation for {component.Name} ({component.Type})",
            Content = content,
            RelevantFiles = new List<string> { component.FilePath }
        });
    }

    public async Task<List<WikiPage>> GenerateOverviewPagesAsync(RepositoryStructure structure, List<CodeComponent> components)
    {
        _logger.LogInformation("Generating overview pages for repository: {RepoName}", structure.Name);

        var pages = new List<WikiPage>();

        // Get module tree for architecture overview
        var moduleTree = await _decompositionService.DecomposeHierarchicallyAsync(structure.Path);

        // Architecture overview
        pages.Add(new WikiPage
        {
            Id = "architecture-overview",
            Title = "Architecture Overview",
            Description = "High-level architecture overview of the repository",
            Content = GenerateArchitectureContent(structure, components, moduleTree)
        });

        // Components overview
        pages.Add(new WikiPage
        {
            Id = "components-overview",
            Title = "Components Overview",
            Description = "Overview of all identified components in the repository",
            Content = GenerateComponentsOverviewContent(components)
        });

        return pages;
    }

    private string GenerateComponentContent(CodeComponent component, RepositoryStructure context)
    {
        var content = $@"# {component.Name}

**Type:** {component.Type}  
**Language:** {component.Language}  
**File:** `{component.FilePath}`  
**Complexity:** {component.ComplexityScore}/10

## Description
{component.Description}

## Properties
{string.Join("\n", component.Properties.Select(p => $"- `{p}`"))}

## Methods
{string.Join("\n", component.Methods.Select(m => $"- `{m}()`"))}

## Dependencies
{string.Join("\n", component.Dependencies.Select(d => $"- `{d}`"))}

## Complexity Analysis
- **Line Count:** {component.LineCount}
- **Complexity Score:** {component.ComplexityScore}/10
{(component.ComplexityScore > 7 ? "- **Note:** High complexity detected. Consider refactoring." : "")}

---

*This documentation was automatically generated by codeMRI.*";

        return content;
    }

    private string GenerateArchitectureContent(RepositoryStructure structure, List<CodeComponent> components, ModuleTree moduleTree)
    {
        var componentStats = components.GroupBy(c => c.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        var content = $@"# Architecture Overview

## Repository Information
- **Name:** {structure.Name}
- **Language:** {structure.Language}
- **Total Files:** {structure.Files.Count}
- **Total Components:** {components.Count}
- **Total Modules:** {moduleTree.Nodes.Count}

## Hierarchical Structure
{GenerateModuleTreeMarkdown(moduleTree.Root, 2)}

## Component Distribution
{string.Join("\n", componentStats.Select(kvp => $"- **{kvp.Key}s:** {kvp.Value}"))}

## File Extensions Distribution
{string.Join("\n", structure.FileExtensions.Select(kvp => $"- `{kvp.Key}`: {kvp.Value} files"))}

## Architecture Notes
This repository follows a {(structure.Language == "C#" ? ".NET" : structure.Language)} architecture pattern.
The codebase is {(components.Count > 20 ? "large" : components.Count > 10 ? "medium-sized" : "small")} with {components.Count} identified components.

---

*This documentation was automatically generated by codeMRI.*";

        return content;
    }

    /// <summary>
    /// Generates markdown representation of the module tree
    /// </summary>
    private string GenerateModuleTreeMarkdown(ModuleNode node, int indentLevel)
    {
        var indent = new string(' ', indentLevel * 2);
        var markdown = $"{indent}- **{node.Name}** (Components: {node.Components.Count}, Tokens: {node.EstimatedTokens:F0})";

        foreach (var child in node.Children)
        {
            markdown += $"\n{GenerateModuleTreeMarkdown(child, indentLevel + 1)}";
        }

        return markdown;
    }

    private string GenerateComponentsOverviewContent(List<CodeComponent> components)
    {
        var sortedComponents = components.OrderByDescending(c => c.ComplexityScore).ToList();

        var content = $@"# Components Overview

## Summary
Total components: {components.Count}

## High Complexity Components (Score > 7)
{string.Join("\n", sortedComponents.Where(c => c.ComplexityScore > 7).Select(c => $"- **{c.Name}** ({c.Type}) - Score: {c.ComplexityScore}/10"))}

## All Components
| Name | Type | Language | Complexity | File |
|------|------|----------|------------|------|
{string.Join("\n", sortedComponents.Select(c => $"| {c.Name} | {c.Type} | {c.Language} | {c.ComplexityScore}/10 | `{Path.GetFileName(c.FilePath)}` |"))}

## Component Types Distribution
{string.Join("\n", components.GroupBy(c => c.Type).Select(g => $"- **{g.Key}s:** {g.Count()}"))}

---

*This documentation was automatically generated by codeMRI.*";

        return content;
    }
}
