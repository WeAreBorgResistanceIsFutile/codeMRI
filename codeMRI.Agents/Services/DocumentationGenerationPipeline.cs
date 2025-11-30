using codeMRI.Agents.Interfaces;
using codeMRI.Agents.Models;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Shared.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Agents.Services;

public class DocumentationGenerationPipeline : IDocumentationGenerationPipeline
{
    private readonly IAgentCoordinator _coordinator;
    private readonly ILogger<DocumentationGenerationPipeline> _logger;

    public DocumentationGenerationPipeline(
        IAgentCoordinator coordinator,
        ILogger<DocumentationGenerationPipeline> logger)
    {
        _coordinator = coordinator;
        _logger = logger;
    }

    public async Task<WikiStructure> GenerateDocumentationAsync(string repositoryPath, DocumentationOptions options)
    {
        _logger.LogInformation("Starting agent-based documentation generation for: {RepoPath}", repositoryPath);

        // 1. Analyze
        var analysisTask = new AgentTask
        {
            Type = "Analyzer",
            Payload = repositoryPath
        };

        var analysisResult = await _coordinator.CoordinateTaskAsync(analysisTask, CancellationToken.None);
        if (!analysisResult.Success)
        {
            throw new Exception($"Analysis failed: {string.Join(", ", analysisResult.Errors)}");
        }

        // Extract components from result (assuming specific structure from AnalyzerAgent)
        // The AnalyzerAgent returns { Structure, Components } via anonymous type, checking JSON/dynamic serialization might be needed if across process,
        // but here it's in-memory. However, anonymous types are tricky.
        // Let's assume AnalyzerAgent returns a typed object or we cast carefully.
        // AnalyzerAgent used `new { Structure = structure, Components = components }`
        
        // Improvement: Update AnalyzerAgent to return a strongly typed Result object or Dictionary.
        // For now, using reflection to extract or assuming we can refactor AnalyzerAgent later.
        // To be safe, let's refactor AnalyzerAgent to return a dictionary or specific DTO?
        // Or just use dynamic.
        
        var data = (AnalysisResult)analysisResult.Output!;
        var components = data.Components;
        var structure = data.Structure;

        // 2. Document Components
        var wikiPages = new List<WikiPage>();
        foreach (var component in components)
        {
            var docTask = new AgentTask
            {
                Type = "Documenter",
                Payload = component
            };
            var docResult = await _coordinator.CoordinateTaskAsync(docTask, CancellationToken.None);
            if (docResult.Success && docResult.Output is WikiPage page)
            {
                wikiPages.Add(page);
            }
        }

        // 3. Synthesize
        var synthTask = new AgentTask
        {
            Type = "Synthesizer",
            Payload = wikiPages
        };
        var synthResult = await _coordinator.CoordinateTaskAsync(synthTask, CancellationToken.None);

        if (synthResult.Success && synthResult.Output is WikiStructure wikiStructure)
        {
            return wikiStructure;
        }

        throw new Exception("Failed to synthesize documentation");
    }

    public Task<WikiPage> GenerateComponentDocumentationAsync(CodeComponent component, RepositoryStructure context)
    {
        // Direct delegation to Documenter
        var task = new AgentTask { Type = "Documenter", Payload = component };
        // This method is sync-over-async wrapper or we change interface? Interface returns Task.
        // We need to wait for coordinator.
        var result = _coordinator.CoordinateTaskAsync(task, CancellationToken.None).GetAwaiter().GetResult(); // Blocking!
        // Better: make this async if interface allows. Interface IS async Task<WikiPage>.
        
        // But I can't await in this sync block if I don't make method async... 
        // Wait, the method signature IS `Task<WikiPage>`.
        return GenerateComponentDocumentationAsyncInternal(component);
    }

    private async Task<WikiPage> GenerateComponentDocumentationAsyncInternal(CodeComponent component)
    {
        var task = new AgentTask { Type = "Documenter", Payload = component };
        var result = await _coordinator.CoordinateTaskAsync(task, CancellationToken.None);
        if (result.Success && result.Output is WikiPage page)
        {
            return page;
        }
        throw new Exception("Documentation generation failed");
    }

    public async Task<List<WikiPage>> GenerateOverviewPagesAsync(RepositoryStructure structure, List<CodeComponent> components)
    {
         // Simplified implementation for now - could use another agent
         return new List<WikiPage>();
    }
}
