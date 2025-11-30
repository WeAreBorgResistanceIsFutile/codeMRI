using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using codeMRI.Infrastructure.Configuration;
using codeMRI.Infrastructure.Services;
using codeMRI.Core.Interfaces;

namespace SimpleASTTest;

class Program
{
    static async Task Main(string[] args)
    {
        // Create configuration
        var configuration = new Dictionary<string, string>
        {
            ["ASTService:BaseUrl"] = "http://localhost:3000",
            ["ASTService:TimeoutSeconds"] = "30",
            ["ASTService:Enabled"] = "true"
        };

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(config =>
            {
                config.AddInMemoryCollection(configuration);
            })
            .ConfigureServices((context, services) =>
            {
                // Configuration
                services.Configure<ASTServiceSettings>(context.Configuration.GetSection("ASTService"));
                
                // Infrastructure
                services.AddHttpClient();
                services.AddSingleton<IASTServiceClient, ASTServiceClient>();
            })
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();
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
            var pythonContent = await File.ReadAllTextAsync(pythonFile);
            
            var pythonResult = await astClient.ParseCodeAsync(pythonContent, "python", pythonFile);
            if (pythonResult != null)
            {
                logger.LogInformation("Python AST parsing successful:");
                logger.LogInformation("  Language: {Language}", pythonResult.Language);
                logger.LogInformation("  File Path: {FilePath}", pythonResult.FilePath);
                logger.LogInformation("  Timestamp: {Timestamp}", pythonResult.Timestamp);
                
                var pythonComponents = await astClient.ConvertToCodeComponentsAsync(pythonResult);
                logger.LogInformation("Found {Count} Python components:", pythonComponents.Count);
                foreach (var component in pythonComponents)
                {
                    logger.LogInformation("  - {Name} ({Type}): {Description}", component.Name, component.Type, component.Description);
                    logger.LogInformation("    Methods: {Methods}", string.Join(", ", component.Methods));
                    logger.LogInformation("    Properties: {Properties}", string.Join(", ", component.Properties));
                    logger.LogInformation("    Dependencies: {Dependencies}", string.Join(", ", component.Dependencies));
                }
            }
            else
            {
                logger.LogError("Failed to parse Python file");
            }
        }

        // Test parsing JavaScript file
        var jsFile = "/Users/levente/AI/codeMRI/codeMRI.ASTService/tests/fixtures/javascript/simple.js";
        if (File.Exists(jsFile))
        {
            logger.LogInformation("Testing JavaScript file: {File}", jsFile);
            var jsContent = await File.ReadAllTextAsync(jsFile);
            
            var jsResult = await astClient.ParseCodeAsync(jsContent, "javascript", jsFile);
            if (jsResult != null)
            {
                logger.LogInformation("JavaScript AST parsing successful:");
                logger.LogInformation("  Language: {Language}", jsResult.Language);
                logger.LogInformation("  File Path: {FilePath}", jsResult.FilePath);
                
                var jsComponents = await astClient.ConvertToCodeComponentsAsync(jsResult);
                logger.LogInformation("Found {Count} JavaScript components:", jsComponents.Count);
                foreach (var component in jsComponents)
                {
                    logger.LogInformation("  - {Name} ({Type}): {Description}", component.Name, component.Type, component.Description);
                    logger.LogInformation("    Methods: {Methods}", string.Join(", ", component.Methods));
                    logger.LogInformation("    Properties: {Properties}", string.Join(", ", component.Properties));
                    logger.LogInformation("    Dependencies: {Dependencies}", string.Join(", ", component.Dependencies));
                }
            }
            else
            {
                logger.LogError("Failed to parse JavaScript file");
            }
        }

        // Test parsing TypeScript file
        var tsFile = "/Users/levente/AI/codeMRI/codeMRI.ASTService/tests/fixtures/typescript/simple.ts";
        if (File.Exists(tsFile))
        {
            logger.LogInformation("Testing TypeScript file: {File}", tsFile);
            var tsContent = await File.ReadAllTextAsync(tsFile);
            
            var tsResult = await astClient.ParseCodeAsync(tsContent, "typescript", tsFile);
            if (tsResult != null)
            {
                logger.LogInformation("TypeScript AST parsing successful:");
                logger.LogInformation("  Language: {Language}", tsResult.Language);
                logger.LogInformation("  File Path: {FilePath}", tsResult.FilePath);
                
                var tsComponents = await astClient.ConvertToCodeComponentsAsync(tsResult);
                logger.LogInformation("Found {Count} TypeScript components:", tsComponents.Count);
                foreach (var component in tsComponents)
                {
                    logger.LogInformation("  - {Name} ({Type}): {Description}", component.Name, component.Type, component.Description);
                    logger.LogInformation("    Methods: {Methods}", string.Join(", ", component.Methods));
                    logger.LogInformation("    Properties: {Properties}", string.Join(", ", component.Properties));
                    logger.LogInformation("    Dependencies: {Dependencies}", string.Join(", ", component.Dependencies));
                }
            }
            else
            {
                logger.LogError("Failed to parse TypeScript file");
            }
        }

        logger.LogInformation("AST Service Integration test completed.");
    }
}