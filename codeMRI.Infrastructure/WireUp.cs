using codeMRI.Agents.Agents;
using codeMRI.Agents.Interfaces;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Services;
using codeMRI.Visualization.Services;
using Microsoft.Extensions.DependencyInjection;

namespace codeMRI.Infrastructure;

public class WireUp
{
    public static void Registered(IServiceCollection services)
    {
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
        services.AddScoped<RAGService>();
        services.AddScoped<IWikiGenerationService, WikiGenerationService>(sp =>
        {
            var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<codeMRI.Infrastructure.Configuration.OllamaSettings>>().Value;
            return new WikiGenerationService(
                sp.GetRequiredService<ILLMClient>(),
                sp.GetRequiredService<IEmbedder>(),
                sp.GetRequiredService<IVectorDatabase>(),
                sp.GetRequiredService<IDiagramGenerator>(),
                sp.GetRequiredService<IEnhancedDependencyGraphService>(),
                sp.GetRequiredService<IDocumentationSynthesisService>(),
                sp.GetRequiredService<IReferenceManagementService>(),
                settings.DocumentationModel);
        });
        services.AddScoped<IHierarchicalDecompositionService, HierarchicalDecompositionService>();
        services.AddScoped<IDocumentationSynthesisService, DocumentationSynthesisService>();
        services.AddSingleton<IReferenceManagementService, ReferenceManagementService>();
        services.AddScoped<IDocumentationJudgeService, DocumentationJudgeService>();
    }
}