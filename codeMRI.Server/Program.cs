using System.Text.Json;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Infrastructure;
using codeMRI.Infrastructure.Configuration;
using codeMRI.Infrastructure.Services;
using codeMRI.Core.Services;
using codeMRI.Core.Services.MessageComposition;
using codeMRI.Server;
using codeMRI.Server.Hubs;
using codeMRI.Server.Infrastructure.Logging;
using Microsoft.Data.Sqlite;
using Serilog;

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

// Use centralized service registration
ServiceRegistration.ConfigureServices(builder.Services, builder.Configuration);

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