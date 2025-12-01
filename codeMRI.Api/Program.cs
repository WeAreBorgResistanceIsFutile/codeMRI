using codeMRI.Core.Interfaces;
using codeMRI.Core.Services;
using codeMRI.Infrastructure.Configuration;
using codeMRI.Infrastructure.Services;
using codeMRI.Agents.Services;
using codeMRI.Agents.Interfaces;
using codeMRI.Agents.Agents;
using codeMRI.Visualization.Interfaces;
using codeMRI.Visualization.Services;
using Microsoft.Extensions.Options;
using Qdrant.Client;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configuration
builder.Services.Configure<OllamaSettings>(builder.Configuration.GetSection("Ollama"));
builder.Services.Configure<QdrantSettings>(builder.Configuration.GetSection("Qdrant"));
builder.Services.Configure<ASTServiceSettings>(builder.Configuration.GetSection("ASTService"));

// Infrastructure
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IEmbedder, OllamaEmbedderService>();
builder.Services.AddSingleton<ILLMClient, OllamaLLMService>();
builder.Services.AddSingleton<IDocumentProcessor, TextSplitterService>();
builder.Services.AddSingleton<IASTServiceClient, ASTServiceClient>();

// Register QdrantClient
builder.Services.AddSingleton<IQdrantClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<QdrantSettings>>().Value;
    return new QdrantClient(settings.Host, settings.Port, apiKey: string.IsNullOrEmpty(settings.ApiKey) ? null : settings.ApiKey);
});

builder.Services.AddSingleton<IVectorDatabase, QdrantVectorDb>();
builder.Services.AddSingleton<IWikiRepository>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("WikiDb") 
                           ?? "Data Source=data/sqlite/codemri.db";
    return new SqliteWikiRepository(connectionString);
});

// Agent Services
builder.Services.AddSingleton<AgentMessageBus>();
builder.Services.AddSingleton<DelegationService>();
builder.Services.AddScoped<IAgent, AnalyzerAgent>();
builder.Services.AddScoped<IAgent, DocumenterAgent>();
builder.Services.AddScoped<IAgent, SynthesizerAgent>();
builder.Services.AddScoped<IAgent, ValidatorAgent>();
builder.Services.AddScoped<IAgentCoordinator, AgentCoordinator>();

// Core Services
builder.Services.AddScoped<IArchitecturalPatternService, ArchitecturalPatternService>();
builder.Services.AddScoped<IComponentIdentificationService, ComponentIdentificationService>();
builder.Services.AddScoped<IEnhancedDependencyGraphService, EnhancedDependencyGraphService>();
builder.Services.AddScoped<RAGService>();
builder.Services.AddScoped<WikiGenerationService>();
builder.Services.AddScoped<IHierarchicalDecompositionService, HierarchicalDecompositionService>();
builder.Services.AddScoped<IDocumentationGenerationPipeline, DocumentationGenerationPipeline>();

// Visualization Services
builder.Services.AddScoped<IDiagramGenerator, DiagramGeneratorService>();
builder.Services.AddScoped<IVisualSynthesisService, VisualSynthesisService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        b => b.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

// Initialize Vector DB
using (var scope = app.Services.CreateScope())
{
    var vectorDb = scope.ServiceProvider.GetRequiredService<IVectorDatabase>();
    // Initialize a default collection
    await vectorDb.InitializeAsync("default_repo");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
