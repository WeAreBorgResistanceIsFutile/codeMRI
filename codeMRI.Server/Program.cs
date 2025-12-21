using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Infrastructure;
using codeMRI.Infrastructure.Configuration;
using codeMRI.Infrastructure.Services;
using codeMRI.Server.Hubs;
using Microsoft.Data.Sqlite;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
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
builder.Services.Configure<ASTServiceSettings>(builder.Configuration.GetSection("ASTService"));
builder.Services.Configure<CodeWikiOptions>(builder.Configuration.GetSection("CodeWiki"));

// Infrastructure
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ILLMClient, OllamaLLMService>();
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

    var connectionString = builder.Configuration.GetConnectionString("IngestionDb");
    string dbPath;

    if (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("Data Source="))
    {
        // Extract if it's a connection string
        var builder = new SqliteConnectionStringBuilder(connectionString);
        dbPath = builder.DataSource;
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

    return new DbIngestionManager(logger, messageBus, scopeFactory, dbPath);
});


// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        b => b.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

var app = builder.Build();

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
app.MapHub<WikiHub>("/wikiHub");

app.Run();

public partial class Program
{
}