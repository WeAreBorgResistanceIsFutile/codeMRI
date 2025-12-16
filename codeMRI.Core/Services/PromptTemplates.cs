using System.Text.Json;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;

namespace codeMRI.Core.Services;


public static class PromptTemplates
{
    #region Enhanced Page Generation Prompt

    /// <summary>
    /// Generates a sophisticated prompt for wiki page generation that includes module context,
    /// quality metrics, dependencies, and related wiki pages for cross-referencing.
    /// </summary>
    public static string EnhancedPagePrompt(
        ModuleNode module,
        List<WikiPage>? relatedPages,
        ModulePageContext context,
        string language)
    {
        var relatedSummaries = relatedPages?
            .Select(p => $"- **{p.Title}**: {ExtractFirstParagraph(p.Content)}")
            .Take(5)
            .ToList() ?? new List<string>();

        var dependenciesList = context.Dependencies?
            .Select(d => $"- {d}")
            .Take(10)
            .ToList() ?? new List<string>();

        var dependentsList = context.Dependents?
            .Select(d => $"- {d}")
            .Take(10)
            .ToList() ?? new List<string>();

        return $"""
            You are an expert technical writer documenting the "{module.Name}" module.
            
            ## Module Context
            - **Architectural Pattern**: {module.Metadata.GetValueOrDefault("ArchitecturalPattern", "Not identified")}
            - **Level in Hierarchy**: {module.Level}
            - **Component Count**: {module.Components.Count}
            - **Is Entry Point Module**: {module.Metadata.ContainsKey("HasEntryPoint")}
            
            ## Quality Metrics (Guide your emphasis)
            - **Cohesion**: {module.QualityMetrics.Cohesion:F2} (higher is better, aim for > 0.7)
            - **Coupling**: {module.QualityMetrics.Coupling:F2} (lower is better, aim for < 0.3)
            - **Maintainability Index**: {module.QualityMetrics.MaintainabilityIndex:F1}/100
            - **Complexity Score**: {module.ComplexityScore:F1}
            
            If cohesion is low, explain how components relate despite apparent disconnection.
            If coupling is high, document the integration points carefully.
            If complexity is high, provide clear explanations and diagrams.
            
            ## Dependencies (What this module uses)
            {(dependenciesList.Any() ? string.Join("\n", dependenciesList) : "None identified")}
            
            ## Dependents (What uses this module)
            {(dependentsList.Any() ? string.Join("\n", dependentsList) : "None identified")}
            
            ## Related Documentation (For cross-references)
            {(relatedSummaries.Any() ? string.Join("\n", relatedSummaries) : "No related pages available yet")}
            
            ## Instructions
            1. Write a comprehensive wiki page for "{module.Name}"
            2. Reference the related documentation where appropriate using [[WikiLink]] syntax
            3. Explain the module's role in the larger architecture
            4. Document PUBLIC interfaces and key abstractions
            5. Include Mermaid diagrams showing component relationships (use graph TD for vertical layout)
            6. Add usage examples where appropriate
            7. Include a summary table of key classes/functions
            
            STRICT RULES:
            - Only document what exists in the SOURCE FILES CONTENT below
            - Do NOT invent classes, methods, or features
            - Do NOT add conversational filler (e.g., "Here is the page...")
            - Use {language} for all content
            
            Output ONLY the markdown content starting with # {module.Name}
            
            At the VERY END of the page, include a <details> block listing ALL source files used.
            """;
    }

    #endregion

    #region Parent Page Synthesis Prompt

    /// <summary>
    /// Generates a prompt for synthesizing parent/overview pages from child pages.
    /// </summary>
    public static string ParentPageSynthesisPrompt(
        ModuleNode parentModule,
        List<WikiPage> childPages,
        int crossModuleDependencies,
        string language)
    {
        var childSummaries = childPages
            .Select(p => new
            {
                Title = p.Title,
                Summary = ExtractFirstParagraph(p.Content),
                FileCount = p.RelevantFiles?.Count ?? 0
            });

        var childSummariesJson = JsonSerializer.Serialize(childSummaries, 
            new JsonSerializerOptions { WriteIndented = true });

        return $"""
            Synthesize an architectural overview page for "{parentModule.Name}" (Level {parentModule.Level}).
            
            ## Child Modules
            <children>
            {childSummariesJson}
            </children>
            
            ## Architecture Insights
            - **Detected Pattern**: {parentModule.Metadata.GetValueOrDefault("ArchitecturalPattern", "Mixed")}
            - **Cross-Module Dependencies**: {crossModuleDependencies} connections between child modules
            - **Total Complexity**: {parentModule.ComplexityScore:F1}
            - **Cohesion**: {parentModule.QualityMetrics.Cohesion:F2}
            - **Coupling**: {parentModule.QualityMetrics.Coupling:F2}
            
            ## Your Task
            1. Create a HIGH-LEVEL overview that explains how child modules collaborate
            2. Do NOT repeat details from child pages - reference them instead using [[PageTitle]] syntax
            3. Include an architecture diagram showing child module relationships (Mermaid graph TD)
            4. Explain the design decisions and patterns employed
            5. Document any cross-cutting concerns (logging, error handling, etc.)
            6. Provide a "Getting Started" section for new developers
            
            STRICT RULES:
            - Base content ONLY on the provided child module summaries
            - Do NOT invent components or features not mentioned in child modules
            - Do NOT add conversational filler
            - Use {language} for all content
            
            Output ONLY the markdown content starting with # {parentModule.Name}
            """;
    }

