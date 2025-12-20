using System.Diagnostics;
using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Infrastructure.Services;
using codeMRI.Server.Api;
using codeMRI.Server.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using AgentMessage = codeMRI.Agents.Models.AgentMessage;
using AudienceType = codeMRI.Core.Models.AudienceType;
using ProgressInfo = codeMRI.Core.Models.ProgressInfo;

namespace codeMRI.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WikiController : ControllerBase
{
    private readonly IHubContext<WikiHub> _hubContext;
    private readonly IIngestionJobManager _ingestionManager;
    private readonly ILogger<WikiController> _logger;
    private readonly AgentMessageBus _messageBus;
    private readonly ICodeWikiOrchestrator _orchestrator;
    private readonly IAgentTelemetryService _telemetryService;
    private readonly IWikiRepository _wikiRepo;
    private readonly IWikiGenerationService _wikiService;

    public WikiController(
        IWikiGenerationService wikiService,
        IWikiRepository wikiRepo,
        ICodeWikiOrchestrator orchestrator,
        IHubContext<WikiHub> hubContext,
        ILogger<WikiController> logger,
        AgentMessageBus messageBus,
        IAgentTelemetryService telemetryService,
        IIngestionJobManager ingestionManager)
    {
        _wikiService = wikiService;
        _wikiRepo = wikiRepo;
        _orchestrator = orchestrator;
        _hubContext = hubContext;
        _logger = logger;
        _messageBus = messageBus;
        _telemetryService = telemetryService;
        _ingestionManager = ingestionManager;
    }


    [HttpPost("page")]
    public async Task<IActionResult> GeneratePage([FromBody] PageGenerationRequest request)
    {
        if (!request.ForceRegenerate)
        {
            var existing = await _wikiRepo.GetPageByTitleAsync(request.RepoPath, request.Title);
            if (existing != null) return Ok(existing);
        }

        if (request.FileContents == null) request.FileContents = new Dictionary<string, string>();

        // Ensure we try to load content for all requested files if not provided
        if (!string.IsNullOrEmpty(request.RepoPath))
            foreach (var relPath in request.FilePaths)
            {
                // Skip if we already have content (and it's not empty/null)
                if (request.FileContents.TryGetValue(relPath, out var content) && !string.IsNullOrWhiteSpace(content))
                    continue;

                // Sanitize path to ensure it's relative
                var clearPath = relPath.TrimStart('/', '\\');
                var fullPath = Path.Combine(request.RepoPath, clearPath);

                try
                {
                    if (System.IO.File.Exists(fullPath))
                        request.FileContents[relPath] = await System.IO.File.ReadAllTextAsync(fullPath);
                    else
                        _logger.LogWarning("File not found for wiki generation: {Path} (Repo: {Repo})", fullPath,
                            request.RepoPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading file for wiki generation: {Path}", fullPath);
                }
            }

        var page = await _wikiService.GeneratePageAsync(request.Title, request.FilePaths, request.FileContents,
            request.Language, request.RepoPath);

        await _wikiRepo.SavePageAsync(request.RepoPath, page);

        return Ok(page);
    }


    [HttpGet("repositories")]
    public async Task<IActionResult> GetRepositories()
    {
        var repos = await _wikiRepo.GetAllRepositoriesAsync();
        return Ok(repos);
    }

    [HttpGet("repositories-summary")]
    public async Task<IActionResult> GetRepositorySummaries()
    {
        var summaries = await _wikiRepo.GetAllRepositorySummariesAsync();
        var apiSummaries = summaries.Select(s => new RepositorySummary
        {
            Path = s.Path,
            Name = s.Name,
            IsIngested = s.IsIngested,
            CreatedAt = s.CreatedAt,
            RemoteUrl = s.RemoteUrl
        }).ToList();

        return Ok(apiSummaries);
    }

    [HttpPost("ingest")]
    public async Task<IActionResult> IngestRepository([FromBody] IngestionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Url))
            return BadRequest("URL is required.");

        if (!GitHelper.IsGitUrl(request.Url))
            return BadRequest("Invalid Git URL.");

