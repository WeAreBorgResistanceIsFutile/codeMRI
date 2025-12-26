using System.Text.Json;
using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace codeMRI.Infrastructure.Services;

public class DbIngestionManager : IIngestionJobManager
{
    private readonly string _connectionString;
    private readonly ILogger<DbIngestionManager> _logger;
    private readonly AgentMessageBus _messageBus;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDebugSnapshotService _snapshotService;

    public DbIngestionManager(
        ILogger<DbIngestionManager> logger,
        AgentMessageBus messageBus,
        IServiceScopeFactory scopeFactory,
        IDebugSnapshotService snapshotService,
        string dbPath)
    {
        _logger = logger;
        _messageBus = messageBus;
        _scopeFactory = scopeFactory;
        _snapshotService = snapshotService;

        // Ensure directory exists
        var builder = new SqliteConnectionStringBuilder($"Data Source={dbPath}");
        if (!string.IsNullOrEmpty(builder.DataSource) && builder.DataSource != ":memory:")
        {
            var dir = Path.GetDirectoryName(builder.DataSource);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        _connectionString = $"Data Source={dbPath}";

        InitializeDb();
    }

    public async Task<IngestionJob> StartJobAsync(string repoUrl, bool forceRegenerate, AudienceType audience,
        string? connectionId = null)
    {
        // 1. Check if active job exists for this URL
        using var connection = new SqliteConnection(_connectionString);
        var existing = await connection.QueryFirstOrDefaultAsync<IngestionJob>(
            "SELECT * FROM IngestionJobs WHERE RepoUrl = @RepoUrl AND Status IN (@Queued, @Cloning, @Analyzing, @Generating)",
            new
            {
                RepoUrl = repoUrl, IngestionStatus.Queued, IngestionStatus.Cloning, IngestionStatus.Analyzing,
                IngestionStatus.Generating
            });

        if (!forceRegenerate && existing != null)
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
            RepoName = Path.GetFileNameWithoutExtension(repoUrl), // Preliminary name
            Audience = audience
        };

        await connection.ExecuteAsync(@"
            INSERT INTO IngestionJobs (Id, RepoUrl, RepoPath, RepoName, Status, ProgressPercentage, CurrentPhase, Message, WorkerId, CreatedAt, LastUpdated, Error, Audience)
            VALUES (@Id, @RepoUrl, @RepoPath, @RepoName, @Status, @ProgressPercentage, @CurrentPhase, @Message, @WorkerId, @CreatedAt, @LastUpdated, @Error, @Audience)",
            job);

        // 3. Spawn background execution
        _ = Task.Run(() => RunJobAsync(job.Id, repoUrl, forceRegenerate, audience, connectionId));

        return job;
    }

    public async Task<IngestionJob?> GetJobAsync(string jobId)
    {
        using var connection = new SqliteConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<IngestionJob>("SELECT * FROM IngestionJobs WHERE Id = @Id",
            new { Id = jobId });
    }

