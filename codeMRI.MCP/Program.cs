using codeMRI.Core.Interfaces;
using codeMRI.Infrastructure.Configuration;
using codeMRI.Infrastructure.Services;
using codeMRI.MCP.Services;
using codeMRI.MCP.Server;
using codeMRI.MCP.UpdateStrategies;
using codeMRI.Core.Services;
using codeMRI.Agents.Services;
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
        var transportMode = GetArgument(args, "--transport", "stdio");
        var httpPort = int.Parse(GetArgument(args, "--port", "8080"));

        Console.Error.WriteLine("========================================");
        Console.Error.WriteLine("CodeMRI MCP Server");
        Console.Error.WriteLine($"Repository: {repositoryPath}");
        Console.Error.WriteLine($"Transport: {transportMode}");
        Console.Error.WriteLine($"Index on Start: {indexOnStart}");
        
        var updateStrategy = GetArgument(args, "--update-strategy", "hybrid");
        Console.Error.WriteLine($"Update Strategy: {updateStrategy}");
        if (transportMode == "http")
            Console.Error.WriteLine($"HTTP Port: {httpPort}");
        if (transportMode == "http-client")
            Console.Error.WriteLine($"Remote URL: {GetArgument(args, "--url", "http://localhost:8080/sse")}");
        Console.Error.WriteLine("========================================");

        // Build host
        var builder = Host.CreateApplicationBuilder(args);
        ConfigureServices(builder.Services, args, repositoryPath);
        
        var app = builder.Build();
        await RunServerAsync(app, args, repositoryPath, indexOnStart);
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

        // Configure AST Service Settings
        services.Configure<ASTServiceSettings>(options =>
        {
            options.BaseUrl = "http://localhost:3000";
            options.TimeoutSeconds = 120;
            options.Enabled = true;
            configuration.GetSection(ASTServiceSettings.SectionName).Bind(options);
        });

        services.AddHttpClient<IASTServiceClient, ASTServiceClient>();

        // Register core services
        services.AddSingleton<ICSharpParser, RoslynCSharpParser>();
        services.AddSingleton<IProgressService, ProgressService>();
        services.AddSingleton<IComponentIdentificationService, ComponentIdentificationService>();
        services.AddSingleton<IEnhancedDependencyGraphService, EnhancedDependencyGraphService>();
        services.AddSingleton<QueryEngine>();
        services.AddSingleton<IndexStateService>();
        services.AddSingleton<GraphIndexService>();

        // Register MCP server components
        services.AddSingleton<McpProtocolHandler>();
       
        var transportMode = GetArgument(args, "--transport", "stdio");
        if (transportMode == "http")
        {
            var httpPort = int.Parse(GetArgument(args, "--port", "8080"));
            services.AddSingleton<IMcpTransport>(sp => 
                new StreamableHttpMcpTransport(sp.GetRequiredService<ILogger<StreamableHttpMcpTransport>>(), httpPort));
        }
        else
        {
            services.AddSingleton<IMcpTransport, StdioTransport>();
        }

        services.AddSingleton<JsonRpcServer>();

        // Register change detection based on strategy
        var updateStrategy = GetArgument(args, "--update-strategy", "hybrid");
        if (updateStrategy == "hybrid")
        {
            services.AddHostedService<HybridChangeDetector>(sp =>
                new HybridChangeDetector(
                    sp.GetRequiredService<GraphIndexService>(),
                    sp.GetRequiredService<IndexStateService>(),
                    sp.GetRequiredService<ILogger<HybridChangeDetector>>(),
                    repositoryPath));
        }
        else if (updateStrategy == "polling")
        {
             services.AddHostedService<PollingChangeDetector>(sp =>
                new PollingChangeDetector(
                    sp.GetRequiredService<GraphIndexService>(),
                    sp.GetRequiredService<IndexStateService>(),
                    sp.GetRequiredService<ILogger<PollingChangeDetector>>(),
                    repositoryPath));
        }

        // Tools
        services.AddTransient<codeMRI.MCP.Tools.FindReferencesTool>();
        services.AddTransient<codeMRI.MCP.Tools.CallHierarchyTool>();
        services.AddTransient<codeMRI.MCP.Tools.FindImplementationsTool>();
        services.AddTransient<codeMRI.MCP.Tools.QueryDependencyGraphTool>();
        services.AddTransient<codeMRI.MCP.Tools.TypeHierarchyTool>();
        services.AddTransient<codeMRI.MCP.Tools.SemanticSearchTool>();
        services.AddTransient<codeMRI.MCP.Tools.RefreshGraphTool>();
    }

    private static async Task RunServerAsync(IHost app, string[] args, string repositoryPath, bool indexOnStart)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        var graphIndexService = app.Services.GetRequiredService<GraphIndexService>();

        // Start background services (HostedServices)
        await app.StartAsync();

        if (indexOnStart)
        {
            logger.LogInformation("Starting initial indexing...");
            await graphIndexService.IndexRepositoryAsync(repositoryPath);
            logger.LogInformation("Initial indexing complete");
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) => { e.Cancel = true; cts.Cancel(); };

        var jsonRpcServer = app.Services.GetRequiredService<JsonRpcServer>();
        
        try 
        {
            await jsonRpcServer.RunAsync(cts.Token);
        }
        finally
        {
            // Graceful shutdown of background services
            await app.StopAsync();
        }
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
