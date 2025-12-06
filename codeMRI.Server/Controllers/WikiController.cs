using codeMRI.Core.Interfaces;
using codeMRI.Server.Api;
using Microsoft.AspNetCore.Mvc;

namespace codeMRI.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WikiController : ControllerBase
{
    private readonly IWikiRepository _wikiRepo;
    private readonly IWikiGenerationService _wikiService;
    private readonly ICodeWikiOrchestrator _orchestrator;

    public WikiController(
        IWikiGenerationService wikiService, 
        IWikiRepository wikiRepo, 
        ICodeWikiOrchestrator orchestrator)
    {
        _wikiService = wikiService;
        _wikiRepo = wikiRepo;
        _orchestrator = orchestrator;
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
        var fileTree = "Files:\n" + string.Join("\n",
            Directory.GetFiles(request.RepoPath, "*.*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(request.RepoPath, f))
                .Where(f => !f.StartsWith(".") && !f.Contains("bin/") && !f.Contains("obj/"))
                .Take(200)); // Limit for prompt context

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

        // If contents are empty but RepoPath is provided, try to read them
        if (request.FileContents.Count == 0 && !string.IsNullOrEmpty(request.RepoPath))
            foreach (var relPath in request.FilePaths)
            {
                var fullPath = Path.Combine(request.RepoPath, relPath);
                if (System.IO.File.Exists(fullPath))
                    request.FileContents[relPath] = await System.IO.File.ReadAllTextAsync(fullPath);
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
        // Construct RepositoryInfo from request and filesystem
        var repoInfo = new RepositoryInfo
        {
            Name = Path.GetFileName(request.RepoPath),
            Language = request.Language,
            // Estimation
            LinesOfCode = 0, 
            ComponentCount = 0
        };

        var structure = await _orchestrator.GenerateAdvancedWikiAsync(
            request.RepoPath, 
            repoInfo);

        await _wikiRepo.SaveStructureAsync(request.RepoPath, structure);
        return Ok(structure);
    }
}