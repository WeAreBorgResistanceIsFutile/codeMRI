using codeMRI.Core.Services;
using codeMRI.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace codeMRI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WikiController : ControllerBase
{
    private readonly WikiGenerationService _wikiService;

    public WikiController(WikiGenerationService wikiService)
    {
        _wikiService = wikiService;
    }

    [HttpPost("structure")]
    public async Task<IActionResult> GenerateStructure([FromBody] StructureRequest request)
    {
        // Simple file tree generation
        string fileTree = "Files:\n" + string.Join("\n", Directory.GetFiles(request.RepoPath, "*.*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(request.RepoPath, f))
            .Where(f => !f.StartsWith(".") && !f.Contains("bin/") && !f.Contains("obj/"))
            .Take(200)); // Limit for prompt context

        var structure = await _wikiService.GenerateStructureAsync(fileTree, request.ReadmeContent, request.Language);
        return Ok(structure);
    }

    [HttpPost("page")]
    public async Task<IActionResult> GeneratePage([FromBody] PageGenerationRequest request)
    {
        if (request.FileContents == null)
        {
            request.FileContents = new Dictionary<string, string>();
        }

        // If contents are empty but RepoPath is provided, try to read them
        if (request.FileContents.Count == 0 && !string.IsNullOrEmpty(request.RepoPath))
        {
            foreach (var relPath in request.FilePaths)
            {
                var fullPath = Path.Combine(request.RepoPath, relPath);
                if (System.IO.File.Exists(fullPath))
                {
                    request.FileContents[relPath] = await System.IO.File.ReadAllTextAsync(fullPath);
                }
            }
        }

        var page = await _wikiService.GeneratePageAsync(request.Title, request.FilePaths, request.FileContents, request.Language);
        return Ok(page);
    }
}
