using System.Text.Json;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace codeMRI.Server.Tests;

[TestFixture]
public class DebugSnapshotReplayTests
{
    private ILLMClient? _llmClient;

    private WebApplicationFactory<Program> _factory = default!;
    private IServiceProvider _services = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../.."));
        var dbPath = Path.Combine(projectRoot, "data/sqlite/codemri.db");
        var connectionString = $"Data Source={dbPath}";

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var logPath = Path.Combine(projectRoot, "codeMRI.Server/logs/ingestion-test-.log");
                
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:WikiDb"] = connectionString,
                    ["ConnectionStrings:IngestionDb"] = connectionString,
                    ["Serilog:WriteTo:0:Name"] = "File",
                    ["Serilog:WriteTo:0:Args:path"] = logPath,
                    ["Serilog:WriteTo:0:Args:rollingInterval"] = "Day",
                    // Override chunking configuration - use conservative limit for Ollama
                    ["Embedding:Chunking:MaxTokens"] = "512",
                    ["Embedding:Chunking:OverlapTokens"] = "50"
                });
            });
        });
        _services = _factory.Services;

        _llmClient = (ILLMClient)_services.GetService(typeof(ILLMClient))!;
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _factory.Dispose();
    }

    [Test]
    [Explicit("This test is for manual debugging/replay of captured snapshots. It depends on local files and a running Ollama instance.")]
    [TestCase("/Users/levente/AI/codeMRI/codeMRI.Server.Tests/bin/Debug/data/repos/codeMRI/.codemri/debug/")]
    public async Task ReplaySnapshot_ShouldSendToLLM_WhenSnapshotExists(string debugDir)
    {
        // 1. Find the most recent snapshot file in the directory
        if (!Directory.Exists(debugDir))
        {
            Assert.Ignore($"Debug directory not found: {debugDir}");
            return;
        }

        foreach (var snapshotFile in Directory.GetFiles(debugDir, "*.json"))
        {
            await TestContext.Out.WriteLineAsync($"Replaying snapshot: {snapshotFile}");

            // 2. Load and Deserialize
            var json = await File.ReadAllTextAsync(snapshotFile);
            var snapshot = JsonSerializer.Deserialize<DebugSnapshot>(json);

            Assert.That(snapshot, Is.Not.Null, "Failed to deserialize snapshot");
            await TestContext.Out.WriteLineAsync($"Snapshot JobId: {snapshot!.JobId}");
            await TestContext.Out.WriteLineAsync($"Snapshot Component: {snapshot.ComponentId}");
            await TestContext.Out.WriteLineAsync($"Snapshot Error: {snapshot.Error}");
            
            // 3. Reconstruct Messages
            var messages = new List<ChatMessage>();

            if (!string.IsNullOrWhiteSpace(snapshot.SystemPrompt))
            {
                messages.Add(new ChatMessage("system", snapshot.SystemPrompt));
            }

            if (!string.IsNullOrWhiteSpace(snapshot.UserPrompt))
            {
                messages.Add(new ChatMessage("user", snapshot.UserPrompt));
            }

            Assert.That(messages, Is.Not.Empty, "No prompts found in snapshot");
            
            // Use model from snapshot, or fallback to default if empty
            var modelToUse = !string.IsNullOrWhiteSpace(snapshot.Model) ? snapshot.Model : "llama3";
            await TestContext.Out.WriteLineAsync($"Sending to LLM Model: {modelToUse}...");

            try 
            {
                // 4. Send to REAL LLM
                var response = await _llmClient!.ChatAsync(messages, modelToUse);

                // 5. Verify
                Assert.That(response, Is.Not.Null.And.Not.Empty);
                await TestContext.Out.WriteLineAsync("LLM Response received successfully:");
                await TestContext.Out.WriteLineAsync(response.Substring(0, Math.Min(500, response.Length)) + "...");
            }
            catch (Exception ex)
            {
                await TestContext.Out.WriteLineAsync($"Replay failed with error: {ex.Message}");
                throw;
            }
        }
    }
}