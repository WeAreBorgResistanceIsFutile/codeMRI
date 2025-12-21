using System.Text;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;

namespace codeMRI.Core.Services;

/// <summary>
///     Prompt templates for navigation structure generation
/// </summary>
public static class NavigationPromptTemplates
{
    /// <summary>
    ///     Generates a prompt for the LLM to create documentation-focused navigation structure
    /// </summary>
    public static string GenerateNavigationStructurePrompt(
        ModuleTree moduleTree,
        RepositoryInfo repositoryInfo)
    {
        // Serialize module tree for context
        var treeContext = SerializeModuleTree(moduleTree.Root, 0);

        return $$"""
                You are a technical documentation architect. Your task is to create a user-friendly documentation navigation structure.

                ## Repository Information
                - **Name**: {{repositoryInfo.Name}}
                - **Language**: {{repositoryInfo.Language}}
                - **Components**: {{repositoryInfo.ComponentCount}}
                - **Approximate Size**: {{repositoryInfo.LinesOfCode}} lines of code

                ## Code Structure (Hierarchical Decomposition)
                The following is the result of analyzing the codebase and identifying logical modules:

                {{treeContext}}

                ## Your Task
                Create a documentation table of contents (navigation) that helps users understand this project.
                
                **CRITICAL RULE: DO NOT create one section per module!**
                
                Instead, create 4-6 documentation-focused sections and assign multiple modules to each section based on their purpose.

                **WRONG Approach** (one section per module):
                ❌ Section: "CrossCutting" → contains CrossCutting module
                ❌ Section: "Repository" → contains Repository module  
                ❌ Section: "Src Main Java Tokenizer" → contains Tokenizer module
                
                **CORRECT Approach** (functional sections with multiple modules):
                ✅ Section: "Core Features" → contains [CrossCutting, Util, Helper] modules
                ✅ Section: "Data Access" → contains [Repository, Database, Cache] modules
                ✅ Section: "API Reference" → contains [Tokenizer, Parser, Analyzer] modules

                **Guidelines:**
                1. **User-Focused**: Organize by what users need to know, not how the code is structured
                2. **Plain Language**: Use descriptive names like "Getting Started", "Core Features", "API Reference"
                3. **Shallow Hierarchy**: Maximum 2-3 levels (e.g., "API Reference" → "Tokenization" → pages)
                4. **Logical Grouping**: Group related modules by functionality
                5. **Limited Sections**: Create 4-8 top-level sections, NOT one per module

                **Section Title Rules - NEVER use these as titles:**
                ❌ Module names: "CrossCutting", "Repository", "Infrastructure"
                ❌ Path names: "Src Main Java", "Test Java Net"
                ❌ Technical layers: "Presentation", "Domain", "Data"
                
                **Section Title Rules - ALWAYS use these patterns:**
                ✅ "Getting Started" / "Quick Start" / "Installation"
                ✅ "Core Features" / "Main Functionality"
                ✅ "Advanced Topics" / "Advanced Usage"
                ✅ "API Reference" / "Technical Reference"
                ✅ "Configuration" / "Setup"
                ✅ "Examples" / "Tutorials"

                **Navigation Patterns:**
                
                Simple library (4-5 sections):
                - Getting Started
                - Core Functionality
                - API Reference
                - Examples
                
                Complex application (6-8 sections):
                - Getting Started
                - Core Features
                - Advanced Topics
                - API Reference
                - Configuration
                - Operations Guide

                ## Output Format
                Return ONLY valid JSON matching this schema:

                {
                  "title": "Project Documentation",
                  "description": "Brief description",
                  "sections": [
                    {
                      "id": "section_getting_started",
                      "title": "Getting Started",
                      "moduleIds": ["mod1", "mod2", "mod5"],
                      "subSections": []
                    },
                    {
                      "id": "section_core",
                      "title": "Core Features",
                      "moduleIds": ["mod3", "mod4", "mod6"],
                      "subSections": []
                    }
                  ],
                  "moduleMapping": {
                    "mod1": "section_getting_started",
                    "mod2": "section_getting_started",
                    "mod3": "section_core",
                    "mod4": "section_core",
                    "mod5": "section_getting_started",
                    "mod6": "section_core"
                  }
                }

                **Critical Requirements:**
                - Create only 4-8 top-level sections (NOT one section per module!)
                - Each section should contain MULTIPLE modules (moduleIds array has multiple items)
                - Section titles must be documentation topics, NOT module names
                - **SubSections (if used) must ALSO have documentation-focused titles, NOT module names**
                - Prefer flat structure - avoid subsections unless absolutely necessary for organization
                - Every module ID must appear in moduleMapping
                - Section IDs must be unique and NOT match module IDs

                Return ONLY the JSON, no explanations or markdown code blocks.
                """;
    }

    private static string SerializeModuleTree(ModuleNode node, int depth)
    {
        var sb = new StringBuilder();
        var indent = new string(' ', depth * 2);

        sb.AppendLine($"{indent}- **Module**: {node.Name} (ID: {node.Id})");
        sb.AppendLine($"{indent}  - Components: {node.Components.Count}");
        sb.AppendLine($"{indent}  - Complexity: {node.ComplexityScore:F1}");
        sb.AppendLine($"{indent}  - IsLeaf: {node.IsLeaf}");

        if (node.Metadata.TryGetValue("ArchitecturalPattern", out var pattern))
            sb.AppendLine($"{indent}  - Pattern: {pattern}");

        if (node.Children.Any())
        {
            sb.AppendLine($"{indent}  - Children:");
            foreach (var child in node.Children)
                sb.Append(SerializeModuleTree(child, depth + 2));
        }

        return sb.ToString();
    }
}
