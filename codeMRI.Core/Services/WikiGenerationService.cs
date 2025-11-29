using codeMRI.Shared.Models;
using System.Xml.Linq;
using codeMRI.Core.Interfaces;

namespace codeMRI.Core.Services;

public class WikiGenerationService
{
    private readonly ILLMClient _llmClient;
    private readonly IEmbedder _embedder; // Needed if we want to retrieve content for generation
    private readonly IVectorDatabase _vectorDb; // Needed to find files if not provided
    
    // For MVP, we might assume files are loaded in memory or passed directly, 
    // but let's stick to the RAG pattern if possible or direct file reading.
    // We'll assume we have a way to "Get File Content" from the repo.
    // Let's add a helper to access the raw repo files via the IDocumentProcessor or a RepoService.
    // For now, we'll assume the "Context" passed to us contains the necessary info.
    
    public WikiGenerationService(ILLMClient llmClient, IEmbedder embedder, IVectorDatabase vectorDb)
    {
        _llmClient = llmClient;
        _embedder = embedder;
        _vectorDb = vectorDb;
    }

    public async Task<WikiStructure> GenerateStructureAsync(string fileTree, string readme, string language = "English")
    {
        var prompt = PromptTemplates.StructurePrompt(fileTree, readme, language);
        var response = await _llmClient.ChatAsync("", prompt, new List<ChatMessage>());
        
        // Parse XML response
        // This is a simplified parser, real one should handle markdown code blocks wrapping the XML
        var cleanXml = response.Replace("```xml", "").Replace("```", "").Trim();
        try 
        {
            // Find start and end of xml
            int start = cleanXml.IndexOf("<wiki_structure>");
            int end = cleanXml.LastIndexOf("</wiki_structure>");
            if (start >= 0 && end > start)
            {
                cleanXml = cleanXml.Substring(start, end - start + 17); // 17 is length of closing tag
            }

            var doc = XDocument.Parse(cleanXml);
            var root = doc.Element("wiki_structure");
            
            var structure = new WikiStructure
            {
                Title = root?.Element("title")?.Value ?? "Wiki",
                Description = root?.Element("description")?.Value ?? "",
                Sections = root?.Element("sections")?.Elements("section").Select(s => new WikiSection
                {
                    Id = s.Attribute("id")?.Value ?? Guid.NewGuid().ToString(),
                    Title = s.Element("title")?.Value ?? "Section",
                    PageRefs = s.Element("pages")?.Elements("page_ref").Select(p => p.Value).ToList() ?? new()
                }).ToList() ?? new()
            };
            
            // We also need to extract the Page definitions to store them somewhere, 
            // but for this method we just return the structure. 
            // Ideally we should return both or have a stateful process.
            // For this MVP, let's attach the pages to the structure or handle them separately.
            // Let's stick to the WikiStructure model which defines hierarchy.
            // The 'Pages' detailed info is usually separate.
            // Let's assume the caller handles the XML parsing fully or we adjust the model.
            
            return structure; 
        }
        catch (Exception)
        {
            // Fallback structure on error
            return new WikiStructure { Title = "Error generating structure", Sections = new() };
        }
    }

    public async Task<WikiPage> GeneratePageAsync(string pageTitle, List<string> filePaths, Dictionary<string, string> fileContents, string language = "English")
    {
        // If no files provided, search for them using RAG
        if (filePaths == null || filePaths.Count == 0)
        {
            var queryEmbedding = await _embedder.EmbedAsync(pageTitle);
            var docs = await _vectorDb.SearchAsync(queryEmbedding, topK: 5);
            filePaths = docs.Select(d => d.FilePath).Distinct().ToList();
            
            // Populate content from the found docs
            foreach (var doc in docs)
            {
                if (!fileContents.ContainsKey(doc.FilePath))
                {
                    fileContents[doc.FilePath] = doc.Content;
                }
            }
        }

        // Construct context from file contents
        var contextBuilder = new System.Text.StringBuilder();
        foreach (var path in filePaths)
        {
            if (fileContents.ContainsKey(path))
            {
                contextBuilder.AppendLine($"File: {path}");
                contextBuilder.AppendLine("```");
                contextBuilder.AppendLine(fileContents[path]);
                contextBuilder.AppendLine("```");
                contextBuilder.AppendLine();
            }
        }
        
        var prompt = PromptTemplates.PagePrompt(pageTitle, filePaths, language);
        // We append the file content to the prompt or system message
        var fullPrompt = prompt + "\n\nSOURCE FILES CONTENT:\n" + contextBuilder.ToString();

        var content = await _llmClient.ChatAsync("", fullPrompt, new List<ChatMessage>());

        return new WikiPage
        {
            Id = Guid.NewGuid().ToString(),
            Title = pageTitle,
            Content = content,
            RelevantFiles = filePaths
        };
    }
}
