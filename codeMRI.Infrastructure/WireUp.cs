using codeMRI.Agents.Agents;
using codeMRI.Agents.Configuration;
using codeMRI.Agents.Interfaces;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Models.Configuration;
using codeMRI.Core.Services;
using codeMRI.Infrastructure.Configuration;
using codeMRI.Infrastructure.Services;
using codeMRI.Core.Services.MessageComposition;
using codeMRI.Core.Services.Decorators;
using codeMRI.Core.Services.MessageComposition.Strategies;
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
        // LLM Services
        services.AddSingleton<OllamaLLMService>();
        
        // Debug and Context Services
        services.AddSingleton<ILLMInvocationContext, LLMInvocationContext>();
        services.AddSingleton<IDebugSnapshotService, DebugSnapshotService>();

        // Configuration
        services.Configure<RetrySettings>(configuration.GetSection("Retry"));

        services.AddSingleton<ILLMClient>(sp => {
            var inner = sp.GetRequiredService<OllamaLLMService>();
            
            // 1. Wrap with Retry Logic
            var resilient = new ResilientLLMClientDecorator(
                inner,
                sp.GetRequiredService<ILogger<ResilientLLMClientDecorator>>(),
                sp.GetRequiredService<IOptions<RetrySettings>>());

            // 2. Wrap with Debug Snapshot (captures final failure after retries)
            return new DebugSnapshotLLMClientDecorator(
                resilient,
                sp.GetRequiredService<IDebugSnapshotService>(),
                sp.GetRequiredService<ILLMInvocationContext>(),
                sp.GetRequiredService<ILogger<DebugSnapshotLLMClientDecorator>>());
        });
        services.AddSingleton<ILLMValidator>(sp => sp.GetRequiredService<OllamaLLMService>());

        // Message Composition Strategies - Composition
        services.AddSingleton<IMessageCompositionStrategy, SimpleMessageStrategy>();
        services.AddSingleton<IMessageCompositionStrategy, RAGStrategy>();
        services.AddSingleton<IMessageCompositionStrategy, MapReduceStrategy>();

        // Message Composition Strategies - Iterative Execution
        services.AddSingleton<IIterativeExecutionStrategy, ChunkingMessageStrategy>();
        services.AddSingleton<IIterativeExecutionStrategy, MultiPassReductionStrategy>();

        // Message Composition Orchestrator and Facade
        services.AddSingleton<IMessageCompositionOrchestrator, MessageCompositionOrchestrator>();
        services.AddSingleton<ILLMServiceFacade, LLMServiceFacade>();

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
        services.Configure<EmbeddingSettings>(configuration.GetSection("Embedding"));
        
        services.AddSingleton<OllamaEmbeddingService>();
        services.AddSingleton<IEmbeddingService>(sp =>
        {
            var inner = sp.GetRequiredService<OllamaEmbeddingService>();
            return new DebugSnapshotEmbeddingServiceDecorator(
                inner,
                sp.GetRequiredService<IDebugSnapshotService>(),
                sp.GetRequiredService<ILLMInvocationContext>(),
                sp.GetRequiredService<ILogger<DebugSnapshotEmbeddingServiceDecorator>>());
        });
        services.AddSingleton<IVectorStoreService, QdrantVectorStoreService>();
        services.AddScoped<IDocumentIndexer, DocumentationIndexer>();
        services.AddSingleton<IVectorStoreInitializationService, VectorStoreInitializationService>();
        
        // Register chunkers with configured settings from Embedding section
        services.AddSingleton<SemanticDocumentChunker>(sp =>
        {
            var embeddingSettings = sp.GetRequiredService<IOptions<EmbeddingSettings>>().Value;
            return new SemanticDocumentChunker(
                maxTokens: embeddingSettings.Chunking.MaxTokens,
                overlapTokens: embeddingSettings.Chunking.OverlapTokens);
        });
        
        services.AddSingleton<CodeChunker>(sp =>
        {
            var embeddingSettings = sp.GetRequiredService<IOptions<EmbeddingSettings>>().Value;
            return new CodeChunker(
                maxTokens: embeddingSettings.Chunking.MaxTokens);
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
        services.AddScoped<IWikiGenerationService>(sp =>
        {
            var routingSettings = sp.GetRequiredService<IOptions<ModelRoutingSettings>>().Value;
            var docModel = routingSettings.DocumentationModel;
            if (string.IsNullOrWhiteSpace(docModel))
                throw new InvalidOperationException("DocumentationModel is not configured in ModelRoutingSettings.");

            var inner = new WikiGenerationService(
                sp.GetRequiredService<ILLMServiceFacade>(),
                sp.GetRequiredService<IDiagramGenerator>(),
                sp.GetRequiredService<IEnhancedDependencyGraphService>(),
                sp.GetRequiredService<IDocumentationSynthesisService>(),
                sp.GetRequiredService<IReferenceManagementService>(),
                sp.GetRequiredService<ILogger<WikiGenerationService>>(),
                sp.GetRequiredService<IOptions<CodeWikiOptions>>(),
                docModel,
                sp.GetService<IModelRoutingService>(),
                sp.GetService<IMultiModelOrchestrationService>());

            return new ResumableWikiGenerationDecorator(
                inner,
                sp.GetRequiredService<IWikiRepository>(),
                sp.GetRequiredService<ILLMInvocationContext>(),
                sp.GetRequiredService<ILogger<ResumableWikiGenerationDecorator>>());
        });

        services.AddScoped<HierarchicalDecompositionService>();
        services.AddScoped<IHierarchicalDecompositionService>(sp => 
            new ResumableDecompositionDecorator(
                sp.GetRequiredService<HierarchicalDecompositionService>(),
                sp.GetRequiredService<IWikiRepository>(),
                sp.GetRequiredService<ILLMInvocationContext>(),
                sp.GetRequiredService<ILogger<ResumableDecompositionDecorator>>()));

        services.AddScoped<IDocumentationSynthesisService, DocumentationSynthesisService>();
        services.AddScoped<IHierarchicalSummaryService, HierarchicalSummaryService>();
        services.AddScoped<IDocumentationRevisionService, DocumentationRevisionService>();
        services.AddSingleton<IReferenceManagementService, ReferenceManagementService>();
        services.AddScoped<IEvaluationPromptBuilder, DefaultEvaluationPromptBuilder>();
        services.AddScoped<RagEvaluationPromptBuilder>();
        services.AddScoped<DocumentationJudgeService>();
        services.AddScoped<IDocumentationJudgeService>(sp => sp.GetRequiredService<DocumentationJudgeService>());

        services.AddScoped<RubricGenerationService>();
        services.AddScoped<IRubricGenerationService>(sp => 
            new ResumableRubricDecorator(
                sp.GetRequiredService<RubricGenerationService>(),
                sp.GetRequiredService<IWikiRepository>(),
                sp.GetRequiredService<ILLMInvocationContext>(),
                sp.GetRequiredService<ILogger<ResumableRubricDecorator>>()));

        services.AddScoped<IProgressService, ProgressService>();
        services.AddScoped<INavigationStructureService, NavigationStructureService>();

        // Multi-Model Services with configuration injection
        services.AddSingleton<IModelRoutingService>(sp =>
        {
            var routingSettings = sp.GetRequiredService<IOptions<ModelRoutingSettings>>().Value;
            var config = new ModelRoutingConfig
            {
                EnableModelRouting = routingSettings.EnableModelRouting,
                CodeAnalysisModel = routingSettings.CodeAnalysisModel,
                NaturalLanguageModel = routingSettings.NaturalLanguageModel,
                SynthesisJudgeModel = routingSettings.SynthesisJudgeModel
            };
            return new ModelRoutingService(config, sp.GetRequiredService<ILogger<ModelRoutingService>>());
        });

        services.AddScoped<IMultiModelOrchestrationService>(sp =>
        {
            var routingSettings = sp.GetRequiredService<IOptions<ModelRoutingSettings>>().Value;
            var config = new EnsembleConfig
            {
                EnableEnsembleGeneration = routingSettings.EnableEnsembleGeneration,
                EnsembleModels = routingSettings.EnsembleModels,
                MinimumAgreementThreshold = routingSettings.MinimumAgreementThreshold
            };
            return new MultiModelOrchestrationService(
                sp.GetRequiredService<ILLMServiceFacade>(),
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
            var routingSettings = sp.GetRequiredService<IOptions<ModelRoutingSettings>>().Value;
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
                sp.GetRequiredService<ILLMInvocationContext>(),
                sp.GetRequiredService<ILogger<CodeWikiOrchestrator>>(),
                // Use configured Judge model from ModelRoutingSettings
                (routingSettings.JudgeModels.Any() ? routingSettings.JudgeModels.First() : routingSettings.DocumentationModel) ?? "llama3");
        });
    }
}