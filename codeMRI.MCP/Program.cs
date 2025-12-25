using codeMRI.Core.Interfaces;
using codeMRI.Infrastructure.Configuration;
using codeMRI.Infrastructure.Services;
using codeMRI.MCP.Services;
using codeMRI.MCP.Server;
using codeMRI.MCP.UpdateStrategies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace codeMRI.MCP;

class Program
{
    static async Task Main(string[] args)
    {
        // Parse arguments
        var repositoryPath = GetRepositoryPath(args);
        var indexOnStart = GetBoolArgument(args, "--index-on-start", true);

        Console.Error.WriteLine("========================================");
        Console.Error.WriteLine("CodeMRI MCP Server");
        Console.Error.WriteLine($"Repository: {repositoryPath}");
        Console.Error.WriteLine($"Index on Start: {indexOnStart}");
        
        var updateStrategy = GetArgument(args, "--update-strategy", "hybrid");
        Console.Error.WriteLine($"Update Strategy: {updateStrategy}");
        Console.Error.WriteLine("========================================");

        // Build host
        var builder = Host.CreateApplicationBuilder(args);
        ConfigureServices(builder.Services, args, repositoryPath);
        
        var app = builder.Build();
        await RunServerAsync(app, repositoryPath, indexOnStart);
    }

    private static void ConfigureServices(IServiceCollection services, string[] args, string repositoryPath)
    {
        // Add configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddCommandLine(args)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Configure logging to stderr
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Information);
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Configure AST Service Settings with defaults
        services.Configure<ASTServiceSettings>(options =>
        {
            // Start with defaults from the class
            options.BaseUrl = "http://localhost:3000";
            options.TimeoutSeconds = 120;
            options.Enabled = true;

            // Override with configuration if available
            configuration.GetSection(ASTServiceSettings.SectionName).Bind(options);

            // Override with command-line if specified
            var astServiceUrl = GetArgument(args, "--ast-service-url", string.Empty);
            if (!string.IsNullOrEmpty(astServiceUrl))
            {
                options.BaseUrl = astServiceUrl;
            }
        });

        // Register AST Service with configured settings
        services.AddHttpClient<IASTServiceClient, ASTServiceClient>();

        // Register core services
        services.AddSingleton<ICSharpParser, RoslynCSharpParser>();
        services.AddSingleton<QueryEngine>();
        services.AddSingleton<IndexStateService>();

        // Register MCP server components
        services.AddSingleton<McpProtocolHandler>();
        services.AddSingleton<JsonRpcServer>();

        // Register change detection (hybrid strategy) - with factory to pass repositoryPath
        services.AddSingleton<HybridChangeDetector>(sp =>
            new HybridChangeDetector(
                sp.GetRequiredService<GraphIndexService>(),
                sp.GetRequiredService<IndexStateService>(),
                sp.GetRequiredService<ILogger<HybridChangeDetector>>(),
                repositoryPath));

        // Register graph indexer
        services.AddSingleton<GraphIndexService>();

        // Register MCP Tools
        services.AddTransient<codeMRI.MCP.Tools.FindReferencesTool>();
        services.AddTransient<codeMRI.MCP.Tools.CallHierarchyTool>();
        services.AddTransient<codeMRI.MCP.Tools.FindImplementationsTool>();
        services.AddTransient<codeMRI.MCP.Tools.QueryDependencyGraphTool>();
        services.AddTransient<codeMRI.MCP.Tools.TypeHierarchyTool>();
        services.AddTransient<codeMRI.MCP.Tools.SemanticSearchTool>();
        services.AddTransient<codeMRI.MCP.Tools.RefreshGraphTool>();
    }

    private static async Task RunServerAsync(IHost app, string repositoryPath, bool indexOnStart)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        var graphIndexService = app.Services.GetRequiredService<GraphIndexService>();
        var jsonRpcServer = app.Services.GetRequiredService<JsonRpcServer>();

        // Perform initial indexing if requested
        if (indexOnStart)
        {
            Console.Error.WriteLine("Update Strategy: Hybrid (FileSystemWatcher with polling fallback)");
            logger.LogInformation("Starting initial indexing...");
            await graphIndexService.IndexRepositoryAsync(repositoryPath);
            logger.LogInformation("Initial indexing complete");
        }

        // Start the MCP server
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        await jsonRpcServer.RunAsync(cts.Token);
    }

    private static string GetRepositoryPath(string[] args)
    {
        var path = GetArgument(args, "--repository", Environment.GetEnvironmentVariable("CODEMRI_REPO_PATH") ?? Directory.GetCurrentDirectory());
        return Path.GetFullPath(path);
    }

    private static string GetArgument(string[] args, string name, string defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
                return args[i + 1];
        }
        return defaultValue;
    }

    private static bool GetBoolArgument(string[] args, string name, bool defaultValue)
    {
        var value = GetArgument(args, name, defaultValue.ToString());
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }
}
