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
using Microsoft.Extensions.Configuration;

namespace codeMRI.Infrastructure;

public class WireUp
{
    public static void Registered(IServiceCollection services, IConfiguration configuration)
    {
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

        // Configure DelegationOptions (can be overridden via appsettings)
        services.Configure<DelegationOptions>(options =>
        {
            // MaxTokensPerModule will be derived from LLM context size if not set
            options.MaxComplexityScore = 100;
            options.MaxDelegationDepth = 3;
            options.SemanticDiversityThreshold = 0.6;
            options.EnableDelegation = true;
            options.ContextUtilizationRatio = 0.8;
        });

        // Configure VectorStore and Embedding
        services.Configure<VectorStoreSettings>(configuration.GetSection("VectorStore"));
        services.AddSingleton<IEmbeddingService, OllamaEmbeddingService>();
        services.AddSingleton<IVectorStoreService, QdrantVectorStoreService>();
        services.AddScoped<IDocumentIndexer, DocumentationIndexer>();
        services.AddSingleton<SemanticDocumentChunker>();
        services.AddSingleton<CodeChunker>();

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
                throw new InvalidOperationException("DocumentationModel is not configured in OllamaSettings.");

            return new WikiGenerationService(
                sp.GetRequiredService<ILLMClient>(),
                sp.GetRequiredService<IDiagramGenerator>(),
                sp.GetRequiredService<IEnhancedDependencyGraphService>(),
                sp.GetRequiredService<IDocumentationSynthesisService>(),
                sp.GetRequiredService<IReferenceManagementService>(),
                sp.GetRequiredService<ILogger<WikiGenerationService>>(),
                sp.GetRequiredService<IOptions<CodeWikiOptions>>(),
                docModel,
                sp.GetService<IModelRoutingService>(),
                sp.GetService<IMultiModelOrchestrationService>());
        });
        services.AddScoped<IHierarchicalDecompositionService, HierarchicalDecompositionService>();
        services.AddScoped<IDocumentationSynthesisService, DocumentationSynthesisService>();
        services.AddScoped<IHierarchicalSummaryService, HierarchicalSummaryService>();
        services.AddScoped<IDocumentationRevisionService, DocumentationRevisionService>();
        services.AddSingleton<IReferenceManagementService, ReferenceManagementService>();
        services.AddScoped<IEvaluationPromptBuilder, DefaultEvaluationPromptBuilder>();
        services.AddScoped<RagEvaluationPromptBuilder>();
        services.AddScoped<IDocumentationJudgeService, DocumentationJudgeService>();
        services.AddScoped<IRubricGenerationService, RubricGenerationService>();
        services.AddScoped<IProgressService, ProgressService>();
        services.AddScoped<INavigationStructureService, NavigationStructureService>();

        // Multi-Model Services with configuration injection
        services.AddSingleton<IModelRoutingService>(sp =>
        {
            var ollamaSettings = sp.GetRequiredService<IOptions<OllamaSettings>>().Value;
            var config = new ModelRoutingConfig
            {
                EnableModelRouting = ollamaSettings.ModelRouting.EnableModelRouting,
                CodeAnalysisModel = ollamaSettings.ModelRouting.CodeAnalysisModel,
                NaturalLanguageModel = ollamaSettings.ModelRouting.NaturalLanguageModel,
                SynthesisJudgeModel = ollamaSettings.ModelRouting.SynthesisJudgeModel
            };
            return new ModelRoutingService(config, sp.GetRequiredService<ILogger<ModelRoutingService>>());
        });

        services.AddScoped<IMultiModelOrchestrationService>(sp =>
        {
            var ollamaSettings = sp.GetRequiredService<IOptions<OllamaSettings>>().Value;
            var config = new EnsembleConfig
            {
                EnableEnsembleGeneration = ollamaSettings.ModelRouting.EnableEnsembleGeneration,
                EnsembleModels = ollamaSettings.ModelRouting.EnsembleModels,
                MinimumAgreementThreshold = ollamaSettings.ModelRouting.MinimumAgreementThreshold
            };
            return new MultiModelOrchestrationService(
                sp.GetRequiredService<ILLMClient>(),
                sp.GetRequiredService<IModelRoutingService>(),
                config,
                sp.GetRequiredService<ILogger<MultiModelOrchestrationService>>(),
                sp.GetRequiredService<IOptions<CodeWikiOptions>>());
        });

        // Dynamic Delegation Service with LLM context size from configuration
        services.AddScoped<IDelegationService, DynamicDelegationService>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<OllamaSettings>>().Value;
            return new DynamicDelegationService(
                sp.GetRequiredService<ILogger<DynamicDelegationService>>(),
                sp.GetRequiredService<IAgentTelemetryService>(),
                sp.GetRequiredService<IOptions<DelegationOptions>>(),
                settings.ContextSize);
        });
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
                sp.GetRequiredService<IDocumentationRevisionService>(),
                sp.GetRequiredService<IWikiRepository>(),
                sp.GetRequiredService<IProgressService>(),
                sp.GetRequiredService<IAgentTelemetryService>(),
                sp.GetRequiredService<IDelegationService>(),
                sp.GetRequiredService<IDocumentIndexer>(),
                sp.GetRequiredService<INavigationStructureService>(),
                sp.GetRequiredService<IOptions<CodeWikiOptions>>(),
                sp.GetRequiredService<ILogger<CodeWikiOrchestrator>>(),
                // Use configured Judge model, or fall back to DocumentationModel, then to "llama3"
                (settings.JudgeModels.Any() ? settings.JudgeModels.First() : settings.DocumentationModel) ?? "llama3");
        });
    }
}