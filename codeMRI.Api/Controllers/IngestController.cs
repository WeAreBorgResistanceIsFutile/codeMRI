using System.Security.Cryptography;
using System.Text;
using codeMRI.Core.Interfaces;
using codeMRI.Shared.DTOs;
using codeMRI.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace codeMRI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngestController : ControllerBase
{
    private readonly IEmbedder _embedder;
    private readonly ILogger<IngestController> _logger;
    private readonly IDocumentProcessor _processor;
    private readonly IVectorDatabase _vectorDb;
    private readonly IWikiRepository _wikiRepo;

    public IngestController(IDocumentProcessor processor, IEmbedder embedder, IVectorDatabase vectorDb,
        IWikiRepository wikiRepo, ILogger<IngestController> logger)
    {
        _processor = processor;
        _embedder = embedder;
        _vectorDb = vectorDb;
        _wikiRepo = wikiRepo;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> IngestRepo([FromBody] IngestRequest request)
    {
        if (request.Delete)
        {
            await _vectorDb.DeleteByMetadataAsync("repo_path", request.RepoPath);
            await _wikiRepo.DeleteIngestionManifestAsync(request.RepoPath);
            return Ok(new { Message = "Repository data deleted successfully." });
        }

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
            return StatusCode(500,
                $"Ollama Error: Unable to connect or model not found. Details: {ex.Message}. Ensure Ollama is running and model is pulled.");
        }

        try
        {
            // Handle Force Ingest: Wipe clean first
            if (request.Force)
            {
                await _vectorDb.DeleteByMetadataAsync("repo_path", request.RepoPath);
                await _wikiRepo.DeleteIngestionManifestAsync(request.RepoPath);
            }

            var manifest = await _wikiRepo.GetIngestionManifestAsync(request.RepoPath);

            // 2. Safe Directory Walk
            var allFiles = SafeGetFiles(request.RepoPath);

            var documents = new List<Document>();
            var processedFiles = 0;
            var upToDateFiles = 0;
            var skippedErrorFiles = 0;
            var errors = new List<string>();

            foreach (var file in allFiles)
                try
                {
                    if (IsIgnored(file)) continue;
                    if (!IsTextFile(file)) continue;

                    var content = await System.IO.File.ReadAllTextAsync(file);
                    var currentHash = CalculateHash(content);
                    var relativePath = Path.GetRelativePath(request.RepoPath, file);

                    // Check if changed
                    if (manifest.TryGetValue(relativePath, out var storedHash) && storedHash == currentHash)
                    {
                        upToDateFiles++;
                        continue;
                    }

                    // If updating an existing file, clear its old chunks first to avoid duplicates
                    if (manifest.ContainsKey(relativePath))
                        // We rely on the relative path being stored in metadata as "file_path"
                        // Note: QdrantVectorDb implementation of DeleteByMetadataAsync uses exact match.
                        await _vectorDb.DeleteByMetadataAsync("file_path", relativePath);

                    var doc = new Document
                    {
                        FilePath = relativePath,
                        Content = content
                    };
                    doc.Metadata.Add("repo_path", request.RepoPath);

                    // Split into chunks
                    var chunks = _processor.Split(doc).ToList();

                    // Embed chunks
                    foreach (var chunk in chunks)
                    {
                        chunk.Embedding = await _embedder.EmbedAsync(chunk.Content);
                        documents.Add(chunk);
                    }

                    // Update manifest
                    manifest[relativePath] = currentHash;
                    processedFiles++;
                }
                catch (Exception fileEx)
                {
                    _logger.LogWarning(fileEx, "Failed to process file: {FilePath}", file);
                    skippedErrorFiles++;
                    if (errors.Count < 5) errors.Add($"{Path.GetFileName(file)}: {fileEx.Message}");
                }

            if (documents.Count > 0) await _vectorDb.UpsertAsync(documents);

            // Save updated manifest
            await _wikiRepo.SaveIngestionManifestAsync(request.RepoPath, manifest);

            var msg =
                $"Ingested {processedFiles} new/changed files ({documents.Count} chunks). {upToDateFiles} files up-to-date. Skipped {skippedErrorFiles} errors.";
            if (errors.Any()) msg += " Sample errors: " + string.Join(", ", errors);

            return Ok(new { Message = msg });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting repo");
            return StatusCode(500, $"Critical Error: {ex.Message}");
        }
    }

    private string CalculateHash(string input)
    {
        using var md5 = MD5.Create();
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = md5.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes);
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
                    if (dirName.StartsWith(".") || dirName == "bin" || dirName == "obj" ||
                        dirName == "node_modules") continue;
                    stack.Push(subDir);
                }
            }
            catch (UnauthorizedAccessException)
            {
                /* Ignore */
            }
            catch (DirectoryNotFoundException)
            {
                /* Ignore */
            }
        }

        return files;
    }

    private bool IsTextFile(string path)
    {
        var ext = Path.GetExtension(path).ToLower();
        // Added more extensions
        return new[]
            {
                ".cs", ".py", ".js", ".ts", ".md", ".txt", ".json", ".xml", ".html", ".css", ".java", ".cpp", ".h",
                ".yml", ".yaml", ".csproj", ".sln", ".gitignore", ".sh", ".bat", ".razor"
            }
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