    #endregion

    #region User/DevOps Documentation Prompts

    /// <summary>
    /// Generates a user-focused guide prompt for a module.
    /// Focuses on capabilities, configuration, and operations rather than code structure.
    /// </summary>
    public static string UserGuidePagePrompt(
        ModuleNode module,
        ModulePageContext context,
        Dictionary<string, string> sourceContent,
        AudienceType audience,
        string language)
    {
        return $"""
            You are a Technical Writer creating documentation for {audience} audience.
            
            ## Context
            - Module Name: {module.Name}
            - System Role: part of {module.Level} level components
            
            ## Instructions
            Write a user-friendly guide covering the following aspects based ONLY on the source code provided:
            1. **Overview**: What is this component and what problem does it solve? (No code jargon)
            2. **Key Capabilities**: specific features available to the user/admin.
            3. **Configuration**: Look for environment variables, config files, or settings classes. specific flags.
            4. **Operational Requirements**: External dependencies (DBs, APIs) found in connection strings or clients.
            5. **Troubleshooting**: Common error messages or failure scenarios visible in exceptions/logging.

            STRICT RULES:
            - Tone: Professional, concise, actionable.
            - NO class diagrams.
            - NO code metrics (cohesion/coupling).
            - NO architectural pattern discussions unless relevant to deployment.
            - Focus on "How to use/deploy/configure" vs "How it works internally".
            - Use {language} language.

            Output ONLY the markdown content starting with # {module.Name}
            """;
    }

    /// <summary>
    /// Generates a high-level system overview prompt for User/DevOps audience.
    /// </summary>
    public static string UserGuideSynthesisPrompt(
        ModuleNode parentModule,
        List<WikiPage> childPages,
        AudienceType audience,
        string language)
    {
        var childSummaries = childPages
             .Select(p => new
             {
                 Title = p.Title,
                 Summary = ExtractFirstParagraph(p.Content)
             });

        var childSummariesJson = JsonSerializer.Serialize(childSummaries,
            new JsonSerializerOptions { WriteIndented = true });

        return $"""
            Synthesize a System Overview for "{parentModule.Name}" for a {audience} audience.

            ## Component Summaries
            {childSummariesJson}

            ## Instructions
            1. **System Overview**: What is the complete system? What value does it deliver?
            2. **Deployment Architecture**: How do these components fit together in a deployment?
            3. **Integration Points**: External APIs or systems involved.
            4. **Getting Started**: Steps to deploy, configure, or run the system.

            STRICT RULES:
            - Focus on value proposition and operations.
            - Ignore internal code structure/refactoring details.
            - Use {language} language.

            Output ONLY the markdown content starting with # {parentModule.Name}
            """;
    }

    #endregion

    #region Enhanced RAG System Prompt

    /// <summary>
    /// Generates a context-aware system prompt for RAG-based code assistance.
    /// </summary>
    public static string EnhancedRAGSystemPrompt(
        RepositoryInfo repoInfo,
        WikiStructure? wikiStructure,
        string language)
    {
        var sectionTitles = wikiStructure?.Sections?
            .Select(s => s.Title)
            .Take(10)
            .ToList() ?? new List<string>();

        return $"""
            You are a code assistant for the "{repoInfo.Name}" repository.
            
            ## Repository Context
            - **Primary Language**: {repoInfo.Language}
            - **Size**: {repoInfo.LinesOfCode:N0} lines across {repoInfo.ComponentCount} components
            {(sectionTitles.Any() ? $"- **Documentation Sections**: {string.Join(", ", sectionTitles)}" : "")}
            
            ## Your Behavior
            1. Answer questions based ONLY on the provided context
            2. If information is not in the context, say "I don't have information about that in the current context"
            3. Reference specific wiki pages when relevant: "See [[Page Title]] for more details"
            4. Prioritize accuracy over completeness - better to admit uncertainty than hallucinate
            5. When explaining code, cite specific files and line numbers when available
            
            ## Language
            Respond in {language}.
            
            ## Formatting
            - Use markdown for structure
            - Include code examples when showing implementation details
            - Use Mermaid diagrams sparingly for complex relationships
            - Link to relevant documentation sections using [[WikiLink]] syntax
            """;
    }

    /// <summary>
    /// Legacy RAG system prompt for backward compatibility.
    /// </summary>
    public static string RAGSystemPrompt(string language)
    {
        return $"""
            You are a code assistant which answers user questions on a Github Repo.
            You must prioritize FALSE-NEGATIVE (omitting info) over FALSE-POSITIVE (inventing info). 
            If a component is missing, do not assume it exists.
            You will receive user query, relevant context, and past conversation history.

            LANGUAGE DETECTION AND RESPONSE:
            - Respond in {language}.

            FORMAT YOUR RESPONSE USING MARKDOWN:
            - Use proper markdown syntax.
            - No markdown fences around the entire response.
            """;
    }

