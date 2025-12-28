using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using codeMRI.Core.Services.MessageComposition;
using codeMRI.Infrastructure.Configuration;
using codeMRI.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Microsoft.Data.Sqlite;

using codeMRI.Infrastructure;

namespace codeMRI.Benchmark;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("codeMRI Benchmark CLI - Run benchmarks directly in-process");

        var inputOption = new Option<string>(
            new[] { "--input", "-i" },
            "Git URL or local path of the repository to benchmark") { IsRequired = true };

        var configOption = new Option<string>(
            new[] { "--config", "-c" },
            "Path to ModelRouting configuration JSON file") { IsRequired = true };

        var nameOption = new Option<string?>(
            new[] { "--name", "-n" },
            "Name of the benchmark run (default: auto-generated)");
            
        var verboseOption = new Option<bool>(
             new[] { "--verbose", "-v" },
             "Enable verbose logging");

        rootCommand.AddOption(inputOption);
        rootCommand.AddOption(configOption);
        rootCommand.AddOption(nameOption);
        rootCommand.AddOption(verboseOption);

        rootCommand.SetHandler(async (input, configPath, name, verbose) =>
        {
            await RunBenchmarkAsync(input, configPath, name, verbose);
        }, inputOption, configOption, nameOption, verboseOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task RunBenchmarkAsync(string input, string configPath, string? name, bool verbose)
    {
        // 1. Load Configuration
        if (!File.Exists(configPath))
        {
            Console.WriteLine($"Error: Configuration file not found at {configPath}");
            return;
        }

        string configJson;
        ModelRoutingSettings? routingSettings;
        try 
        {
            configJson = await File.ReadAllTextAsync(configPath);
            var options = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            routingSettings = JsonSerializer.Deserialize<ModelRoutingSettings>(configJson, options);
            
            if (routingSettings == null) throw new JsonException("Parsed config is null");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing configuration file: {ex.Message}");
            return;
        }

        // 2. Setup Host
        var builder = Host.CreateApplicationBuilder();

        // Logging
        builder.Services.AddLogging(logging => 
        {
            logging.ClearProviders();
            var loggerConfig = new LoggerConfiguration()
                .WriteTo.Console();
            
            if (verbose) 
                loggerConfig.MinimumLevel.Debug();
            else
                loggerConfig.MinimumLevel.Information();

            logging.AddSerilog(loggerConfig.CreateLogger());
        });

        // App Settings
        var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        builder.Configuration.AddJsonFile(appSettingsPath, optional: false, reloadOnChange: true);

        // Force overrides specific to Benchmark tool behavior
        var overrides = new Dictionary<string, string?>
        {
            // FORCE GenerateAllPages to true to ensure full benchmark coverage regardless of appsettings
            {"CodeWiki:GenerateAllPages", "true"} 
        };
        builder.Configuration.AddInMemoryCollection(overrides);

        // 3. Register Services
        
        // Core WireUp
        WireUp.Registered(builder.Services, builder.Configuration);

        // Infrastructure Managers (Manually registered like in Server/Program.cs)
        RegisterInfrastructure(builder.Services, builder.Configuration);

        // Manual Configuration of ModelRoutingSettings - Overrides appsettings
        // Must be registered AFTER WireUp to ensure it takes precedence if WireUp also configures options
        builder.Services.Configure<ModelRoutingSettings>(settings =>
        {
            settings.EnableModelRouting = routingSettings.EnableModelRouting;
            settings.CodeAnalysisModel = routingSettings.CodeAnalysisModel;
            settings.NaturalLanguageModel = routingSettings.NaturalLanguageModel;
            settings.SynthesisJudgeModel = routingSettings.SynthesisJudgeModel;
            settings.DocumentationModel = routingSettings.DocumentationModel;
            settings.ChatModel = routingSettings.ChatModel;
            settings.JudgeModels = routingSettings.JudgeModels;
            settings.EnableEnsembleGeneration = routingSettings.EnableEnsembleGeneration;
            settings.EnsembleModels = routingSettings.EnsembleModels;
            settings.MinimumAgreementThreshold = routingSettings.MinimumAgreementThreshold;
        });

        // 4. Build Host
        using var host = builder.Build();

        // 5. Initialize & Wire-up Callbacks
        var services = host.Services;
        InitializeCallbacks(services);

        // 6. Start Benchmark
        var benchmarkingService = services.GetRequiredService<IBenchmarkingService>();
        var orchestrator = services.GetRequiredService<ICodeWikiOrchestrator>();
        var telemetry = services.GetRequiredService<IAgentTelemetryService>();
        
        string repoUrl = input;
        string repoPath = input;
        string originalUrl = input;
        bool isTemp = false;

        // Handle Git URL
        if (codeMRI.Infrastructure.Services.GitHelper.IsGitUrl(input))
        {
             Console.WriteLine($"Cloning {input}...");
             try 
             {
                 repoPath = await codeMRI.Infrastructure.Services.GitHelper.CloneRepositoryAsync(input);
                 isTemp = true;
                 Console.WriteLine($"Cloned to: {repoPath}");
             }
             catch (Exception ex)
             {
                 Console.WriteLine($"Error cloning: {ex.Message}");
                 return;
             }
        }
        else
        {
            if (!Directory.Exists(input))
            {
                Console.WriteLine($"Error: Directory not found: {input}");
                return;
            }
            repoPath = Path.GetFullPath(input);
        }

        string runName = name ?? $"Benchmark-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        Console.WriteLine($"Starting benchmark '{runName}' for {originalUrl}...");

        BenchmarkRun? run = null;
        try
        {
            // Start Benchmark Run
            run = await benchmarkingService.StartBenchmarkRunAsync(
                originalUrl, 
                runName, 
                configJson);

            Console.WriteLine($"Benchmark Run ID: {run.Id}");

            // Prepare RepositoryInfo
            var repoInfo = new RepositoryInfo 
            {
                RepoPath = repoPath,
                Url = originalUrl,
                Name = Path.GetFileName(repoPath),
                Branch = await codeMRI.Infrastructure.Services.GitHelper.GetCurrentBranch(repoPath)
            };

            // Progress reporting
            var progress = new Progress<ProgressInfo>(info => {
                 Console.Write($"\rPHASE: {info.Phase} - {info.Message} ({info.Percentage}%)" + new string(' ', 20));
            });

            // Execute Orchestrator Directly
            Console.WriteLine("\nExecuting CodeWiki Orchestrator (Full Workflow)...");
            
            var structure = await orchestrator.GenerateAdvancedWikiAsync(
                repoPath,
                repoInfo,
                progress,
                force: true);

            Console.WriteLine("\n\nWorkflow completed successfully.");
            
            // Complete Benchmark
            await benchmarkingService.CompleteBenchmarkRunAsync(run.Id);
            
            // Generate Report
            var report = await benchmarkingService.GenerateReportAsync(run.Id);
            Console.WriteLine("\n" + report);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nCritical Error: {ex.Message}");
            if (verbose) Console.WriteLine(ex.StackTrace);
            
            if (run != null)
                await benchmarkingService.CancelBenchmarkRunAsync(run.Id);
        }
        finally
        {
            if (isTemp && Directory.Exists(repoPath))
            {
                try
                {
                    Console.WriteLine($"\nCleaning up temporary repository: {repoPath}");
                    Directory.Delete(repoPath, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error cleaning up temporary directory: {ex.Message}");
                }
            }
        }
    }

    private static void RegisterInfrastructure(IServiceCollection services, IConfiguration configuration)
    {
        // Persistence - mimic Server setup but standalone
        services.AddSingleton<IWikiRepository>(sp =>
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "codeMRI");
            if (!Directory.Exists(appDataPath)) Directory.CreateDirectory(appDataPath);
            var dbPath = Path.Combine(appDataPath, "codemri.db");
            return new SqliteWikiRepository($"Data Source={dbPath}");
        });

        services.AddSingleton<IIngestionJobManager, DbIngestionManager>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DbIngestionManager>>();
            var messageBus = sp.GetRequiredService<AgentMessageBus>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            
             // Create a dummy snapshot service if not needed or reuse
             // Assuming WireUp registers IDebugSnapshotService? 
             // If not, we might need to mock it or register it.
             // Checking WireUp is hard, so let's try to get it. If missing, we fix.
             var snapshotService = sp.GetService<IDebugSnapshotService>(); 
             if (snapshotService == null) {
                 // Simple mock or implementation
                 // Actually Infrastructure probably has it.
                 // Let's assume it's registered by WireUp for now.
             }

            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "codeMRI");
            var dbPath = Path.Combine(appDataPath, "ingestion.db");
            return new DbIngestionManager(logger, messageBus, scopeFactory, snapshotService!, dbPath);
        });

        services.AddSingleton<IGenerationJobManager, DbGenerationJobManager>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DbGenerationJobManager>>();
            var messageBus = sp.GetRequiredService<AgentMessageBus>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>(); // Fixed typo

            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "codeMRI");
            var dbPath = Path.Combine(appDataPath, "generation.db");
            return new DbGenerationJobManager(logger, messageBus, scopeFactory, dbPath);
        });

        services.AddSingleton<IBenchmarkRepository>(sp =>
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "codeMRI");
            var dbPath = Path.Combine(appDataPath, "benchmarks.db");
            return new BenchmarkRepository($"Data Source={dbPath}");
        });

        services.AddSingleton<IBenchmarkingService, BenchmarkingService>();
        
        // AST Service
        services.AddSingleton<IASTServiceClient, ASTServiceClient>();
        services.AddHttpClient();
    }

    private static void InitializeCallbacks(IServiceProvider services)
    {
        var llmFacade = services.GetRequiredService<ILLMServiceFacade>();
        var benchmarkingService = services.GetRequiredService<IBenchmarkingService>();
        var messageBus = services.GetRequiredService<AgentMessageBus>();

        // 1. LLM Metrics -> Benchmark
        llmFacade.SetMetricsCallback(metrics =>
        {
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
                catch { /* Ignore */ }
            });
        });

        // 2. Ingestion Progress -> Benchmark Lifecycle
        messageBus.Subscribe("IngestionProgress", async msg =>
        {
            try
            {
                var content = msg.Content?.ToString() ?? "{}";
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("Status", out var statusProp))
                {
                     var status = (IngestionStatus)statusProp.GetInt32();
                     if (status == IngestionStatus.Completed || status == IngestionStatus.Failed || status == IngestionStatus.Cancelled)
                    {
                        var activeRun = await benchmarkingService.GetActiveRunAsync();
                        if (activeRun != null)
                        {
                            if (status == IngestionStatus.Completed)
                                await benchmarkingService.CompleteBenchmarkRunAsync(activeRun.Id);
                            else
                                await benchmarkingService.CancelBenchmarkRunAsync(activeRun.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"Callback Error: {ex.Message}");
            }
        });
    }
}
