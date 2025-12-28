using System.CommandLine;
using System.Text.Json;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace codeMRI.Benchmark;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("codeMRI Benchmark CLI - Run benchmarks using the server's infrastructure");

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

        // 2. Build a minimal host that uses the Server's configuration
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

        // Load the Server's appsettings.Development.json
        var serverAppSettings = Path.Combine(AppContext.BaseDirectory, "../../../../codeMRI.Server/appsettings.Development.json");
        if (File.Exists(serverAppSettings))
        {
            builder.Configuration.AddJsonFile(serverAppSettings, optional: false, reloadOnChange: false);
        }
        else
        {
            Console.WriteLine($"Warning: Could not find server appsettings at {serverAppSettings}");
        }

        // Force overrides specific to Benchmark tool behavior
        var overrides = new Dictionary<string, string?>
        {
            {"CodeWiki:GenerateAllPages", "true"} 
        };
        builder.Configuration.AddInMemoryCollection(overrides);

        // Use the Server's service registration (this is the key part!)
        codeMRI.Server.ServiceRegistration.ConfigureServices(builder.Services, builder.Configuration);

        // Override ModelRoutingSettings from CLI config
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

        using var host = builder.Build();
        var services = host.Services;

        // Initialize Vector Store
        try
        {
            var vectorStoreInit = services.GetRequiredService<IVectorStoreInitializationService>();
            await vectorStoreInit.InitializeAsync();
            Console.WriteLine("Vector Store initialized successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to initialize vector store: {ex.Message}");
        }

        // 3. Start Benchmark using the IngestionJobManager (just like the UI does)
        var ingestionManager = services.GetRequiredService<IIngestionJobManager>();
        var benchmarkingService = services.GetRequiredService<IBenchmarkingService>();
        
        string runName = name ?? $"Benchmark-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        Console.WriteLine($"Starting benchmark '{runName}' for {input}...");

        BenchmarkRun? run = null;
        try
        {
            // Start Benchmark Run
            run = await benchmarkingService.StartBenchmarkRunAsync(
                input, 
                runName, 
                configJson);

            Console.WriteLine($"Benchmark Run ID: {run.Id}");

            // Start the ingestion job (this will run in the background just like the UI)
            var job = await ingestionManager.StartJobAsync(input, forceRegenerate: true, AudienceType.Developer);
            Console.WriteLine($"Created Ingestion Job ID: {job.Id}");
            Console.WriteLine("Job is running in the background...");

            // Poll for completion
            while (true)
            {
                await Task.Delay(2000);
                var currentJob = await ingestionManager.GetJobAsync(job.Id);
                
                if (currentJob == null)
                {
                    Console.WriteLine("Job not found!");
                    break;
                }

                Console.Write($"\r[{currentJob.Status}] {currentJob.CurrentPhase} - {currentJob.ProgressPercentage}%: {currentJob.Message}".PadRight(100));

                if (currentJob.Status == IngestionStatus.Completed)
                {
                    Console.WriteLine("\n\nIngestion completed successfully!");
                    break;
                }
                else if (currentJob.Status == IngestionStatus.Failed)
                {
                    Console.WriteLine($"\n\nIngestion failed: {currentJob.Error}");
                    break;
                }
                else if (currentJob.Status == IngestionStatus.Cancelled)
                {
                    Console.WriteLine("\n\nIngestion was cancelled.");
                    break;
                }
            }

            // Complete Benchmark
            await benchmarkingService.CompleteBenchmarkRunAsync(run.Id);
            
            // Allow time for async metrics to be processed
            await Task.Delay(2000);

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
    }
}
