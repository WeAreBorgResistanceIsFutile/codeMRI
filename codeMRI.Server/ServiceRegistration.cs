using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using codeMRI.Infrastructure;
using codeMRI.Infrastructure.Configuration;
using codeMRI.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace codeMRI.Server;

/// <summary>
/// Centralized service registration for the codeMRI application.
/// Used by both the Server and Benchmark CLI to ensure consistent IoC configuration.
/// </summary>
public static class ServiceRegistration
{
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Configuration
        services.Configure<OllamaSettings>(configuration.GetSection("Ollama"));
        services.Configure<ModelRoutingSettings>(configuration.GetSection("ModelRouting"));
        services.Configure<ASTServiceSettings>(configuration.GetSection("ASTService"));
        services.Configure<CodeWikiOptions>(configuration.GetSection("CodeWiki"));

        // Infrastructure - LLM Services
        services.AddHttpClient();
        services.AddSingleton<IASTServiceClient, ASTServiceClient>();

        // Wire up core application services
        WireUp.Registered(services, configuration);

        // Repository Services
        services.AddSingleton<IWikiRepository>(sp =>
        {
            var connectionString = configuration.GetConnectionString("WikiDb");
            if (string.IsNullOrEmpty(connectionString))
            {
                var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "codeMRI");
                if (!Directory.Exists(appDataPath)) Directory.CreateDirectory(appDataPath);
                connectionString = $"Data Source={Path.Combine(appDataPath, "codemri.db")}";
            }

            return new SqliteWikiRepository(connectionString);
        });

        services.AddSingleton<IIngestionJobManager, DbIngestionManager>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DbIngestionManager>>();
            var messageBus = sp.GetRequiredService<AgentMessageBus>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            var snapshotService = sp.GetRequiredService<IDebugSnapshotService>();

            var connectionString = configuration.GetConnectionString("IngestionDb");
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
                dbPath = Path.Combine(appDataPath, "ingestion.db");
            }

            return new DbIngestionManager(logger, messageBus, scopeFactory, snapshotService, dbPath);
        });

        services.AddSingleton<IGenerationJobManager, DbGenerationJobManager>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DbGenerationJobManager>>();
            var messageBus = sp.GetRequiredService<AgentMessageBus>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

            var connectionString = configuration.GetConnectionString("GenerationDb");
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
        services.AddSingleton<IBenchmarkRepository>(sp =>
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "codeMRI");
            if (!Directory.Exists(appDataPath)) Directory.CreateDirectory(appDataPath);
            var dbPath = Path.Combine(appDataPath, "benchmarks.db");
            return new BenchmarkRepository($"Data Source={dbPath}");
        });

        services.AddSingleton<IBenchmarkingService, BenchmarkingService>();
    }
}
