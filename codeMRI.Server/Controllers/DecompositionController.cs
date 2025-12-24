using System.Text.Json;
using System.Text.Json.Serialization;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace codeMRI.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DecompositionController : ControllerBase
{
    private readonly IHierarchicalDecompositionService _decompositionService;
    private readonly ILogger<DecompositionController> _logger;

    public DecompositionController(
        IHierarchicalDecompositionService decompositionService,
        ILogger<DecompositionController> logger)
    {
        _decompositionService = decompositionService;
        _logger = logger;
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> AnalyzeRepository([FromBody] DecompositionRequest request)
    {
        try
        {
            _logger.LogInformation("Starting hierarchical decomposition for {RepoPath}", request.RepoPath);

            var moduleTree = await _decompositionService.DecomposeHierarchicallyAsync(
                request.RepoPath,
                null!,
                HttpContext.RequestAborted
            );

            var response = new DecompositionResponse
            {
                ModuleTree = moduleTree,
                Summary = new
                {
                    TotalModules = moduleTree.Nodes.Count,
                    LeafModules = moduleTree.GetAllLeaves().Count,
                    MaxDepth = moduleTree.Nodes.Values.Max(n => n.Level)
                }
            };

            // Use custom JSON options to handle circular references
            var options = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            return new JsonResult(response, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decompose repository");
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}

public class DecompositionRequest
{
    public string RepoPath { get; set; } = string.Empty;
}

public class DecompositionResponse
{
    public ModuleTree ModuleTree { get; set; } = null!;
    public object? Summary { get; set; }
}
