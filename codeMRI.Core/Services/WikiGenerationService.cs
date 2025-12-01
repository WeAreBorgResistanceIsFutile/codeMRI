using codeMRI.Shared.Models;
using System.Xml.Linq;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Visualization.Interfaces;
using System.Text;

namespace codeMRI.Core.Services;

public class WikiGenerationService : IWikiGenerationService
{
    private readonly ILLMClient _llmClient;
    private readonly IEmbedder _embedder;
    private readonly IVectorDatabase _vectorDb;
    private readonly IDiagramGenerator _diagramGenerator;
    private readonly IEnhancedDependencyGraphService _graphService; // Needed to fetch graph for diagrams

    public WikiGenerationService(
        ILLMClient llmClient, 
        IEmbedder embedder, 
        IVectorDatabase vectorDb,
        IDiagramGenerator diagramGenerator,
        IEnhancedDependencyGraphService graphService)
    {
        _llmClient = llmClient;
        _embedder = embedder;
        _vectorDb = vectorDb;
        _diagramGenerator = diagramGenerator;
        _graphService = graphService;
    }

    public async Task<WikiStructure> GenerateStructureAsync(string fileTree, string readme, string language = "English")
    {
        var prompt = PromptTemplates.StructurePrompt(fileTree, readme, language);
        var response = await _llmClient.ChatAsync("", prompt, new List<ChatMessage>());
        
        var cleanXml = response.Replace("```xml", "").Replace("```", "").Trim();
        try 
        {
            int start = cleanXml.IndexOf("<wiki_structure>");
            int end = cleanXml.LastIndexOf("</wiki_structure>");
            if (start >= 0 && end > start)
            {
                cleanXml = cleanXml.Substring(start, end - start + 17); 
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
            return structure; 
        }
        catch (Exception)
        {
            return new WikiStructure { Title = "Error generating structure", Sections = new() };
        }
    }

    public async Task<WikiPage> GeneratePageAsync(string pageTitle, List<string> filePaths, Dictionary<string, string> fileContents, string language = "English")
    {
        if (filePaths == null || filePaths.Count == 0)
        {
            var queryEmbedding = await _embedder.EmbedAsync(pageTitle);
            var docs = await _vectorDb.SearchAsync(queryEmbedding, topK: 5);
            filePaths = docs.Select(d => d.FilePath).Distinct().ToList();
            
            foreach (var doc in docs)
            {
                if (!fileContents.ContainsKey(doc.FilePath))
                {
                    fileContents[doc.FilePath] = doc.Content;
                }
            }
        }

        var contextBuilder = new StringBuilder();
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
        var fullPrompt = prompt + "\n\nSOURCE FILES CONTENT:\n" + contextBuilder.ToString();

        var content = await _llmClient.ChatAsync("", fullPrompt, new List<ChatMessage>());

        // Generate Sequence Diagram if applicable (e.g. for Controllers or Services)
        // We need to reconstruct/get the graph. For this scope, assuming we can get graph or skip.
        // Ideally, we'd pass the ModuleNode or Component ID to GeneratePageAsync.
        // Since we don't have it here, we might skip or try to match file path to component.

        return new WikiPage
        {
            Id = Guid.NewGuid().ToString(),
            Title = pageTitle,
            Content = content,
            RelevantFiles = filePaths
        };
    }

    public async Task<WikiPage> GenerateParentPageAsync(ModuleNode module, List<WikiPage> childPages, string language = "English")
    {
        // 1. Generate Architectural Overview
        var sb = new StringBuilder();
        sb.AppendLine($"Synthesize an architectural overview for the module: {module.Name}");
        sb.AppendLine($"This module is at level {module.Level} in the hierarchy.");
        
        if (!string.IsNullOrEmpty(module.Description))
        {
            sb.AppendLine($"Module Description: {module.Description}");
        }
        
        sb.AppendLine("\nSub-modules/Components:");
        foreach (var page in childPages)
        {
            sb.AppendLine($"- **{page.Title}**: {ExtractSummary(page.Content)}");
        }
        
        sb.AppendLine("\nInstructions:");
        sb.AppendLine("1. Create a high-level overview of this module's responsibilities.");
        sb.AppendLine("2. Explain how the sub-modules interact and contribute to the overall goal.");
        sb.AppendLine("3. Identify key architectural patterns used in this module.");
        sb.AppendLine($"4. Write the response in {language}.");
        
        var prompt = sb.ToString();
        var content = await _llmClient.ChatAsync("", prompt, new List<ChatMessage>());
        
        // 2. Generate Diagrams
        var graph = await _graphService.BuildGraphAsync(new List<CodeComponent>(), CancellationToken.None); // This might be empty if not cached? 
        // We need a way to get the full graph. Assuming GraphService has state or we pass components.
        // IMPORTANT: In current architecture, BuildGraphAsync takes components. We likely need to persist the graph or pass it down.
        // For now, we'll assume we can't easily get the full graph here without re-parsing, 
        // so we will skip diagram generation in this method unless we refactor to inject the graph.
        
        // However, we can generate a diagram based on the ModuleTree structure we have in 'module'
        // Using a simplified method in DiagramGenerator that takes ModuleNode
        
        if (_diagramGenerator is codeMRI.Visualization.Services.DiagramGeneratorService concreteGenerator)
        {
             // Assuming we can get at least a partial graph or we rely on what's available
             // If we can't get the graph, we can't generate edges.
        }
        
        return new WikiPage
        {
            Id = Guid.NewGuid().ToString(),
            Title = module.Name,
            Content = content,
            RelevantFiles = new List<string>() 
        };
    }
    
    private string ExtractSummary(string content)
    {
        if (string.IsNullOrEmpty(content)) return "";
        var idx = content.IndexOf("\n\n");
        if (idx > 0) return content.Substring(0, idx);
        return content.Length > 200 ? content.Substring(0, 200) + "..." : content;
    }
}
