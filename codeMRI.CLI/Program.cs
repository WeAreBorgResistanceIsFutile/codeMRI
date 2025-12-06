using System.CommandLine;
using System.CommandLine.Invocation;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Infrastructure;
using codeMRI.Infrastructure.Configuration;
using codeMRI.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;

namespace codeMRI.CLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("codeMRI CLI tool for automated documentation generation");

        var inputOption = new Option<string>(
            aliases: new[] { "--input", "-i" },
            description: "Path to local repository or Git URL")
        { IsRequired = true };

        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "Enable verbose logging");

        rootCommand.AddOption(inputOption);
        rootCommand.AddOption(verboseOption);

        rootCommand.SetHandler(async (string input, bool verbose) =>
        {
            await RunAsync(input, verbose);
        }, inputOption, verboseOption);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task RunAsync(string input, bool verbose)
    {
        // 1. Setup Configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // 2. Setup Services
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information);
        });

        // Config
        services.Configure<OllamaSettings>(configuration.GetSection("Ollama"));
        services.Configure<QdrantSettings>(configuration.GetSection("Qdrant"));
        services.Configure<ASTServiceSettings>(configuration.GetSection("ASTService"));

        // Infrastructure
        services.AddHttpClient();
        services.AddSingleton<IEmbedder, OllamaEmbedderService>();
        services.AddSingleton<ILLMClient, OllamaLLMService>();
        services.AddSingleton<IDocumentProcessor, TextSplitterService>();
        services.AddSingleton<IASTServiceClient, ASTServiceClient>();

        // Wire up core
        WireUp.Registered(services);

        // Database & Vector DB
        services.AddSingleton<IQdrantClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<QdrantSettings>>().Value;
            // Default to localhost if not configured
            var host = string.IsNullOrEmpty(settings.Host) ? "localhost" : settings.Host;
            return new QdrantClient(host, settings.Port, apiKey: string.IsNullOrEmpty(settings.ApiKey) ? null : settings.ApiKey);
        });
        services.AddSingleton<IVectorDatabase, QdrantVectorDb>();
        
        services.AddSingleton<IWikiRepository>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var connectionString = config.GetConnectionString("WikiDb") 
                                   ?? "Data Source=data/sqlite/codemri.db";
            
            // Ensure directory exists if using default relative path
            if (connectionString.Contains("Data Source=data/sqlite/"))
            {
                 var dir = Path.GetDirectoryName("data/sqlite/codemri.db");
                 if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            }
            
            return new SqliteWikiRepository(connectionString);
        });

        var serviceProvider = services.BuildServiceProvider();

        // 3. Initialize & Execute
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var vectorDb = serviceProvider.GetRequiredService<IVectorDatabase>();
        
        try 
        {
            await vectorDb.InitializeAsync("default_repo");
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Failed to initialize VectorDB: {ex.Message}. Make sure Qdrant is running.");
        }

        string targetPath = input;
        bool isTemp = false;

        // Clone if Git URL
        if (GitHelper.IsGitUrl(input))
        {
            try 
            {
                targetPath = await GitHelper.CloneRepositoryAsync(input);
                isTemp = true;
                logger.LogInformation($"Cloned repository to {targetPath}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to clone repository");
                return;
            }
        }
        else
        {
            if (!Directory.Exists(targetPath))
            {
                logger.LogError($"Directory not found: {targetPath}");
                return;
            }
            targetPath = Path.GetFullPath(targetPath);
        }

        logger.LogInformation($"Processing repository at: {targetPath}");

        var orchestrator = serviceProvider.GetRequiredService<ICodeWikiOrchestrator>();
        
        // Infer explicit info for now
        var repoInfo = new RepositoryInfo 
        { 
            Name = Path.GetFileName(targetPath.TrimEnd(Path.DirectorySeparatorChar)),
            Language = "Detected automatically during analysis", 
            Description = $"Documentation for {input}"
        };

        try
        {
            var structure = await orchestrator.GenerateAdvancedWikiAsync(targetPath, repoInfo);
            logger.LogInformation("Documentation generated successfully!");
            logger.LogInformation($"Pages: {structure.Pages.Count}");
            logger.LogInformation($"Sections: {structure.Sections.Count}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate documentation");
        }
        finally
        {
            if (isTemp)
            {
                logger.LogInformation("Cleaning up temporary files...(Keeping them for now for inspection)");
                // Directory.Delete(targetPath, true); 
            }
        }
    }
}