    public async Task<List<IngestionJob>> ListActiveJobsAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        // List jobs from last 24 hours or active
        var jobs = await connection.QueryAsync<IngestionJob>(
            "SELECT * FROM IngestionJobs WHERE Status NOT IN (@Completed, @Failed, @Cancelled) OR CreatedAt > @Cutoff ORDER BY CreatedAt DESC",
            new
            {
                IngestionStatus.Completed, IngestionStatus.Failed, IngestionStatus.Cancelled,
                Cutoff = DateTime.UtcNow.AddHours(-24)
            });
        return jobs.AsList();
    }

    public async Task CancelJobAsync(string jobId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(
            "UPDATE IngestionJobs SET Status = @Cancelling, LastUpdated = @Now WHERE Id = @Id AND Status NOT IN (@Completed, @Failed, @Cancelled)",
            new
            {
                IngestionStatus.Cancelling, IngestionStatus.Completed, IngestionStatus.Failed,
                IngestionStatus.Cancelled, Now = DateTime.UtcNow, Id = jobId
            });

        // Publish event so local job runner can pick it up if it's not polling (though we will implement polling/checking)
        await _messageBus.PublishAsync(new AgentMessage
        {
            MessageType = "IngestionCancellation",
            Content = jobId
        });
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
                Error TEXT,
                Audience INTEGER DEFAULT 0
            )");

        // Migration: Check if Audience column exists, if not add it
        var audienceExists =
            connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM pragma_table_info('IngestionJobs') WHERE name='Audience'");
        if (audienceExists == 0) connection.Execute("ALTER TABLE IngestionJobs ADD COLUMN Audience INTEGER DEFAULT 0");

        // Sanitize data (fix previous string enums)
        try
        {
            connection.Execute("UPDATE IngestionJobs SET Status = 4 WHERE Status = 'Completed'");
            connection.Execute("UPDATE IngestionJobs SET Status = 5 WHERE Status = 'Failed'");
            connection.Execute("UPDATE IngestionJobs SET Status = 7 WHERE Status = 'Cancelled'");
            connection.Execute("UPDATE IngestionJobs SET Status = 6 WHERE Status = 'Cancelling'");
            connection.Execute("UPDATE IngestionJobs SET Status = 0 WHERE Status = 'Queued'");
            connection.Execute("UPDATE IngestionJobs SET Status = 1 WHERE Status = 'Cloning'");
            connection.Execute("UPDATE IngestionJobs SET Status = 2 WHERE Status = 'Analyzing'");
            connection.Execute("UPDATE IngestionJobs SET Status = 3 WHERE Status = 'Generating'");
        }
        catch
        {
            /* Ignore if fails, e.g. type mismatch in where clause if strict */
        }
    }

    private async Task RunJobAsync(string jobId, string repoUrl, bool forceRegenerate, AudienceType audience,
        string? connectionId)
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

            // Determine target path
            var cleanName = Path.GetFileNameWithoutExtension(repoUrl);
            if (string.IsNullOrWhiteSpace(cleanName)) cleanName = "repo_" + jobId;

            // Sanitize name
            cleanName = string.Join("_", cleanName.Split(Path.GetInvalidFileNameChars()));

            var targetDir = Path.GetFullPath(Path.Combine("../data/repos", cleanName));

            // Ensure data dir exists
            var dataDir = Path.GetDirectoryName(targetDir);
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir!);

            // Clean up if exists (fresh clone) or we could pull... for now overwrite
            if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);


            await GitHelper.CloneRepositoryAsync(repoUrl, targetDir, cts.Token);

            // Update Job with path (but don't mark as complete yet - that happens after wiki generation)
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.ExecuteAsync(
                    @"UPDATE IngestionJobs 
                      SET RepoPath = @RepoPath,
                          LastUpdated = @LastUpdated
                      WHERE Id = @Id",
                    new { RepoPath = targetDir, Id = jobId, LastUpdated = DateTime.UtcNow });
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
            // Capture token to avoid accessing disposed cts in the loop condition race
            var token = cts.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        if (await CheckCancellationAsync(jobId))
                        {
                            cts.Cancel();
                            break;
                        }

                        await Task.Delay(2000, token);
                    }
                }
                catch (ObjectDisposedException)
                {
                    /* Request likely finished, cts disposed */
                }
                catch (OperationCanceledException)
                {
                    /* Token cancelled */
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error in cancellation polling for job {JobId}", jobId);
                }
            });

            var branchName = await GitHelper.GetCurrentBranch(targetDir);

            var repoInfo = new RepositoryInfo
            {
                Name = Path.GetFileName(targetDir),
                Language = "Detected", // Logic to detect language could be added
                Url = repoUrl,
                Branch = branchName
            };

            var structure = await orchestrator.GenerateAdvancedWikiAsync(targetDir, repoInfo, progress, cts.Token);

            // IMPORTANT: Save the generated structure to the repository
            await wikiRepo.SaveStructureAsync(targetDir, structure);
            _logger.LogInformation("Saved wiki structure for {RepoPath}", targetDir);

            // Save the remote URL for re-ingestion
            // Only save if it's a remote URL (not a local directory path)
            if (GitHelper.IsGitUrl(repoUrl) && !Directory.Exists(repoUrl))
            {
                await wikiRepo.SetRepositoryRemoteUrlAsync(targetDir, repoUrl);
                _logger.LogInformation("Saved remote URL {RemoteUrl} for repository {RepoPath}", repoUrl, targetDir);
            }

            // Cleanup debug snapshots
            try
            {
                await _snapshotService.DeleteSnapshotsAsync(targetDir);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup debug snapshots for {RepoPath} after ingestion, but continuing.", targetDir);
            }

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
            new
            {
                Status = status, Pct = percentage, Phase = status.ToString(), Msg = message, Now = DateTime.UtcNow,
                Id = jobId
            });

        // Publish to MessageBus for SignalR
        await _messageBus.PublishAsync(new AgentMessage
        {
            MessageType = "IngestionProgress",
            Content = JsonSerializer.Serialize(new
                { JobId = jobId, Status = status, Percentage = percentage, Message = message })
        });
    }

    private async Task<bool> CheckCancellationAsync(string jobId)
    {
        using var connection = new SqliteConnection(_connectionString);
        var status =
            await connection.QueryFirstOrDefaultAsync<IngestionStatus?>(
                "SELECT Status FROM IngestionJobs WHERE Id = @Id", new { Id = jobId });
        return status == IngestionStatus.Cancelling || status == IngestionStatus.Cancelled;
    }
}