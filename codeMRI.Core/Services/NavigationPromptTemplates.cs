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

        return $$$"""
                You are a Principal Technical Documentation Architect analyzing a REAL codebase structure.
                
                **ACTUAL Project Data**:
                - Project: {{{repositoryInfo.Name}}} ({{{repositoryInfo.Language}}})
                - Complexity: {{{repositoryInfo.LinesOfCode}}} LOC / {{{repositoryInfo.ComponentCount}}} Modules
                
                **ACTUAL Module Tree** (use these exact IDs):
                {{{treeContext}}}
                
                ---
                
                ### Your Task:
                
                Analyze the ACTUAL module tree above and create a documentation navigation structure.
                
                **Phase 1: Extract Module IDs**
                From the source tree above, extract all module IDs (shown as "ID: xyz").
                
                **Phase 2: Semantic Grouping**
                Group modules by documentation theme using these patterns:
                
                | When module names contain... | Group into theme... |
                |------------------------------|---------------------|
                | `setup`, `install`, `init`, `config`, `env` | **Getting Started** |
                | `core`, `main`, `engine`, `logic`, `base` | **Core Architecture** |
                | `api`, `routes`, `endpoint`, `controller` | **API & Integration** |
                | `db`, `model`, `schema`, `store`, `repository` | **Data Persistence** |
                | `util`, `helper`, `common`, `shared`, `lib` | **Utilities & Shared Resources** |
                | `worker`, `job`, `task`, `service` | **Background Processing** |
                
                **Phase 3: Constraints**
                1. Each section should contain at least 3 modules (except "Getting Started")
                2. Use 4-8 top-level sections total
                3. All module IDs from the tree MUST appear in moduleMapping
                4. Use `subSections` only if a theme has 5+ modules
                
                **Phase 4: Generate JSON**
                
                Return ONLY the JSON structure below. No templates, no examples, no explanations.
                Use the ACTUAL module IDs from the tree above.
                
                {
                  "title": "Project Documentation Navigation",
                  "description": "Functional documentation structure for {{{repositoryInfo.Name}}}",
                  "sections": [
                    {
                      "id": "section_id",
                      "title": "Section Title",
                      "moduleIds": ["actual_module_id_from_tree", "another_actual_id"],
                      "subSections": []
                    }
                  ],
                  "moduleMapping": {
                    "actual_module_id_from_tree": "section_id",
                    "another_actual_id": "section_id"
                  }
                }
                
                START JSON NOW (no text before the opening brace):
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
