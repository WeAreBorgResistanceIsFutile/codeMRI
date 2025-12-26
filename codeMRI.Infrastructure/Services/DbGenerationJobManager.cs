using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace codeMRI.Infrastructure.Services;

public class DbGenerationJobManager : IGenerationJobManager
{
    private readonly string _connectionString;
    private readonly ILogger<DbGenerationJobManager> _logger;
    private readonly AgentMessageBus _messageBus;
    private readonly IServiceScopeFactory _scopeFactory;

    public DbGenerationJobManager(
        ILogger<DbGenerationJobManager> logger,
        AgentMessageBus messageBus,
        IServiceScopeFactory scopeFactory,
        string dbPath = "data/codeMRI.db")
    {
        _logger = logger;
        _messageBus = messageBus;
        _scopeFactory = scopeFactory;

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

    public async Task<GenerationJob> StartJobAsync(string repoPath, StructureRequest request, string? connectionId = null)
    {
        // 1. Create new job
        var job = new GenerationJob
        {
            RepoPath = repoPath,
            Status = GenerationStatus.Queued,
            Message = "Duplicate check complete. Queueing job..."
        };

        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(@"
            INSERT INTO GenerationJobs (Id, RepoPath, Status, ProgressPercentage, Message, CreatedAt, LastUpdated, Error, ResultJson)
            VALUES (@Id, @RepoPath, @Status, @ProgressPercentage, @Message, @CreatedAt, @LastUpdated, @Error, @ResultJson)",
            job);

        // 2. Spawn background execution
        _ = Task.Run(() => RunJobAsync(job.Id, repoPath, request, connectionId));

        return job;
    }

    public async Task<GenerationJob?> GetJobAsync(string jobId)
    {
        using var connection = new SqliteConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<GenerationJob>("SELECT * FROM GenerationJobs WHERE Id = @Id",
            new { Id = jobId });
    }

    public async Task<List<GenerationJob>> ListActiveJobsAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        var jobs = await connection.QueryAsync<GenerationJob>(
            "SELECT * FROM GenerationJobs WHERE Status IN (@Queued, @Processing) ORDER BY CreatedAt DESC",
            new { GenerationStatus.Queued, GenerationStatus.Processing });
        return jobs.AsList();
    }

    public async Task CancelJobAsync(string jobId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(
            "UPDATE GenerationJobs SET Status = @Status, LastUpdated = @Now WHERE Id = @Id AND Status IN (@Queued, @Processing)",
            new
            {
                Status = GenerationStatus.Cancelled,
                Now = DateTime.UtcNow,
                Id = jobId,
                GenerationStatus.Queued,
                GenerationStatus.Processing
            });
    }

    private void InitializeDb()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS GenerationJobs (
                Id TEXT PRIMARY KEY,
                RepoPath TEXT,
                Status INTEGER,
                ProgressPercentage INTEGER,
                Message TEXT,
                CreatedAt TEXT,
                LastUpdated TEXT,
                Error TEXT,
                ResultJson TEXT
            )");
    }

    private async Task RunJobAsync(string jobId, string repoPath, StructureRequest request, string? connectionId)
    {
        _logger.LogInformation("Starting generation job {JobId} for {RepoPath}", jobId, repoPath);

        using var scope = _scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ICodeWikiOrchestrator>();
        // Using CancellationToken logic similar to IngestionJobManager would be ideal here

        try
        {
            await UpdateJobStatusAsync(jobId, GenerationStatus.Processing, 0, "Starting generation...");

            var progress = new Progress<ProgressInfo>(async p =>
            {
                await UpdateJobStatusAsync(jobId, GenerationStatus.Processing, p.Percentage, p.Message);
            });

            // Pass token if we implement cancellation checking
            var result = await orchestrator.GenerateAdvancedWikiAsync(repoPath, new RepositoryInfo { Name = Path.GetFileName(repoPath) }, progress);

            // Save result
            var json = JsonSerializer.Serialize(result);
            using var connection = new SqliteConnection(_connectionString);
            await connection.ExecuteAsync(
                "UPDATE GenerationJobs SET Status = @Status, ProgressPercentage = 100, Message = 'Complete', ResultJson = @Result, LastUpdated = @Now WHERE Id = @Id",
                new { Status = GenerationStatus.Completed, Result = json, Now = DateTime.UtcNow, Id = jobId });
            
            _logger.LogInformation("Job {JobId} completed successfully", jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed", jobId);
            using var connection = new SqliteConnection(_connectionString);
            await connection.ExecuteAsync(
                "UPDATE GenerationJobs SET Status = @Status, Error = @Error, LastUpdated = @Now WHERE Id = @Id",
                new { Status = GenerationStatus.Failed, Error = ex.Message, Now = DateTime.UtcNow, Id = jobId });
        }
    }

    private async Task UpdateJobStatusAsync(string jobId, GenerationStatus status, int percentage, string message)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(
            "UPDATE GenerationJobs SET Status = @Status, ProgressPercentage = @Pct, Message = @Msg, LastUpdated = @Now WHERE Id = @Id",
            new { Status = status, Pct = percentage, Msg = message, Now = DateTime.UtcNow, Id = jobId });
    }
}
