using System.Text.Json;
using codeMRI.Core.Interfaces;
using System.Text;
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
            - Do NOT include source file lists or citations sections (these will be added automatically)
            - Use {language} for all content
            
            Output ONLY the markdown content starting with # {module.Name}
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
        Dictionary<string, string> sourceFiles,
        AudienceType audience,
        string language,
        string? humanContext = null)
    {
        var role = audience == AudienceType.DevOps ? "DevOps Engineer" : "Technical Writer";
        var audienceDesc = audience == AudienceType.DevOps ? "System Administrators and DevOps" : "End Users and Non-Technical Stakeholders";
        
        var sb = new StringBuilder();
        sb.AppendLine($"You are an expert {role} writing documentation for {audienceDesc}.");
        sb.AppendLine($"Your goal is to write a clear, actionable guide for the module '{module.Name}'.");
        sb.AppendLine();
        
        if (!string.IsNullOrEmpty(humanContext))
        {
            sb.AppendLine("## EXISTING HUMAN CONTEXT");
            sb.AppendLine("The following documentation was written by humans. Use it to understand the high-level purpose and conceptual details. Prioritize this context over inferred details.");
            sb.AppendLine(humanContext);
            sb.AppendLine("## END HUMAN CONTEXT");
            sb.AppendLine();
        }
        
        sb.AppendLine("## INSTRUCTIONS");
        sb.AppendLine("Write a user-friendly guide covering the following aspects based ONLY on the source code provided:");
        sb.AppendLine("1. **Overview**: What is this component and what problem does it solve? (No code jargon)");
        sb.AppendLine("2. **Key Capabilities**: specific features available to the user/admin.");
        sb.AppendLine("3. **Configuration**: Look for environment variables, config files, or settings classes. specific flags.");
        sb.AppendLine("4. **Operational Requirements**: External dependencies (DBs, APIs) found in connection strings or clients.");
        sb.AppendLine("5. **Troubleshooting**: Common error messages or failure scenarios visible in exceptions/logging.");

        return $"""
            {sb}

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
    /// Generates a comprehensive prompt covering Developer, User, and DevOps perspectives.
    /// </summary>
    public static string ComprehensivePagePrompt(
        ModuleNode module,
        List<WikiPage>? relatedPages,
        ModulePageContext context,
        string language,
        string? humanContext = null)
    {
        var relatedSummaries = relatedPages?
            .Select(p => $"- **{p.Title}**: {ExtractFirstParagraph(p.Content)}")
            .Take(5)
            .ToList() ?? new List<string>();

        var dependenciesList = context.Dependencies?
            .Select(d => $"- {d}")
            .Take(10)
            .ToList() ?? new List<string>();
        
        var sb = new StringBuilder();
        sb.AppendLine($"You are an expert Technical Writer, Software Architect, and DevOps Engineer.");
        sb.AppendLine($"Your goal is to write a COMPLETE documentation suite for the module '{module.Name}' that serves all stakeholders.");
        sb.AppendLine();
        
        if (!string.IsNullOrEmpty(humanContext))
        {
            sb.AppendLine("## EXISTING HUMAN CONTEXT");
            sb.AppendLine("Use this human-written context to improved accuracy:");
            sb.AppendLine(humanContext);
            sb.AppendLine("## END HUMAN CONTEXT");
            sb.AppendLine();
        }
        
        sb.AppendLine("## INSTRUCTIONS");
        sb.AppendLine("Write a comprehensive page with the following sections:");
        sb.AppendLine();
        sb.AppendLine("### 1. Overview (For Everyone)");
        sb.AppendLine("- High-level purpose of the module.");
        sb.AppendLine("- Key problems it solves.");
        sb.AppendLine("- Primary capabilities.");
        sb.AppendLine();
        sb.AppendLine("### 2. User Guide (For End Users)");
        sb.AppendLine("- How to use the features.");
        sb.AppendLine("- Configuration options (user-facing).");
        sb.AppendLine("- Common use cases.");
        sb.AppendLine();
        sb.AppendLine("### 3. Technical Architecture (For Developers)");
        sb.AppendLine($"- Architectural Pattern: {module.Metadata.GetValueOrDefault("ArchitecturalPattern", "Not identified")}");
        sb.AppendLine($"- Metrics: Cohesion ({module.QualityMetrics.Cohesion:F2}), Coupling ({module.QualityMetrics.Coupling:F2}), Complexity ({module.ComplexityScore:F1})");
        sb.AppendLine("- Class/Component structure and key relationships.");
        sb.AppendLine("- Important public interfaces.");
        sb.AppendLine("- Mermaid Component Diagram (graph TD).");
        sb.AppendLine();
        sb.AppendLine("### 4. Operations & Deployment (For DevOps)");
        sb.AppendLine("- External dependencies (DBs, APIs, Queues) based on: " + (dependenciesList.Any() ? string.Join(", ", dependenciesList) : "None detected") + ".");
        sb.AppendLine("- Configuration (Env vars, settings files).");
        sb.AppendLine("- Troubleshooting and Logs.");
        sb.AppendLine();
        sb.AppendLine("### 5. API Reference (If applicable)");
        sb.AppendLine("- Key endpoints or methods.");
        
        return $"""
            {sb}

            STRICT RULES:
            - Structure the response exactly with the headers above.
            - Only document what exists in the SOURCE FILES CONTENT below.
            - Do NOT invent features.
            - Do NOT include source file lists or citations sections (these will be added automatically)
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

    /// <summary>
    /// Generates a comprehensive system overview prompt covering all audiences.
    /// </summary>
    public static string ComprehensiveParentPageSynthesisPrompt(
        ModuleNode parentModule,
        List<WikiPage> childPages,
        int crossModuleDependencies,
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
            Synthesize a Comprehensive System Overview for "{parentModule.Name}".
            
            ## Component Summaries
            {childSummariesJson}
            
            ## Architecture Insights
            - **Detected Pattern**: {parentModule.Metadata.GetValueOrDefault("ArchitecturalPattern", "Mixed")}
            - **Cross-Module Dependencies**: {crossModuleDependencies} connections
            - **Complexity**: {parentModule.ComplexityScore:F1}

            ## Instructions
            Create a master overview page with the following sections:

            ### 1. System Abstract
            - High-level purpose and value proposition.
            
            ### 2. User & Operations Overview
            - System capabilities.
            - Deployment Architecture.
            - External Integration Points.
            
            ### 3. Technical Architecture
            - Module collaboration and design patterns.
            - Cross-cutting concerns.
            - Architecture Diagrams reference (if applicable).
            
            STRICT RULES:
            - Synthesize information from child components.
            - Use {language} language.

            Output ONLY the markdown content starting with # {parentModule.Name}
            """;
    }

    #endregion

    #region Cluster Merging Prompt

    /// <summary>
    /// Generates a prompt for merging cluster pages into a single cohesive document.
    /// Used when a module was subdivided due to size constraints during dynamic delegation.
    /// </summary>
    public static string MergeClusterPagesPrompt(
        ModuleNode parentModule,
        List<WikiPage> clusterPages,
        string language)
    {
        // Extract section headers and content from each cluster page
        var clusterContents = clusterPages
            .Select((p, idx) => new
            {
                Index = idx,
                Title = p.Title,
                Content = p.Content,
                FileCount = p.RelevantFiles?.Count ?? 0
            })
            .ToList();

        var clusterSummary = string.Join("\n", clusterContents.Select(c => 
            $"- **{c.Title}**: {c.FileCount} files, {c.Content.Length} characters"));

        return $"""
            Merge documentation from {clusterPages.Count} cluster pages into a single comprehensive document for "{parentModule.Name}".
            
            ## Context
            This module was temporarily subdivided into clusters for processing efficiency. Your job is to merge them back into ONE cohesive document.
            
            ## Cluster Summary
            {clusterSummary}
            
            ## Cluster Contents
            {string.Join("\n\n---\n\n", clusterContents.Select(c => $"### Source: {c.Title}\n\n{c.Content}"))}
            
            ## Instructions
            1. **Consolidate into ONE document** titled "# {parentModule.Name}"
            2. **Merge redundant sections**: If multiple clusters have "Overview" or similar sections, synthesize them into ONE overview
            3. **Organize by logical sections**: Group related content from different clusters under unified section headers
            4. **Remove cluster-specific headers**: Replace "{parentModule.Name} - Cluster_N" with appropriate subsection names
            5. **Preserve all technical details**: Don't summarize or lose implementation details - include everything
            6. **Create unified diagrams**: If multiple clusters have similar diagrams, merge them into one comprehensive diagram
            7. **Deduplicate content**: If the same class/component is mentioned in multiple clusters, merge into one entry
            
            ## Suggested Structure
            - # {parentModule.Name} (main title)
            - ## Overview (merged from all cluster overviews)
            - ## Architecture (unified view of all components)
            - ## Components (organized by logical grouping, not cluster number)
            - ## Technical Details (merged implementation details)
            - ## API Reference (if applicable)
            
            STRICT RULES:
            - Output ONE complete markdown document
            - Do NOT create subsections for each cluster (e.g., no "## Cluster 0" sections)
            - Do NOT add meta-commentary about the merging process
            - Preserve all technical accuracy from source clusters
            - Use {language} for all content
            
            Output ONLY the merged markdown content starting with # {parentModule.Name}
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

    #region Parent Documentation Revision Prompt

    /// <summary>
    /// Generates a prompt for revising parent documentation based on completed child documentation.
    /// Implements the revision step from Algorithm 1 in the CodeWiki paper.
    /// </summary>
    public static string ReviseParentDocumentationPrompt(
        WikiPage originalParentPage,
        ModuleNode parentModule,
        List<WikiPage> childPages,
        string language)
    {
        var childInsights = childPages
            .Select(p => new
            {
                Title = p.Title,
                KeyTopics = ExtractKeyTopics(p.Content),
                ConcreteDetails = ExtractConcreteDetails(p.Content)
            });

        var childInsightsJson = JsonSerializer.Serialize(childInsights,
            new JsonSerializerOptions { WriteIndented = true });

        return $"""
            You are refining parent-level documentation based on insights from completed child module documentation.
            
            ## Original Parent Page
            <original_content>
            {originalParentPage.Content}
            </original_content>
            
            ## Child Module Insights
            The following insights were extracted from {childPages.Count} child documentation pages:
            <child_insights>
            {childInsightsJson}
            </child_insights>
            
            ## Module Context
            - **Module Name**: {parentModule.Name}
            - **Hierarchy Level**: {parentModule.Level}
            - **Architectural Pattern**: {parentModule.Metadata.GetValueOrDefault("ArchitecturalPattern", "Not identified")}
            - **Child Module Count**: {childPages.Count}
            
            ## Revision Goals (Algorithm 1 - CodeWiki Paper)
            1. **Enrich Abstract Descriptions**: Replace generic descriptions with specific patterns/components discovered in children
               - BEFORE: "This module handles data processing"
               - AFTER: "This module orchestrates three data pipelines: validation (InputValidator), transformation (DataTransformer), and enrichment (MetadataEnricher)"
            
            2. **Add Concrete Evidence**: Back up architectural claims with specific child implementations
               - Reference specific classes, patterns, or behaviors documented in children
               
            3. **Update Architecture Diagrams**: If a component diagram exists, ensure it accurately reflects child relationships
            
            4. **Cross-Reference Children**: Add [[WikiLink]] references to child documentation where appropriate
            
            5. **Preserve Accuracy**: Do NOT change content that is already accurate and specific
            
            ## Output Rules
            - Maintain the same overall structure as the original
            - Keep sections that are already well-detailed
            - Focus improvements on abstract/vague sections
            - Do NOT reduce the length of the document
            - Use {language} for all content
            
            Output ONLY the revised markdown content starting with # {parentModule.Name}
            """;
    }

    /// <summary>
    /// Extracts key topics from page content for revision context.
    /// </summary>
    private static string ExtractKeyTopics(string? content)
    {
        if (string.IsNullOrEmpty(content)) return "None identified";

        // Extract H2 and H3 headers as key topics
        var lines = content.Split('\n');
        var topics = lines
            .Where(l => l.TrimStart().StartsWith("## ") || l.TrimStart().StartsWith("### "))
            .Select(l => l.TrimStart('#', ' '))
            .Take(5)
            .ToList();

        return topics.Any() ? string.Join(", ", topics) : "General overview";
    }

    /// <summary>
    /// Extracts concrete details (class names, patterns) from page content.
    /// </summary>
    private static string ExtractConcreteDetails(string? content)
    {
        if (string.IsNullOrEmpty(content)) return "None";

        // Look for code references (backticked items) as concrete details
        var backtickPattern = new System.Text.RegularExpressions.Regex(@"`([^`]+)`");
        var matches = backtickPattern.Matches(content);

        var details = matches
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(m => m.Groups[1].Value)
            .Where(d => d.Length > 2 && d.Length < 50 && !d.Contains('\n'))
            .Distinct()
            .Take(8)
            .ToList();

        return details.Any() ? string.Join(", ", details) : "See documentation";
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
            6. **Source Citations:** Reference information from the source files, but do NOT include a source file list (this will be added automatically).

            IMPORTANT: Generate the content in {language} language.
            """;
    }

    #endregion

    #region Chunked Processing Prompts

    /// <summary>
    /// Generates a prompt for processing a chunk of text while considering previous findings.
    /// </summary>
    public static string ChunkedFindingsPrompt(int chunkIndex, int totalChunks, string previousFindings, string currentChunk)
    {
        return $"""
            Processing chunk {chunkIndex + 1} of {totalChunks}.
            
            ## Previous Findings
            {(string.IsNullOrEmpty(previousFindings) ? "None so far." : previousFindings)}
            
            ## New Chunk Content
            ```
            {currentChunk}
            ```
            
            ## Instructions
            Continue the analysis based on this new chunk. 
            Synthesize what you've found so far with the information in this new chunk.
            Your output should be an updated set of findings/documentation.
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

    /// <summary>
    /// Generates a C4 Context diagram using Mermaid syntax.
    /// </summary>
    public static string DeploymentDiagramPrompt(
        ModuleNode module,
        EnhancedDependencyGraph graph,
        string language)
    {
        return $@"
You are a Software Architect specializing in C4 Model diagrams.
Your task is to generate a C4 System Context diagram using Mermaid C4 syntax (C4Context).

## INPUT CONTEXT
Module: {module.Name}
Dependencies: The module has {graph.EdgeCount} dependencies detected in the code.

## INSTRUCTIONS
1. Analyze the implied system boundaries based on typical dependencies:
   - Database connections (SQL, Mongo, Postgres) -> External System containers.
   - External APIs (Stripe, Twilio, AWS) -> External System containers.
   - Message Queues (RabbitMQ, Kafka) -> External System containers.
   - Frontend/Clients -> Person actors.
2. Create a C4 Context diagram that shows the System (this module/repo) in the center and its relationships to these external systems and users.
3. Use strict Mermaid C4 syntax (`C4Context`).
4. Keep it high-level. Do NOT include internal classes or minor components. Focus on the ""Big Picture"".

## EXAMPLE OUTPUT
```mermaid
C4Context
    title System Context diagram for Internet Banking System
    Person(customer, ""Banking Customer"", ""A customer of the bank."")
    System(banking_system, ""Internet Banking System"", ""Allows customers to view information about their bank accounts."")
    System_Ext(mail_system, ""E-mail System"", ""The internal Microsoft Exchange e-mail system."")
    System_Ext(mainframe, ""Mainframe Banking System"", ""Stores all of the core banking information."")

    Rel(customer, banking_system, ""Uses"")
    Rel(banking_system, mail_system, ""Sends e-mails"", ""SMTP"")
    Rel(banking_system, mainframe, ""Uses"")
```

## OUTPUT FORMAT
Return ONLY the Mermaid code block.
Start with ```mermaid and end with ```.
Use the language '{language}' for labels if possible.
";
    }
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