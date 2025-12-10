using codeMRI.Agents.Agents;
using codeMRI.Agents.Interfaces;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Services;
using codeMRI.Infrastructure.Services;
using codeMRI.Visualization.Services;
using Microsoft.Extensions.DependencyInjection;

namespace codeMRI.Infrastructure;

public class WireUp
{
    public static void Registered(IServiceCollection services)
    {
        services.AddSingleton<ICSharpParser, RoslynCSharpParser>();
        services.AddSingleton<AgentMessageBus>();
        services.AddSingleton<DelegationService>();
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
            var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<codeMRI.Infrastructure.Configuration.OllamaSettings>>().Value;
            return new WikiGenerationService(
                sp.GetRequiredService<ILLMClient>(),
                sp.GetRequiredService<IDiagramGenerator>(),
                sp.GetRequiredService<IEnhancedDependencyGraphService>(),
                sp.GetRequiredService<IDocumentationSynthesisService>(),
                sp.GetRequiredService<IReferenceManagementService>(),
                settings.DocumentationModel);
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
            var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<codeMRI.Infrastructure.Configuration.OllamaSettings>>().Value;
            return new CodeWikiOrchestrator(
                sp.GetRequiredService<IHierarchicalDecompositionService>(),
                sp.GetRequiredService<IRubricGenerationService>(),
                sp.GetRequiredService<IDocumentationJudgeService>(),
                sp.GetRequiredService<IWikiGenerationService>(),
                sp.GetRequiredService<IDocumentationSynthesisService>(),
                sp.GetRequiredService<IWikiRepository>(),
                sp.GetRequiredService<IProgressService>(), // Added
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CodeWikiOrchestrator>>(),
                settings.JudgeModels.FirstOrDefault() ?? "llama3");
        });
    }
}