using System.Security.Cryptography;
using System.Text;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Server.Api;
using Microsoft.AspNetCore.Mvc;

namespace codeMRI.Server.Controllers;

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
            await _wikiRepo.DeleteIngestionProcessingStateAsync(request.RepoPath);
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
            // Check if there's an existing processing state
            var processingState = await _wikiRepo.GetIngestionProcessingStateAsync(request.RepoPath);

            // If processing is already in progress and not stale, return status
            if (processingState.CurrentStatus == "processing" &&
                DateTime.UtcNow.Subtract(processingState.LastCheckpoint).TotalMinutes <= 5)
            {
                return Ok(new {
                    Message = "Ingestion is already in progress",
                    Status = processingState.CurrentStatus,
                    Progress = $"{processingState.CompletedFiles.Count}/{processingState.TotalFiles}"
                });
            }

            // Handle Force Ingest: Wipe clean first
            if (request.Force)
            {
                await _vectorDb.DeleteByMetadataAsync("repo_path", request.RepoPath);
                await _wikiRepo.DeleteIngestionManifestAsync(request.RepoPath);
                await _wikiRepo.DeleteIngestionProcessingStateAsync(request.RepoPath);
            }

            var manifest = await _wikiRepo.GetIngestionManifestAsync(request.RepoPath);

            // 2. Safe Directory Walk
            var allFiles = SafeGetFiles(request.RepoPath);

            // Initialize or reset processing state
            processingState = new IngestionProcessingState
            {
                CurrentStatus = "processing",
                StartedAt = DateTime.UtcNow,
                LastCheckpoint = DateTime.UtcNow
            };

            // Build processing queue - only include new or changed files
            foreach (var file in allFiles)
            {
                if (IsIgnored(file)) continue;
                if (!IsTextFile(file)) continue;

                var relativePath = Path.GetRelativePath(request.RepoPath, file);
                if (!manifest.ContainsKey(relativePath))
                {
                    // New file
                    processingState.ProcessingQueue.Add(relativePath);
                }
                else
                {
                    // Check if file has changed
                    try
                    {
                        var content = await System.IO.File.ReadAllTextAsync(file);
                        var currentHash = CalculateHash(content);
                        if (manifest[relativePath] != currentHash)
                        {
                            processingState.ProcessingQueue.Add(relativePath);
                        }
                    }
                    catch (Exception)
                    {
                        // If we can't read the file, add it to the queue to try again
                        processingState.ProcessingQueue.Add(relativePath);
                    }
                }
            }

            processingState.TotalFiles = processingState.ProcessingQueue.Count;
            await _wikiRepo.SaveIngestionProcessingStateAsync(request.RepoPath, processingState);

            var documents = new List<Document>();
            var processedFiles = 0;
            var upToDateFiles = allFiles.Count - processingState.ProcessingQueue.Count;
            var skippedErrorFiles = 0;
            var errors = new List<string>();

            // Process files in batches
            while (processingState.ProcessingQueue.Count > 0)
            {
                var batch = processingState.ProcessingQueue.Take(processingState.CurrentBatchSize).ToList();
                processingState.ProcessingQueue.RemoveRange(0, Math.Min(processingState.CurrentBatchSize, processingState.ProcessingQueue.Count));

                foreach (var relativePath in batch)
                {
                    var file = Path.Combine(request.RepoPath, relativePath);
                    try
                    {
                        var content = await System.IO.File.ReadAllTextAsync(file);
                        var currentHash = CalculateHash(content);

                        // If updating an existing file, clear its old chunks first to avoid duplicates
                        if (manifest.ContainsKey(relativePath))
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
                        processingState.CompletedFiles.Add(relativePath);
                        processedFiles++;
                    }
                    catch (Exception fileEx)
                    {
                        _logger.LogWarning(fileEx, "Failed to process file: {FilePath}", file);
                        skippedErrorFiles++;
                        processingState.FailedFiles.Add(relativePath);
                        if (processingState.Errors.Count < 20)
                        {
                            processingState.Errors[relativePath] = fileEx.Message;
                        }
                        if (errors.Count < 5) errors.Add($"{Path.GetFileName(file)}: {fileEx.Message}");
                    }
                }

                // Save checkpoint
                processingState.LastCheckpoint = DateTime.UtcNow;
                await _wikiRepo.SaveIngestionProcessingStateAsync(request.RepoPath, processingState);
            }

            if (documents.Count > 0) await _vectorDb.UpsertAsync(documents);

            // Save updated manifest
            await _wikiRepo.SaveIngestionManifestAsync(request.RepoPath, manifest);

            // Mark processing as completed
            processingState.CurrentStatus = "completed";
            processingState.CompletedAt = DateTime.UtcNow;
            await _wikiRepo.SaveIngestionProcessingStateAsync(request.RepoPath, processingState);

            var msg =
                $"Ingested {processedFiles} new/changed files ({documents.Count} chunks). {upToDateFiles} files up-to-date. Skipped {skippedErrorFiles} errors.";
            if (errors.Any()) msg += " Sample errors: " + string.Join(", ", errors);

            return Ok(new { Message = msg });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting repo");

            // Update processing state to error
            var processingState = await _wikiRepo.GetIngestionProcessingStateAsync(request.RepoPath);
            processingState.CurrentStatus = "error";
            processingState.LastCheckpoint = DateTime.UtcNow;
            if (processingState.Errors.Count < 20)
            {
                processingState.Errors["critical_error"] = ex.Message;
            }
            await _wikiRepo.SaveIngestionProcessingStateAsync(request.RepoPath, processingState);

            return StatusCode(500, $"Critical Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the current status of an ingestion process
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetIngestionStatus([FromQuery] string repoPath)
    {
        if (string.IsNullOrEmpty(repoPath))
            return BadRequest("Repository path is required");

        if (!Directory.Exists(repoPath))
            return BadRequest($"Directory not found: {repoPath}");

        try
        {
            var processingState = await _wikiRepo.GetIngestionProcessingStateAsync(repoPath);
            var manifest = await _wikiRepo.GetIngestionManifestAsync(repoPath);

            var response = new IngestionStatusResponse
            {
                Status = processingState.CurrentStatus,
                TotalFiles = processingState.TotalFiles,
                ProcessedFiles = processingState.CompletedFiles.Count,
                RemainingFiles = processingState.ProcessingQueue.Count,
                FailedFiles = processingState.FailedFiles.Count,
                ProgressPercentage = processingState.TotalFiles > 0
                    ? (double)processingState.CompletedFiles.Count / processingState.TotalFiles * 100
                    : 100,
                StartedAt = processingState.StartedAt,
                CompletedAt = processingState.CompletedAt,
                LastCheckpoint = processingState.LastCheckpoint,
                CanResume = processingState.CurrentStatus == "paused" ||
                           processingState.CurrentStatus == "error" ||
                           (processingState.CurrentStatus == "processing" &&
                            DateTime.UtcNow.Subtract(processingState.LastCheckpoint).TotalMinutes > 5)
            };

            // Add recent errors
            response.RecentErrors.AddRange(processingState.Errors.Values.Take(5));

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ingestion status for {RepoPath}", repoPath);
            return StatusCode(500, $"Error getting ingestion status: {ex.Message}");
        }
    }

    /// <summary>
    /// Resumes an interrupted ingestion process
    /// </summary>
    [HttpPost("resume")]
    public async Task<IActionResult> ResumeIngestion([FromBody] ResumeIngestionRequest request)
    {
        if (string.IsNullOrEmpty(request.RepoPath))
            return BadRequest("Repository path is required");

        if (!Directory.Exists(request.RepoPath))
            return BadRequest($"Directory not found: {request.RepoPath}");

        try
        {
            // Test Ollama Connection
            await _embedder.EmbedAsync("test");

            var processingState = await _wikiRepo.GetIngestionProcessingStateAsync(request.RepoPath);

            // Check if we can resume
            if (processingState.CurrentStatus != "paused" &&
                processingState.CurrentStatus != "error" &&
                !(processingState.CurrentStatus == "processing" &&
                  DateTime.UtcNow.Subtract(processingState.LastCheckpoint).TotalMinutes <= 5))
            {
                return BadRequest("No interrupted ingestion process found to resume");
            }

            // Update batch size if provided
            if (request.BatchSize.HasValue && request.BatchSize.Value > 0)
            {
                processingState.CurrentBatchSize = request.BatchSize.Value;
            }

            // Reset status to processing
            processingState.CurrentStatus = "processing";
            processingState.LastCheckpoint = DateTime.UtcNow;
            await _wikiRepo.SaveIngestionProcessingStateAsync(request.RepoPath, processingState);

            var manifest = await _wikiRepo.GetIngestionManifestAsync(request.RepoPath);
            var documents = new List<Document>();
            var processedFiles = 0;
            var skippedErrorFiles = 0;
            var errors = new List<string>();

            // Process files in batches
            while (processingState.ProcessingQueue.Count > 0)
            {
                var batch = processingState.ProcessingQueue.Take(processingState.CurrentBatchSize).ToList();
                processingState.ProcessingQueue.RemoveRange(0, Math.Min(processingState.CurrentBatchSize, processingState.ProcessingQueue.Count));

                foreach (var relativePath in batch)
                {
                    var file = Path.Combine(request.RepoPath, relativePath);
                    try
                    {
                        var content = await System.IO.File.ReadAllTextAsync(file);
                        var currentHash = CalculateHash(content);

                        // If updating an existing file, clear its old chunks first to avoid duplicates
                        if (manifest.ContainsKey(relativePath))
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
                        processingState.CompletedFiles.Add(relativePath);
                        processedFiles++;
                    }
                    catch (Exception fileEx)
                    {
                        _logger.LogWarning(fileEx, "Failed to process file: {FilePath}", file);
                        skippedErrorFiles++;
                        processingState.FailedFiles.Add(relativePath);
                        if (processingState.Errors.Count < 20)
                        {
                            processingState.Errors[relativePath] = fileEx.Message;
                        }
                        if (errors.Count < 5) errors.Add($"{Path.GetFileName(file)}: {fileEx.Message}");
                    }
                }

                // Save checkpoint
                processingState.LastCheckpoint = DateTime.UtcNow;
                await _wikiRepo.SaveIngestionProcessingStateAsync(request.RepoPath, processingState);
            }

            if (documents.Count > 0) await _vectorDb.UpsertAsync(documents);

            // Save updated manifest
            await _wikiRepo.SaveIngestionManifestAsync(request.RepoPath, manifest);

            // Mark processing as completed
            processingState.CurrentStatus = "completed";
            processingState.CompletedAt = DateTime.UtcNow;
            await _wikiRepo.SaveIngestionProcessingStateAsync(request.RepoPath, processingState);

            var msg =
                $"Resumed ingestion: processed {processedFiles} files ({documents.Count} chunks). Skipped {skippedErrorFiles} errors.";
            if (errors.Any()) msg += " Sample errors: " + string.Join(", ", errors);

            return Ok(new { Message = msg });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming ingestion");

            // Update processing state to error
            var processingState = await _wikiRepo.GetIngestionProcessingStateAsync(request.RepoPath);
            processingState.CurrentStatus = "error";
            processingState.LastCheckpoint = DateTime.UtcNow;
            if (processingState.Errors.Count < 20)
            {
                processingState.Errors["critical_error"] = ex.Message;
            }
            await _wikiRepo.SaveIngestionProcessingStateAsync(request.RepoPath, processingState);

            return StatusCode(500, $"Error resuming ingestion: {ex.Message}");
        }
    }

    private string CalculateHash(string input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        using var sha512 = SHA512.Create();
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha512.ComputeHash(inputBytes);
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
        if (normalized.Contains("/.git/") ||
            normalized.Contains("/node_modules/") ||
            normalized.Contains("/bin/") ||
            normalized.Contains("/obj/") ||
            normalized.Contains("/.vs/") ||
            normalized.Contains("/.idea/") ||
            normalized.Contains("/.vscode/") ||
            normalized.Contains("/wwwroot/lib/") || // Ignore client-side libraries
            normalized.Contains("/dist/"))          // Ignore build artifacts
        {
            return true;
        }

        // Check file size (skip > 1MB)
        try
        {
            var info = new FileInfo(path);
            if (info.Length > 1024 * 1024) return true;
        }
        catch
        {
            // If we can't check size, assume it's fine or let it fail later
        }

        return false;
    }
}
