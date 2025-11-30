using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using codeMRI.Core.Services;
using codeMRI.Infrastructure.Configuration;
using codeMRI.Infrastructure.Services;
using codeMRI.Core.Interfaces;

namespace TestASTIntegration;

class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Configuration
                services.Configure<ASTServiceSettings>(context.Configuration.GetSection("ASTService"));
                
                // Infrastructure
                services.AddHttpClient();
                services.AddSingleton<IASTServiceClient, ASTServiceClient>();
                
                // Core Services
                services.AddScoped<ComponentIdentificationService>();
            })
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var componentService = host.Services.GetRequiredService<ComponentIdentificationService>();
        var astClient = host.Services.GetRequiredService<IASTServiceClient>();

        logger.LogInformation("Testing AST Service Integration...");

        // Test health check
        var isHealthy = await astClient.IsHealthyAsync();
        logger.LogInformation("AST Service Health: {Healthy}", isHealthy);

        if (!isHealthy)
        {
            logger.LogError("AST Service is not healthy. Exiting.");
            return;
        }

        // Test supported languages
        var supportedLanguages = await astClient.GetSupportedLanguagesAsync();
        logger.LogInformation("Supported languages: {Languages}", string.Join(", ", supportedLanguages));

        // Test parsing Python file
        var pythonFile = "/Users/levente/AI/codeMRI/codeMRI.ASTService/tests/fixtures/python/simple.py";
        if (File.Exists(pythonFile))
        {
            logger.LogInformation("Testing Python file: {File}", pythonFile);
            var pythonComponents = await componentService.AnalyzeFileAsync(pythonFile);
            logger.LogInformation("Found {Count} Python components:", pythonComponents.Count);
            foreach (var component in pythonComponents)
            {
                logger.LogInformation("  - {Name} ({Type}): {Description}", component.Name, component.Type, component.Description);
            }
        }

        // Test parsing JavaScript file
        var jsFile = "/Users/levente/AI/codeMRI/codeMRI.ASTService/tests/fixtures/javascript/simple.js";
        if (File.Exists(jsFile))
        {
            logger.LogInformation("Testing JavaScript file: {File}", jsFile);
            var jsComponents = await componentService.AnalyzeFileAsync(jsFile);
            logger.LogInformation("Found {Count} JavaScript components:", jsComponents.Count);
            foreach (var component in jsComponents)
            {
                logger.LogInformation("  - {Name} ({Type}): {Description}", component.Name, component.Type, component.Description);
            }
        }

        // Test parsing TypeScript file
        var tsFile = "/Users/levente/AI/codeMRI/codeMRI.ASTService/tests/fixtures/typescript/simple.ts";
        if (File.Exists(tsFile))
        {
            logger.LogInformation("Testing TypeScript file: {File}", tsFile);
            var tsComponents = await componentService.AnalyzeFileAsync(tsFile);
            logger.LogInformation("Found {Count} TypeScript components:", tsComponents.Count);
            foreach (var component in tsComponents)
            {
                logger.LogInformation("  - {Name} ({Type}): {Description}", component.Name, component.Type, component.Description);
            }
        }

        logger.LogInformation("AST Service Integration test completed.");
    }
}