        try
        {
            // Cast API AudienceType to Core AudienceType
            var coreAudience = (AudienceType)(int)request.Audience;
            var job = await _ingestionManager.StartJobAsync(request.Url, true,
                coreAudience); // We might want ConnectionId here if we update the request model

            _telemetryService.TrackAgentActivity("System", $"Started ingestion job {job.Id} for {request.Url}",
                new Dictionary<string, object>
                {
                    ["JobId"] = job.Id,
                    ["Url"] = request.Url,
                    ["Audience"] = request.Audience.ToString()
                });

            return Ok(new { JobId = job.Id, Status = job.Status.ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ingestion start failed");
            return StatusCode(500, ex.Message);
        }
    }

    [HttpGet("ingestion/{jobId}")]
    public async Task<IActionResult> GetIngestionStatus(string jobId)
    {
        var job = await _ingestionManager.GetJobAsync(jobId);
        if (job == null) return NotFound();
        return Ok(job);
    }

    [HttpDelete("ingestion/{jobId}")]
    public async Task<IActionResult> CancelIngestion(string jobId)
    {
        await _ingestionManager.CancelJobAsync(jobId);
        return Ok();
    }

    [HttpGet("ingestions/active")]
    public async Task<IActionResult> ListActiveIngestions()
    {
        var jobs = await _ingestionManager.ListActiveJobsAsync();
        return Ok(jobs);
    }

    [HttpGet("repository-status")]
    public async Task<IActionResult> GetRepositoryStatus([FromQuery] string repoPath)
    {
        var structure = await _wikiRepo.GetStructureAsync(repoPath);
        if (structure == null)
            return Ok(new RepositoryStatusResponse
            {
                Exists = false,
                Ingested = false
            });

        // Load pages for accurate count
        var pages = await _wikiRepo.GetAllPagesAsync(repoPath);

        return Ok(new RepositoryStatusResponse
        {
            Exists = true,
            Ingested = true,
            Title = structure.Title,
            PageCount = pages.Count,
            SectionCount = structure.Sections?.Count ?? 0
        });
    }

    [HttpGet("navigation")]
    public async Task<IActionResult> GetNavigation([FromQuery] string repoPath)
    {
        _logger.LogInformation("GetNavigation called with repoPath: '{RepoPath}'", repoPath);

        var structure = await _wikiRepo.GetStructureAsync(repoPath);
        if (structure == null)
        {
            _logger.LogWarning("No structure found for repoPath: '{RepoPath}'", repoPath);
            return NoContent();
        }

        // Populate pages for full navigation tree
        structure.Pages = await _wikiRepo.GetAllPagesAsync(repoPath);

        return Ok(structure);
    }

    [HttpPost("generate-advanced")]
    public async Task<IActionResult> GenerateAdvancedWiki([FromBody] StructureRequest request)
    {
        // Store handlers to unsubscribe later
        Func<AgentMessage, Task>? statusSubscriber = null;
        Func<AgentMessage, Task>? delegationSubscriber = null;
        Func<AgentMessage, Task>? lifecycleSubscriber = null;
        string? clonedRepoPath = null;
        string? originalUrl = null;

        _telemetryService.TrackAgentActivity("System", $"GenerateAdvancedWiki requested for {request.RepoPath}");

        try
        {
            // Handle Git URL cloning
            if (GitHelper.IsGitUrl(request.RepoPath))
            {
                originalUrl = request.RepoPath;
                _logger.LogInformation("Git URL detected, cloning repository: {GitUrl}", request.RepoPath);

                try
                {
                    clonedRepoPath = await GitHelper.CloneRepositoryAsync(request.RepoPath);
                    _logger.LogInformation("Repository cloned to: {ClonedPath}", clonedRepoPath);

                    // Update request to use cloned path
                    request.RepoPath = clonedRepoPath;

                    // Persist the remote URL
                    await _wikiRepo.SetRepositoryRemoteUrlAsync(request.RepoPath, originalUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to clone Git repository: {GitUrl}", request.RepoPath);
                    return BadRequest(new { Error = $"Failed to clone repository: {ex.Message}" });
                }
            }

            if (!string.IsNullOrEmpty(request.ConnectionId))
            {
                // Subscribe to agent status updates
                statusSubscriber = message =>
                {
                    _ = _hubContext.Clients.Client(request.ConnectionId).SendAsync("ReceiveAgentStatus", message);
                    return Task.CompletedTask;
                };
                _messageBus.Subscribe(AgentMessageTypes.AgentStatus, statusSubscriber);

                // Subscribe to delegation events
                delegationSubscriber = message =>
                {
                    _ = _hubContext.Clients.Client(request.ConnectionId).SendAsync("ReceiveDelegationEvent", message);
                    return Task.CompletedTask;
                };
                _messageBus.Subscribe(AgentMessageTypes.TaskDelegated, delegationSubscriber);

                // Subscribe to task lifecycle events
                lifecycleSubscriber = message =>
                {
                    _ = _hubContext.Clients.Client(request.ConnectionId).SendAsync("ReceiveTaskLifecycle", message);
                    return Task.CompletedTask;
                };
                _messageBus.Subscribe(AgentMessageTypes.TaskStarted, lifecycleSubscriber);
                _messageBus.Subscribe(AgentMessageTypes.TaskCompleted, lifecycleSubscriber);
                _messageBus.Subscribe(AgentMessageTypes.TaskFailed, lifecycleSubscriber);
            }

            // Construct RepositoryInfo from request and filesystem
            var currentBranch = await GitHelper.GetCurrentBranch(request.RepoPath);

            var repoInfo = new RepositoryInfo
            {
                Name = Path.GetFileName(request.RepoPath),
                Language = request.Language,
                Url = originalUrl ?? string.Empty,
                Branch = currentBranch,
                // Estimation
                LinesOfCode = 0,
                ComponentCount = 0
            };

            IProgress<ProgressInfo>? progress = null;
            if (!string.IsNullOrEmpty(request.ConnectionId))
                progress = new Progress<ProgressInfo>(info =>
                {
                    _hubContext.Clients.Client(request.ConnectionId).SendAsync("ReceiveProgress", info);
                });

            var structure = await _orchestrator.GenerateAdvancedWikiAsync(
                request.RepoPath,
                repoInfo,
                progress);

            if (!request.SkipPersistence) await _wikiRepo.SaveStructureAsync(request.RepoPath, structure);

            // Populate pages for navigation
            structure.Pages = await _wikiRepo.GetAllPagesAsync(request.RepoPath);

            _telemetryService.TrackAgentActivity("System", $"GenerateAdvancedWiki completed for {request.RepoPath}");
            return Ok(structure);
        }
        finally
        {
            // Cleanup subscriptions to prevent memory leaks
            if (statusSubscriber != null)
                _messageBus.Unsubscribe(AgentMessageTypes.AgentStatus, statusSubscriber);

            if (delegationSubscriber != null)
                _messageBus.Unsubscribe(AgentMessageTypes.TaskDelegated, delegationSubscriber);

            if (lifecycleSubscriber != null)
            {
                _messageBus.Unsubscribe(AgentMessageTypes.TaskStarted, lifecycleSubscriber);
                _messageBus.Unsubscribe(AgentMessageTypes.TaskCompleted, lifecycleSubscriber);
                _messageBus.Unsubscribe(AgentMessageTypes.TaskFailed, lifecycleSubscriber);
            }
        }
    }

    // Helper Method
    private List<string> GetRepoFiles(string repoPath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "ls-files --cached --others --exclude-standard",
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true, // Capture stderr to avoid hanging if git complains
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(3000); // 3 sec timeout

                if (process.HasExited && process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                    return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Select(f => f.Trim())
                        .Where(f => !string.IsNullOrWhiteSpace(f))
                        .ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to use git ls-files for {Path}, falling back to manual discovery.",
                repoPath);
        }

        // Fallback or if not a git repo
        // Manual filter trying to mimic common gitignores (bin, obj, .git, node_modules)
        try
        {
            return Directory.GetFiles(repoPath, "*.*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(repoPath, f))
                .Where(f => !f.StartsWith(".") &&
                            // Common hidden/ignored folders
                            !f.Contains(Path.DirectorySeparatorChar + ".") &&
                            !f.Contains("/bin/") && !f.Contains("\\bin\\") &&
                            !f.Contains("/obj/") && !f.Contains("\\obj\\") &&
                            !f.Contains("/node_modules/") && !f.Contains("\\node_modules\\") &&
                            !f.Contains("/dist/") && !f.Contains("\\dist\\"))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning directory {Path}", repoPath);
            return new List<string>();
        }
    }
}