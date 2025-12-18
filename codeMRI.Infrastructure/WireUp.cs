using codeMRI.Agents.Agents;
using codeMRI.Agents.Configuration;
using codeMRI.Agents.Interfaces;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using codeMRI.Infrastructure.Configuration;
using codeMRI.Infrastructure.Services;
using codeMRI.Visualization.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace codeMRI.Infrastructure;

public class WireUp
{
    public static void Registered(IServiceCollection services)
    {
        // Configure AgentSettings with default values
        services.Configure<AgentSettings>(options =>
        {
            options.EnableDelegation = true;
            options.MaxRecursionDepth = 3;
            options.ComplexityThresholds = new Dictionary<string, int>
            {
                { "ComplexityScore", 8 },
                { "LineCount", 500 }
            };
        });
        
        services.AddSingleton<ICSharpParser, RoslynCSharpParser>();
        services.AddSingleton<AgentMessageBus>();
        services.AddSingleton<IAgentTelemetryService, AgentTelemetryService>();
        services.AddScoped<DelegationService>();
        services.AddScoped<IAgent, AnalyzerAgent>();
        services.AddScoped<IAgent, DocumenterAgent>();
        services.AddScoped<IAgent, SynthesizerAgent>();
        services.AddScoped<IAgent, ValidatorAgent>();
        services.AddScoped<IAgentCoordinator, AgentCoordinator>();
        services.AddScoped<IComponentIdentificationService, ComponentIdentificationService>();
        services.AddScoped<IDocumentationGenerationPipeline, DocumentationGenerationPipeline>();

        services.AddScoped<IDiagramGenerator, DiagramGeneratorService>();
        services.AddScoped<IVisualSynthesisService, VisualSynthesisService>();

        services.AddScoped<IArchitecturalPatternService, ArchitecturalPatternService>();
        services.AddScoped<IEnhancedDependencyGraphService, EnhancedDependencyGraphService>();
        services.AddScoped<IWikiGenerationService, WikiGenerationService>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<OllamaSettings>>().Value;
            var docModel = settings.DocumentationModel;
            if (string.IsNullOrWhiteSpace(docModel))
            {
                throw new InvalidOperationException("DocumentationModel is not configured in OllamaSettings.");
            }
            
            return new WikiGenerationService(
                sp.GetRequiredService<ILLMClient>(),
                sp.GetRequiredService<IDiagramGenerator>(),
                sp.GetRequiredService<IEnhancedDependencyGraphService>(),
                sp.GetRequiredService<IDocumentationSynthesisService>(),
                sp.GetRequiredService<IReferenceManagementService>(),
                sp.GetRequiredService<ILogger<WikiGenerationService>>(),
                docModel);
        });
        services.AddScoped<IHierarchicalDecompositionService, HierarchicalDecompositionService>();
        services.AddScoped<IDocumentationSynthesisService, DocumentationSynthesisService>();
        services.AddSingleton<IReferenceManagementService, ReferenceManagementService>();
        services.AddScoped<IEvaluationPromptBuilder, DefaultEvaluationPromptBuilder>();
        services.AddScoped<IDocumentationJudgeService, DocumentationJudgeService>();
        services.AddScoped<IRubricGenerationService, RubricGenerationService>();
        services.AddScoped<IProgressService, ProgressService>();
        services.AddScoped<ICodeWikiOrchestrator, CodeWikiOrchestrator>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<OllamaSettings>>().Value;
            return new CodeWikiOrchestrator(
                sp.GetRequiredService<IHierarchicalDecompositionService>(),
                sp.GetRequiredService<IEnhancedDependencyGraphService>(),
                sp.GetRequiredService<IRubricGenerationService>(),
                sp.GetRequiredService<IDocumentationJudgeService>(),
                sp.GetRequiredService<IWikiGenerationService>(),
                sp.GetRequiredService<IDocumentationSynthesisService>(),
                sp.GetRequiredService<IWikiRepository>(),
                sp.GetRequiredService<IProgressService>(), // Added
                sp.GetRequiredService<IAgentTelemetryService>(),
                sp.GetRequiredService<IOptions<CodeWikiOptions>>(),
                sp.GetRequiredService<ILogger<CodeWikiOrchestrator>>(),
                // Use configured Judge model, or fall back to DocumentationModel, then to "llama3"
                (settings.JudgeModels.Any() ? settings.JudgeModels.First() : settings.DocumentationModel) ?? "llama3");
        });
    }
}