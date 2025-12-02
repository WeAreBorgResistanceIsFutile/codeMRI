namespace codeMRI.Core.Services;

public static class PromptTemplates
{
    public static string StructurePrompt(string fileTree, string readme, string language)
    {
        return $"""
                Analyze this GitHub repository and create a wiki structure for it.

                1. The complete file tree of project:
                <file_tree>
                {fileTree}
                </file_tree>

                2. The README file of project:
                <readme>
                {readme}
                </readme>

                I want to create a wiki for this repository. Determine most logical structure for a wiki based on repository's content.

                IMPORTANT: The wiki content will be generated in {language} language.

                When designing the wiki structure, include pages that would benefit from visual diagrams.

                Return your analysis in following XML format:

                <wiki_structure>
                  <title>[Overall title for wiki]</title>
                  <description>[Brief description of repository]</description>
                  <sections>
                    <section id="section-1">
                      <title>[Section title]</title>
                      <pages>
                        <page_ref>page-1</page_ref>
                      </pages>
                    </section>
                  </sections>
                  <pages>
                    <page id="page-1">
                      <title>[Page title]</title>
                      <description>[Brief description]</description>
                      <importance>high|medium|low</importance>
                      <relevant_files>
                        <file_path>[Path to relevant file]</file_path>
                      </relevant_files>
                    </page>
                  </pages>
                </wiki_structure>
                """;
    }

    public static string PagePrompt(string title, IEnumerable<string> filePaths, string language)
    {
        return $"""
                You are an expert technical writer and software architect.
                Your task is to generate a comprehensive and accurate technical wiki page in Markdown format.

                Page Topic: "{title}"

                CRITICAL STARTING INSTRUCTION:
                The very first thing on page MUST be a `<details>` block listing ALL source files used.
                Format it exactly like this:
                <details>
                <summary>Relevant source files</summary>

                {string.Join("\n", filePaths.Select(p => $"- {p}"))}
                </details>

                Immediately after `<details>` block, main title of page should be a H1 Markdown heading: `# {title}`.

                1. **Introduction:** Concise introduction (1-2 paragraphs).
                2. **Detailed Sections:** Break down into H2 (`##`) and H3 (`###`) sections.
                3. **Mermaid Diagrams:** EXTENSIVELY use Mermaid diagrams (flowchart TD, sequenceDiagram, classDiagram). 
                   - STRICT vertical orientation (graph TD).
                4. **Tables:** Use Markdown tables for summaries.
                5. **Source Citations:** Cite specific source files for every significant piece of info.

                IMPORTANT: Generate the content in {language} language.
                """;
    }

    public static string RAGSystemPrompt(string language)
    {
        return $"""
                You are a code assistant which answers user questions on a Github Repo.
                You will receive user query, relevant context, and past conversation history.

                LANGUAGE DETECTION AND RESPONSE:
                - Respond in {language}.

                FORMAT YOUR RESPONSE USING MARKDOWN:
                - Use proper markdown syntax.
                - No markdown fences around the entire response.
                """;
    }
}