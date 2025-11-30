using codeMRI.Core.Interfaces;
using codeMRI.Core.Services;
using codeMRI.Infrastructure.Configuration;
using codeMRI.Infrastructure.Services;
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

// Infrastructure
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IEmbedder, OllamaEmbedderService>();
builder.Services.AddSingleton<ILLMClient, OllamaLLMService>();
builder.Services.AddSingleton<IDocumentProcessor, TextSplitterService>();

// Register QdrantClient
builder.Services.AddSingleton<IQdrantClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<QdrantSettings>>().Value;
    return new QdrantClient(settings.Host, settings.Port, apiKey: string.IsNullOrEmpty(settings.ApiKey) ? null : settings.ApiKey);
});

builder.Services.AddSingleton<IVectorDatabase, QdrantVectorDb>();
builder.Services.AddSingleton<IWikiRepository, JsonWikiRepository>();

// Core Services
builder.Services.AddScoped<RAGService>();
builder.Services.AddScoped<WikiGenerationService>();

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
