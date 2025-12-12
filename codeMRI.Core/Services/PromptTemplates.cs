namespace codeMRI.Core.Services;

public static class PromptTemplates
{
    public static string StructurePrompt(string fileTree, string readme, string language)
    {
        return $"""
                Analyze this git repository and create a wiki structure for it.

                1. The complete file tree of project:
                <file_tree>
                {fileTree}
                </file_tree>

                2. The README file of project:
                <readme>
                {readme}
                </readme>

                I want to create a wiki for this repository. Create a structure that STRICTLY reflects the existing file organization and architectural layers found in the <file_tree>.

                CRITICAL RULES:
                1. DO NOT create sections or pages for layers (e.g., "Domain", "Data") unless there are specific matching directories or files in the file tree.
                2. Documentation must be grounded in ACTUAL files. Do not invent modules based on "implied" architecture.

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
}