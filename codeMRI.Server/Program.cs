using System.Text.Json;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Infrastructure;
using codeMRI.Infrastructure.Configuration;
using codeMRI.Infrastructure.Services;
using codeMRI.Core.Services;
using codeMRI.Core.Services.MessageComposition;
using codeMRI.Server.Hubs;
using Microsoft.Data.Sqlite;
using Serilog;
using codeMRI.Server.Infrastructure.Logging;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.With(new CallerInfoEnricher())
    .WriteTo.Console());

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Enable string-to-enum conversion for JSON deserialization
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configuration
builder.Services.Configure<OllamaSettings>(builder.Configuration.GetSection("Ollama"));
builder.Services.Configure<ModelRoutingSettings>(builder.Configuration.GetSection("ModelRouting"));
builder.Services.Configure<ASTServiceSettings>(builder.Configuration.GetSection("ASTService"));
builder.Services.Configure<CodeWikiOptions>(builder.Configuration.GetSection("CodeWiki"));

// Infrastructure - LLM Services
builder.Services.AddHttpClient();

builder.Services.AddSingleton<IASTServiceClient, ASTServiceClient>();

// Wire up core application services
WireUp.Registered(builder.Services, builder.Configuration);

builder.Services.AddSingleton<IWikiRepository>(sp =>
{
    var appDataPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "codeMRI");
    if (!Directory.Exists(appDataPath)) Directory.CreateDirectory(appDataPath);

    var connectionString = builder.Configuration.GetConnectionString("WikiDb");
    if (string.IsNullOrEmpty(connectionString))
    {
        var dbPath = Path.Combine(appDataPath, "codemri.db");
        connectionString = $"Data Source={dbPath}";
    }

    return new SqliteWikiRepository(connectionString);
});

builder.Services.AddSingleton<IIngestionJobManager, DbIngestionManager>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<DbIngestionManager>>();
    var messageBus = sp.GetRequiredService<AgentMessageBus>();
    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
    var snapshotService = sp.GetRequiredService<IDebugSnapshotService>();

    var connectionString = builder.Configuration.GetConnectionString("IngestionDb");
    string dbPath;

    if (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("Data Source="))
    {
        // Extract if it's a connection string
        var connStringBuilder = new SqliteConnectionStringBuilder(connectionString);
        dbPath = connStringBuilder.DataSource;
    }
    else if (!string.IsNullOrEmpty(connectionString))
    {
        // Treat as path
        dbPath = connectionString;
    }
    else
    {
        // Default
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "codeMRI");
        if (!Directory.Exists(appDataPath)) Directory.CreateDirectory(appDataPath);
        dbPath = Path.Combine(appDataPath, "ingestion.db");
    }

    return new DbIngestionManager(logger, messageBus, scopeFactory, snapshotService, dbPath);
});

builder.Services.AddSingleton<IGenerationJobManager, DbGenerationJobManager>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<DbGenerationJobManager>>();
    var messageBus = sp.GetRequiredService<AgentMessageBus>();
    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

    var connectionString = builder.Configuration.GetConnectionString("GenerationDb");
    string dbPath;

    if (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("Data Source="))
    {
        var connStringBuilder = new SqliteConnectionStringBuilder(connectionString);
        dbPath = connStringBuilder.DataSource;
    }
    else if (!string.IsNullOrEmpty(connectionString))
    {
        dbPath = connectionString;
    }
    else
    {
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "codeMRI");
        if (!Directory.Exists(appDataPath)) Directory.CreateDirectory(appDataPath);
        dbPath = Path.Combine(appDataPath, "generation.db");
    }

    return new DbGenerationJobManager(logger, messageBus, scopeFactory, dbPath);
});

// Benchmark Services
builder.Services.AddSingleton<IBenchmarkRepository>(sp =>
{
    var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "codeMRI");
    if (!Directory.Exists(appDataPath)) Directory.CreateDirectory(appDataPath);
    var dbPath = Path.Combine(appDataPath, "benchmarks.db");
    return new BenchmarkRepository($"Data Source={dbPath}");
});

builder.Services.AddSingleton<IBenchmarkingService, BenchmarkingService>();


// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        b => b.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

var app = builder.Build();

// Initialize vector store collections at startup
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var vectorStoreInit = scope.ServiceProvider.GetRequiredService<IVectorStoreInitializationService>();
        await vectorStoreInit.InitializeAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to initialize vector store, continuing with application startup");
    }
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

// Wire up Benchmarking Metrics Callback
var llmFacade = app.Services.GetRequiredService<ILLMServiceFacade>();
var benchmarkingService = app.Services.GetRequiredService<IBenchmarkingService>();

llmFacade.SetMetricsCallback(metrics =>
{
    // Fire and forget to avoid blocking LLM execution
    Task.Run(async () =>
    {
        try
        {
            var activeRun = await benchmarkingService.GetActiveRunAsync();
            if (activeRun != null)
            {
                benchmarkingService.RecordMetrics(activeRun.Id, metrics);
            }
        }
        catch (Exception ex)
        {
            // Suppress errors to avoid crashing background threads
            Console.WriteLine($"Error recording benchmark metrics: {ex.Message}");
        }
    });
});

// Wire up Ingestion Completion to Benchmark Finalization
var messageBus = app.Services.GetRequiredService<AgentMessageBus>();
messageBus.Subscribe("IngestionProgress", async msg =>
{
    try
    {
        var content = msg.Content?.ToString() ?? "{}";
        var data = JsonSerializer.Deserialize<JsonElement>(content);
        var status = (IngestionStatus)data.GetProperty("Status").GetInt32();

        if (status == IngestionStatus.Completed || status == IngestionStatus.Failed || status == IngestionStatus.Cancelled)
        {
            var activeRun = await benchmarkingService.GetActiveRunAsync();
            if (activeRun != null)
            {
                if (status == IngestionStatus.Completed)
                {
                    await benchmarkingService.CompleteBenchmarkRunAsync(activeRun.Id);
                }
                else
                {
                    await benchmarkingService.CancelBenchmarkRunAsync(activeRun.Id);
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error processing IngestionProgress for benchmark: {ex.Message}");
    }
});

app.MapControllers();
app.MapHub<WikiHub>("/wikiHub");

app.Run();

public partial class Program
{
}