    #endregion

    #region Diagram Context Prompt

    /// <summary>
    /// Generates a prompt for creating contextual Mermaid diagrams based on module structure.
    /// </summary>
    public static string DiagramContextPrompt(
        string diagramType,
        ModuleNode module,
        List<DiagramNodeInfo> nodes,
        string language)
    {
        var nodesJson = JsonSerializer.Serialize(nodes.Take(30), 
            new JsonSerializerOptions { WriteIndented = true });

        return $"""
            Generate a {diagramType} Mermaid diagram for the "{module.Name}" module.
            
            ## Available Components
            <components>
            {nodesJson}
            </components>
            
            ## Rules
            1. Include ONLY components from the list above
            2. Show the most important relationships (max 15 edges to keep diagram readable)
            3. Use proper Mermaid syntax with strict vertical orientation (graph TD or sequenceDiagram)
            4. Add meaningful labels to edges where appropriate
            5. Group by layer if applicable using subgraph blocks
            6. Use descriptive but short node IDs
            
            ## Diagram Types
            - For "component": Use classDiagram or graph TD showing class relationships
            - For "sequence": Use sequenceDiagram showing method call flows
            - For "dataflow": Use graph LR showing data transformations
            - For "architecture": Use graph TD with subgraphs for layers
            
            ## Output
            Return ONLY the Mermaid code block, no explanation:
            ```mermaid
            ...
            ```
            """;
    }

    #endregion

    #region Legacy Page Prompt (Kept for backward compatibility)

    /// <summary>
    /// Legacy page prompt for simple page generation without module context.
    /// Consider using EnhancedPagePrompt for new implementations.
    /// </summary>
    public static string PagePrompt(string title, IEnumerable<string> filePaths, string language)
    {
        return $"""
            You are an expert technical writer and software architect.
            Your goal is to document the EXISTING codebase as it is.
            Your task is to generate a comprehensive and accurate technical wiki page in Markdown format.

            Page Topic: "{title}"

            STRICT OUTPUT RULES:
            1. Return ONLY the Markdown content.
            2. GROUNDING: References to classes, methods, or files must exist in the provided "SOURCE FILES CONTENT".
            3. Do not invent "standard classes" (like Factories, Builders) if they are not seen in the code.
            4. DO NOT include any conversational filler (e.g., "Here is the page...").
            5. The output MUST start immediately with the page title (# {title}).

            Structure:
            1. **Title:** `# {title}`
            2. **Introduction:** Concise introduction (1-2 paragraphs).
            3. **Detailed Sections:** Break down into H2 (`##`) and H3 (`###`) sections.
            4. **Mermaid Diagrams:** Use Mermaid diagrams to visualize ACTUAL code relationships. 
               - Do not hallucinate connections or classes that are not present in the source.
               - STRICT vertical orientation (graph TD).
            5. **Tables:** Use Markdown tables for summaries.
            6. **Source Citations:** Cite specific source files for every significant piece of info.
            
            Closing:
            At the VERY END of the page, include a `<details>` block listing ALL source files used, formatted as:
            <details>
            <summary>Relevant source files</summary>

            {string.Join("\n", filePaths.Select(p => $"- {p}"))}
            </details>

            IMPORTANT: Generate the content in {language} language.
            """;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Extracts the first paragraph from markdown content for summaries.
    /// </summary>
    private static string ExtractFirstParagraph(string? content)
    {
        if (string.IsNullOrEmpty(content)) return "";
        
        // Skip the title line if present
        var lines = content.Split('\n');
        var startIndex = 0;
        
        // Skip title (# ...) and empty lines
        while (startIndex < lines.Length && 
               (lines[startIndex].TrimStart().StartsWith('#') || 
                string.IsNullOrWhiteSpace(lines[startIndex])))
        {
            startIndex++;
        }
        
        // Collect lines until we hit a blank line or another header
        var paragraphLines = new List<string>();
        for (var i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                break;
            paragraphLines.Add(line);
        }
        
        var paragraph = string.Join(" ", paragraphLines).Trim();
        
        // Truncate if too long
        if (paragraph.Length > 300)
            return paragraph.Substring(0, 297) + "...";
            
        return paragraph;
    }

    #endregion
}

#region Supporting Models

/// <summary>
/// Context information for module page generation, extracted from the dependency graph.
/// </summary>
public class ModulePageContext
{
    /// <summary>
    /// IDs of components that this module depends on (external to the module).
    /// </summary>
    public List<string> Dependencies { get; set; } = new();
    
    /// <summary>
    /// IDs of components that depend on this module (external to the module).
    /// </summary>
    public List<string> Dependents { get; set; } = new();
}

/// <summary>
/// Simplified node information for diagram generation.
/// </summary>
public class DiagramNodeInfo
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Layer { get; set; }
    public int OutDegree { get; set; }
    public int InDegree { get; set; }
}

#endregion