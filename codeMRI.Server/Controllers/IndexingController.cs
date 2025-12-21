using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace codeMRI.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IndexingController : ControllerBase
{
    private readonly IDocumentIndexer _documentIndexer;
    private readonly IWikiRepository _wikiRepository;
    private readonly IHierarchicalDecompositionService _decompositionService;
    private readonly IEnhancedDependencyGraphService _graphService;
    private readonly ILogger<IndexingController> _logger;

    public IndexingController(
        IDocumentIndexer documentIndexer,
        IWikiRepository wikiRepository,
        IHierarchicalDecompositionService decompositionService,
        IEnhancedDependencyGraphService graphService,
        ILogger<IndexingController> logger)
    {
        _documentIndexer = documentIndexer;
        _wikiRepository = wikiRepository;
        _decompositionService = decompositionService;
        _graphService = graphService;
        _logger = logger;
    }

    [HttpPost("documentation/reindex")]
    public async Task<IActionResult> ReindexDocumentation([FromBody] string repoPath)
    {
        try
        {
            _logger.LogInformation("Manually triggering documentation re-indexing for {RepoPath}", repoPath);
            var structure = await _wikiRepository.GetStructureAsync(repoPath);
            if (structure == null) return NotFound("Wiki structure not found for this repository.");

            await _documentIndexer.DeleteIndexAsync(repoPath);
            await _documentIndexer.IndexDocumentationAsync(repoPath, structure);

            return Ok(new { Message = "Documentation indexing complete" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to re-index documentation");
            return StatusCode(500, ex.Message);
        }
    }

    [HttpPost("code/reindex")]
    public async Task<IActionResult> ReindexCode([FromBody] string repoPath)
    {
        try
        {
            _logger.LogInformation("Manually triggering codebase re-indexing for {RepoPath}", repoPath);
            
            var moduleTree = await _decompositionService.DecomposeHierarchicallyAsync(repoPath);
            var components = await _graphService.GetComponentsAsync(repoPath);
            var graph = await _graphService.BuildGraphAsync(components);

            await _documentIndexer.DeleteIndexAsync(repoPath);
            await _documentIndexer.IndexCodebaseAsync(repoPath, graph);

            return Ok(new { Message = "Codebase indexing complete" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to re-index codebase");
            return StatusCode(500, ex.Message);
        }
    }

    [HttpDelete("{repoPath}")]
    public async Task<IActionResult> DeleteIndex(string repoPath)
    {
        try
        {
            await _documentIndexer.DeleteIndexAsync(repoPath);
            return Ok(new { Message = "Index deleted" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }
}
