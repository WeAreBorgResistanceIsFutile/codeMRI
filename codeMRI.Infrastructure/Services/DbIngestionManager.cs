using System.Data;
using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace codeMRI.Infrastructure.Services;

public class DbIngestionManager : IIngestionJobManager
{
    private readonly string _connectionString;
    private readonly ILogger<DbIngestionManager> _logger;
    private readonly AgentMessageBus _messageBus;
    private readonly ICodeWikiOrchestrator _orchestrator;
    private readonly IWikiApiClient _wikiApi; // Wait, we don't need API client here. We need helper services.
    private readonly IServiceProvider _serviceProvider; // To resolve Orchestrator in a new scope if needed? No, Orchestrator should be singleton or scoped.
    
    // We need to inject the Orchestrator to run the actual job.
    // However, Circular dependency risk if Orchestrator depends on Manager.
    // In the plan, Orchestrator depends on "IProgressService".
    
    // Let's rely on a callback or separate "JobRunner" service to avoid putting heavy logic in the Manager.
    // But for simplicity, I'll inject the dependencies needed to RUN the job here, 
    // OR have the Manager just manage state and spawn a Task that resolves services.
    
    // Better: The Manager just manages state. The Controller or a BackgroundService runs the job using the Manager to update state.
    // BUT the requirement is "Fire and Forget" from the Controller.
    // So the Manager should probably spawn the Task.
    
    // Dependencies to run the job:
    private readonly IServiceScopeFactory _scopeFactory;

    public DbIngestionManager(
        ILogger<DbIngestionManager> logger,
        AgentMessageBus messageBus,
        IServiceScopeFactory scopeFactory,
        string dbPath = "ingestion.db")
    {
        _logger = logger;
        _messageBus = messageBus;
        _scopeFactory = scopeFactory;
        
        // Ensure directory exists
        var builder = new SqliteConnectionStringBuilder($"Data Source={dbPath}");
        if (!string.IsNullOrEmpty(builder.DataSource) && builder.DataSource != ":memory:")
        {
             var dir = Path.GetDirectoryName(builder.DataSource);
             if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
             {
                 Directory.CreateDirectory(dir);
             }
        }
        
        _connectionString = $"Data Source={dbPath}";
        
        InitializeDb();
    }

