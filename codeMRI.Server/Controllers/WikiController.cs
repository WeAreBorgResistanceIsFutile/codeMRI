using codeMRI.Agents.Interfaces;
using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Server.Api;
using codeMRI.Server.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using AgentMessage = codeMRI.Agents.Models.AgentMessage;

namespace codeMRI.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WikiController : ControllerBase
{
    private readonly IWikiRepository _wikiRepo;
    private readonly IWikiGenerationService _wikiService;
    private readonly ICodeWikiOrchestrator _orchestrator;
    private readonly IHubContext<WikiHub> _hubContext;
    private readonly ILogger<WikiController> _logger;
    private readonly AgentMessageBus _messageBus;
    private readonly IAgentTelemetryService _telemetryService;

    public WikiController(
        IWikiGenerationService wikiService, 
        IWikiRepository wikiRepo, 
        ICodeWikiOrchestrator orchestrator,
        IHubContext<WikiHub> hubContext,
        ILogger<WikiController> logger,
        AgentMessageBus messageBus,
        IAgentTelemetryService telemetryService)
    {
        _wikiService = wikiService;
        _wikiRepo = wikiRepo;
        _orchestrator = orchestrator;
        _hubContext = hubContext;
        _logger = logger;
        _messageBus = messageBus;
        _telemetryService = telemetryService;
    }

    [HttpPost("structure")]
    public async Task<IActionResult> GenerateStructure([FromBody] StructureRequest request)
    {
        if (!request.ForceRegenerate)
        {
            var existing = await _wikiRepo.GetStructureAsync(request.RepoPath);
            if (existing != null) return Ok(existing);
        }
        else
        {
            // If regenerating structure, wipe the old one (including pages) to avoid orphans
            await _wikiRepo.DeleteStructureAsync(request.RepoPath);
        }

        // Simple file tree generation
        // Git-aware file tree generation
        var files = GetRepoFiles(request.RepoPath);
        var fileTree = "Files:\n" + string.Join("\n", files.Take(300)); // Limit for prompt context

        var structure = await _wikiService.GenerateStructureAsync(fileTree, request.ReadmeContent, request.Language);

        await _wikiRepo.SaveStructureAsync(request.RepoPath, structure);

        return Ok(structure);
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
        {
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
                    {
                        request.FileContents[relPath] = await System.IO.File.ReadAllTextAsync(fullPath);
                    }
                    else
                    {
                        _logger.LogWarning("File not found for wiki generation: {Path} (Repo: {Repo})", fullPath,
                            request.RepoPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading file for wiki generation: {Path}", fullPath);
                }
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

    [HttpPost("generate-advanced")]
    public async Task<IActionResult> GenerateAdvancedWiki([FromBody] StructureRequest request)
    {
        // Store handlers to unsubscribe later
        Func<AgentMessage, Task>? statusSubscriber = null;
        Func<AgentMessage, Task>? delegationSubscriber = null;
        Func<AgentMessage, Task>? lifecycleSubscriber = null;

        try
        {
            if (!string.IsNullOrEmpty(request.ConnectionId))
            {
                // Subscribe to agent status updates
                statusSubscriber = (message) =>
                {
                    _ = _hubContext.Clients.Client(request.ConnectionId).SendAsync("ReceiveAgentStatus", message);
                    return Task.CompletedTask;
                };
                _messageBus.Subscribe(AgentMessageTypes.AgentStatus, statusSubscriber);

                // Subscribe to delegation events
                delegationSubscriber = (message) =>
                {
                    _ = _hubContext.Clients.Client(request.ConnectionId).SendAsync("ReceiveDelegationEvent", message);
                    return Task.CompletedTask;
                };
                _messageBus.Subscribe(AgentMessageTypes.TaskDelegated, delegationSubscriber);

                // Subscribe to task lifecycle events
                lifecycleSubscriber = (message) =>
                {
                    _ = _hubContext.Clients.Client(request.ConnectionId).SendAsync("ReceiveTaskLifecycle", message);
                    return Task.CompletedTask;
                };
                _messageBus.Subscribe(AgentMessageTypes.TaskStarted, lifecycleSubscriber);
                _messageBus.Subscribe(AgentMessageTypes.TaskCompleted, lifecycleSubscriber);
                _messageBus.Subscribe(AgentMessageTypes.TaskFailed, lifecycleSubscriber);
            }

            // Construct RepositoryInfo from request and filesystem
            var repoInfo = new RepositoryInfo
            {
                Name = Path.GetFileName(request.RepoPath),
                Language = request.Language,
                // Estimation
                LinesOfCode = 0,
                ComponentCount = 0
            };

            IProgress<codeMRI.Core.Models.ProgressInfo>? progress = null;
            if (!string.IsNullOrEmpty(request.ConnectionId))
            {
                progress = new Progress<codeMRI.Core.Models.ProgressInfo>(info =>
                {
                    _hubContext.Clients.Client(request.ConnectionId).SendAsync("ReceiveProgress", info);
                });
            }

            var structure = await _orchestrator.GenerateAdvancedWikiAsync(
                request.RepoPath,
                repoInfo,
                progress);

            if (!request.SkipPersistence)
            {
                await _wikiRepo.SaveStructureAsync(request.RepoPath, structure);
            }

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
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = "ls-files --cached --others --exclude-standard",
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true, // Capture stderr to avoid hanging if git complains
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(3000); // 3 sec timeout

                if (process.HasExited && process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(f => f.Trim())
                                 .Where(f => !string.IsNullOrWhiteSpace(f))
                                 .ToList();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to use git ls-files for {Path}, falling back to manual discovery.", repoPath);
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