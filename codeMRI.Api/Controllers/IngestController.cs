using codeMRI.Core.Interfaces;
using codeMRI.Shared.DTOs;
using codeMRI.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace codeMRI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngestController : ControllerBase
{
    private readonly IDocumentProcessor _processor;
    private readonly IEmbedder _embedder;
    private readonly IVectorDatabase _vectorDb;
    private readonly ILogger<IngestController> _logger;

    public IngestController(IDocumentProcessor processor, IEmbedder embedder, IVectorDatabase vectorDb, ILogger<IngestController> logger)
    {
        _processor = processor;
        _embedder = embedder;
        _vectorDb = vectorDb;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> IngestRepo([FromBody] IngestRequest request)
    {
        if (!Directory.Exists(request.RepoPath))
            return BadRequest($"Directory not found: {request.RepoPath}");

        // 1. Test Ollama Connection
        try 
        {
            // Try to embed a simple string to verify Ollama is up and model exists
            await _embedder.EmbedAsync("test");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama connection failed");
            return StatusCode(500, $"Ollama Error: Unable to connect or model not found. Details: {ex.Message}. Ensure Ollama is running and model is pulled.");
        }

        try
        {
            // 2. Safe Directory Walk
            var files = SafeGetFiles(request.RepoPath);
            
            var documents = new List<Document>();
            int processedFiles = 0;
            int skippedFiles = 0;
            var errors = new List<string>();

            foreach (var file in files)
            {
                try 
                {
                    if (IsIgnored(file)) continue;
                    if (!IsTextFile(file)) continue;

                    var content = await System.IO.File.ReadAllTextAsync(file);
                    var doc = new Document
                    {
                        FilePath = Path.GetRelativePath(request.RepoPath, file),
                        Content = content
                    };

                    // Split into chunks
                    var chunks = _processor.Split(doc).ToList();
                    
                    // Embed chunks
                    foreach (var chunk in chunks)
                    {
                        chunk.Embedding = await _embedder.EmbedAsync(chunk.Content);
                        documents.Add(chunk);
                    }
                    
                    processedFiles++;
                }
                catch (Exception fileEx)
                {
                    _logger.LogWarning(fileEx, "Failed to process file: {FilePath}", file);
                    skippedFiles++;
                    if (errors.Count < 5) errors.Add($"{Path.GetFileName(file)}: {fileEx.Message}");
                }
            }

            if (documents.Count > 0)
            {
                await _vectorDb.UpsertAsync(documents);
            }
            
            var msg = $"Ingested {processedFiles} files ({documents.Count} chunks). Skipped {skippedFiles} errors.";
            if (errors.Any()) msg += " Sample errors: " + string.Join(", ", errors);
            
            return Ok(new { Message = msg });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting repo");
            return StatusCode(500, $"Critical Error: {ex.Message}");
        }
    }

    private List<string> SafeGetFiles(string rootPath)
    {
        var files = new List<string>();
        var stack = new Stack<string>();
        stack.Push(rootPath);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            try
            {
                files.AddRange(Directory.GetFiles(dir));
                foreach (var subDir in Directory.GetDirectories(dir))
                {
                    // Skip hidden directories and common ignore folders early to save time
                    var dirName = Path.GetFileName(subDir);
                    if (dirName.StartsWith(".") || dirName == "bin" || dirName == "obj" || dirName == "node_modules")
                    {
                        continue;
                    }
                    stack.Push(subDir);
                }
            }
            catch (UnauthorizedAccessException) { /* Ignore */ }
            catch (DirectoryNotFoundException) { /* Ignore */ }
        }
        return files;
    }

    private bool IsTextFile(string path)
    {
        var ext = Path.GetExtension(path).ToLower();
        // Added more extensions
        return new[] { ".cs", ".py", ".js", ".ts", ".md", ".txt", ".json", ".xml", ".html", ".css", ".java", ".cpp", ".h", ".yml", ".yaml", ".csproj", ".sln", ".gitignore", ".sh", ".bat", ".razor" }
            .Contains(ext);
    }

    private bool IsIgnored(string path)
    {
        var normalized = path.Replace('\\', '/');
        // Robust check
        return normalized.Contains("/.git/") || 
               normalized.Contains("/node_modules/") || 
               normalized.Contains("/bin/") || 
               normalized.Contains("/obj/") ||
               normalized.Contains("/.vs/") ||
               normalized.Contains("/.idea/") ||
               normalized.Contains("/.vscode/");
    }
}