    private void InitializeDb()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS IngestionJobs (
                Id TEXT PRIMARY KEY,
                RepoUrl TEXT,
                RepoPath TEXT,
                RepoName TEXT,
                Status INTEGER,
                ProgressPercentage INTEGER,
                CurrentPhase TEXT,
                Message TEXT,
                WorkerId TEXT,
                CreatedAt TEXT,
                LastUpdated TEXT,
                Error TEXT
            )");
    }

    public async Task<IngestionJob> StartJobAsync(string repoUrl, bool forceRegenerate, string? connectionId = null)
    {
        // 1. Check if active job exists for this URL
        using var connection = new SqliteConnection(_connectionString);
        var existing = await connection.QueryFirstOrDefaultAsync<IngestionJob>(
            "SELECT * FROM IngestionJobs WHERE RepoUrl = @RepoUrl AND Status IN (@Queued, @Cloning, @Analyzing, @Generating)",
            new { RepoUrl = repoUrl, Queued = IngestionStatus.Queued, Cloning = IngestionStatus.Cloning, Analyzing = IngestionStatus.Analyzing, Generating = IngestionStatus.Generating });

        if (existing != null)
        {
            _logger.LogInformation("Found existing active job {JobId} for {Url}", existing.Id, repoUrl);
            return existing;
        }

        // 2. Create new job
        var job = new IngestionJob
        {
            RepoUrl = repoUrl,
            Status = IngestionStatus.Queued,
            WorkerId = Environment.MachineName,
            RepoName = Path.GetFileNameWithoutExtension(repoUrl) // Preliminary name
        };

        await connection.ExecuteAsync(@"
            INSERT INTO IngestionJobs (Id, RepoUrl, RepoPath, RepoName, Status, ProgressPercentage, CurrentPhase, Message, WorkerId, CreatedAt, LastUpdated, Error)
            VALUES (@Id, @RepoUrl, @RepoPath, @RepoName, @Status, @ProgressPercentage, @CurrentPhase, @Message, @WorkerId, @CreatedAt, @LastUpdated, @Error)",
            job);

        // 3. Spawn background execution
        _ = Task.Run(() => RunJobAsync(job.Id, repoUrl, forceRegenerate, connectionId));

        return job;
    }

    public async Task<IngestionJob?> GetJobAsync(string jobId)
    {
        using var connection = new SqliteConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<IngestionJob>("SELECT * FROM IngestionJobs WHERE Id = @Id", new { Id = jobId });
    }

    public async Task<List<IngestionJob>> ListActiveJobsAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        // List jobs from last 24 hours or active
        var jobs = await connection.QueryAsync<IngestionJob>(
            "SELECT * FROM IngestionJobs WHERE Status NOT IN (@Completed, @Failed, @Cancelled) OR CreatedAt > @Cutoff ORDER BY CreatedAt DESC",
            new { Completed = IngestionStatus.Completed, Failed = IngestionStatus.Failed, Cancelled = IngestionStatus.Cancelled, Cutoff = DateTime.UtcNow.AddHours(-24) });
        return jobs.AsList();
    }

    public async Task CancelJobAsync(string jobId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(
            "UPDATE IngestionJobs SET Status = @Cancelling, LastUpdated = @Now WHERE Id = @Id AND Status NOT IN (@Completed, @Failed, @Cancelled)",
            new { Cancelling = IngestionStatus.Cancelling, Now = DateTime.UtcNow, Id = jobId });
            
        // Publish event so local job runner can pick it up if it's not polling (though we will implement polling/checking)
        await _messageBus.PublishAsync(new AgentMessage 
        { 
            MessageType = "IngestionCancellation", 
            Content = jobId 
        });
    }

    private async Task RunJobAsync(string jobId, string repoUrl, bool forceRegenerate, string? connectionId)
    {
        _logger.LogInformation("Starting background processing for job {JobId}", jobId);
        
        using var scope = _scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ICodeWikiOrchestrator>();
        var wikiRepo = scope.ServiceProvider.GetRequiredService<IWikiRepository>(); // If we need it for pre-checks
        
        // Polling cancellation token
        using var cts = new CancellationTokenSource();
        
        try
        {
            await UpdateJobStatusAsync(jobId, IngestionStatus.Cloning, 0, "Cloning repository...");

            // 1. Clone (using GitHelper which is static, but we should wrap calling it to handle errors/cancellation)
            // We need to implement cancellation checks manually or pass token down if possible.
            // Since GitHelper doesn't take token yet (per my plan), I will just check before/after.
            
            if (await CheckCancellationAsync(jobId)) return;

            string targetDir = await GitHelper.CloneRepositoryAsync(repoUrl);
            
            // Update Job with path and real name
            using (var connection = new SqliteConnection(_connectionString))
            {
                var repoName = Path.GetFileName(targetDir);
                 await connection.ExecuteAsync(
                    "UPDATE IngestionJobs SET RepoPath = @RepoPath, RepoName = @RepoName, LastUpdated = @Now WHERE Id = @Id",
                    new { RepoPath = targetDir, RepoName = repoName, Now = DateTime.UtcNow, Id = jobId });
            }

            if (await CheckCancellationAsync(jobId)) return;

            // 2. Orchestration
            await UpdateJobStatusAsync(jobId, IngestionStatus.Analyzing, 10, "Starting analysis...");
            
            var progress = new Progress<ProgressInfo>(async p => 
            {
                // Map Orchestrator phases to Job status
                var status = p.Phase switch 
                {
                    "Decomposition" => IngestionStatus.Analyzing,
                    "Rubric Generation" => IngestionStatus.Analyzing,
                    "Content Generation" => IngestionStatus.Generating,
                    "Evaluation" => IngestionStatus.Generating,
                    "Complete" => IngestionStatus.Completed,
                    _ => IngestionStatus.Analyzing
                };
                
                // Throttle updates? For now, direct update.
                await UpdateJobStatusAsync(jobId, status, p.Percentage, p.Message);
            });

            // We need to wrap the orchestrator call to support cancellation via the token that we cancel if DB says "Cancel"
            // Start a task to poll DB for cancellation and cancel CTS
            _ = Task.Run(async () => 
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    if (await CheckCancellationAsync(jobId))
                    {
                        cts.Cancel();
                        break;
                    }
                    await Task.Delay(2000);
                }
            });

            var repoInfo = new RepositoryInfo 
            { 
                Name = Path.GetFileName(targetDir),
                Language = "Detected" // Logic to detect language could be added
            };

            await orchestrator.GenerateAdvancedWikiAsync(targetDir, repoInfo, progress, cts.Token);
            
            await UpdateJobStatusAsync(jobId, IngestionStatus.Completed, 100, "Ingestion complete");
        }
        catch (OperationCanceledException)
        {
            await UpdateJobStatusAsync(jobId, IngestionStatus.Cancelled, 0, "Cancelled by user");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed", jobId);
             using var connection = new SqliteConnection(_connectionString);
             await connection.ExecuteAsync(
                "UPDATE IngestionJobs SET Status = @Status, Error = @Error, LastUpdated = @Now WHERE Id = @Id",
                new { Status = IngestionStatus.Failed, Error = ex.Message, Now = DateTime.UtcNow, Id = jobId });
        }
    }

    private async Task UpdateJobStatusAsync(string jobId, IngestionStatus status, int percentage, string message)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(
            "UPDATE IngestionJobs SET Status = @Status, ProgressPercentage = @Pct, CurrentPhase = @Phase, Message = @Msg, LastUpdated = @Now WHERE Id = @Id",
            new { Status = status, Pct = percentage, Phase = status.ToString(), Msg = message, Now = DateTime.UtcNow, Id = jobId });

        // Publish to MessageBus for SignalR
        await _messageBus.PublishAsync(new AgentMessage 
        { 
            MessageType = "IngestionProgress", 
            Content = System.Text.Json.JsonSerializer.Serialize(new { JobId = jobId, Status = status, Percentage = percentage, Message = message })
        });
    }

    private async Task<bool> CheckCancellationAsync(string jobId)
    {
        using var connection = new SqliteConnection(_connectionString);
        var status = await connection.QueryFirstOrDefaultAsync<IngestionStatus?>("SELECT Status FROM IngestionJobs WHERE Id = @Id", new { Id = jobId });
        return status == IngestionStatus.Cancelling || status == IngestionStatus.Cancelled;
    }
}
