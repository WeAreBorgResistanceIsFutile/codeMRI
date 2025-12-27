using System.Text.Json;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Dapper;
using Microsoft.Data.Sqlite;

namespace codeMRI.Infrastructure.Services;

/// <summary>
///     SQLite-based repository for persisting benchmark data.
/// </summary>
public class BenchmarkRepository : IBenchmarkRepository
{
    private readonly string _connectionString;

    public BenchmarkRepository(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        builder["Default Timeout"] = 30;
        _connectionString = builder.ToString();
        InitializeDatabase();
    }

    public async Task<BenchmarkRun> CreateAsync(BenchmarkRun run, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var json = JsonSerializer.Serialize(run);

        await connection.ExecuteAsync(@"
            INSERT INTO BenchmarkRuns (Id, Name, RepositoryUrl, Status, JsonContent, CreatedAt)
            VALUES (@Id, @Name, @RepositoryUrl, @Status, @Json, @CreatedAt)",
            new
            {
                run.Id,
                run.Name,
                run.RepositoryUrl,
                Status = run.Status.ToString(),
                Json = json,
                CreatedAt = DateTime.UtcNow
            });

        return run;
    }

    public async Task<BenchmarkRun?> GetByIdAsync(string runId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var json = await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT JsonContent FROM BenchmarkRuns WHERE Id = @RunId",
            new { RunId = runId });

        return json == null ? null : JsonSerializer.Deserialize<BenchmarkRun>(json);
    }

    public async Task<List<BenchmarkRun>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var results = await connection.QueryAsync<string>(
            "SELECT JsonContent FROM BenchmarkRuns ORDER BY CreatedAt DESC");

        return results
            .Select(json => JsonSerializer.Deserialize<BenchmarkRun>(json)!)
            .Where(run => run != null)
            .ToList();
    }

    public async Task<List<BenchmarkRun>> GetByRepositoryAsync(string repositoryUrl, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var results = await connection.QueryAsync<string>(
            "SELECT JsonContent FROM BenchmarkRuns WHERE RepositoryUrl = @Url ORDER BY CreatedAt DESC",
            new { Url = repositoryUrl });

        return results
            .Select(json => JsonSerializer.Deserialize<BenchmarkRun>(json)!)
            .Where(run => run != null)
            .ToList();
    }

    public async Task<BenchmarkRun?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var json = await connection.QueryFirstOrDefaultAsync<string>(
            "SELECT JsonContent FROM BenchmarkRuns WHERE Status = 'Running' ORDER BY CreatedAt DESC LIMIT 1");

        return json == null ? null : JsonSerializer.Deserialize<BenchmarkRun>(json);
    }

    public async Task UpdateAsync(BenchmarkRun run, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var json = JsonSerializer.Serialize(run);

        await connection.ExecuteAsync(@"
            UPDATE BenchmarkRuns 
            SET Name = @Name, Status = @Status, JsonContent = @Json, UpdatedAt = @UpdatedAt
            WHERE Id = @Id",
            new
            {
                run.Id,
                run.Name,
                Status = run.Status.ToString(),
                Json = json,
                UpdatedAt = DateTime.UtcNow
            });
    }

    public async Task DeleteAsync(string runId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(
            "DELETE FROM BenchmarkRuns WHERE Id = @RunId",
            new { RunId = runId });
        // Cascade will delete PageBenchmarks and BenchmarkMetrics
    }

    public async Task AddPageBenchmarkAsync(string runId, PageBenchmark pageBenchmark, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var json = JsonSerializer.Serialize(pageBenchmark);

        await connection.ExecuteAsync(@"
            INSERT INTO PageBenchmarks (RunId, PageId, ModuleId, QualityScore, JsonContent)
            VALUES (@RunId, @PageId, @ModuleId, @QualityScore, @Json)",
            new
            {
                RunId = runId,
                pageBenchmark.PageId,
                pageBenchmark.ModuleId,
                QualityScore = pageBenchmark.OverallQualityScore,
                Json = json
            });
    }

    public async Task<List<PageBenchmark>> GetPageBenchmarksAsync(string runId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var results = await connection.QueryAsync<string>(
            "SELECT JsonContent FROM PageBenchmarks WHERE RunId = @RunId",
            new { RunId = runId });

        return results
            .Select(json => JsonSerializer.Deserialize<PageBenchmark>(json)!)
            .Where(p => p != null)
            .ToList();
    }

    public async Task AddMetricsAsync(string runId, BenchmarkMetrics metrics, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var json = JsonSerializer.Serialize(metrics);

        await connection.ExecuteAsync(@"
            INSERT INTO BenchmarkMetrics (RunId, MetricsId, ModelName, TaskType, JsonContent)
            VALUES (@RunId, @MetricsId, @ModelName, @TaskType, @Json)",
            new
            {
                RunId = runId,
                MetricsId = metrics.Id,
                metrics.ModelName,
                metrics.TaskType,
                Json = json
            });
    }

    public async Task<List<BenchmarkMetrics>> GetMetricsAsync(string runId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var results = await connection.QueryAsync<string>(
            "SELECT JsonContent FROM BenchmarkMetrics WHERE RunId = @RunId",
            new { RunId = runId });

        return results
            .Select(json => JsonSerializer.Deserialize<BenchmarkMetrics>(json)!)
            .Where(m => m != null)
            .ToList();
    }

    public async Task<ModelBenchmarkComparison> SaveComparisonAsync(ModelBenchmarkComparison comparison, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var json = JsonSerializer.Serialize(comparison);

        await connection.ExecuteAsync(@"
            INSERT INTO BenchmarkComparisons (Id, RepositoryUrl, JsonContent, CreatedAt)
            VALUES (@Id, @RepositoryUrl, @Json, @CreatedAt)
            ON CONFLICT(Id) DO UPDATE SET JsonContent = @Json",
            new
            {
                comparison.Id,
                comparison.RepositoryUrl,
                Json = json,
                CreatedAt = DateTime.UtcNow
            });

        return comparison;
    }

    public async Task<ModelBenchmarkComparison?> GetComparisonAsync(string comparisonId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var json = await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT JsonContent FROM BenchmarkComparisons WHERE Id = @Id",
            new { Id = comparisonId });

        return json == null ? null : JsonSerializer.Deserialize<ModelBenchmarkComparison>(json);
    }

    public async Task<List<ModelBenchmarkComparison>> GetAllComparisonsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var results = await connection.QueryAsync<string>(
            "SELECT JsonContent FROM BenchmarkComparisons ORDER BY CreatedAt DESC");

        return results
            .Select(json => JsonSerializer.Deserialize<ModelBenchmarkComparison>(json)!)
            .Where(c => c != null)
            .ToList();
    }

    public async Task DeleteComparisonAsync(string comparisonId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(
            "DELETE FROM BenchmarkComparisons WHERE Id = @Id",
            new { Id = comparisonId });
    }

    private void InitializeDatabase()
    {
        var builder = new SqliteConnectionStringBuilder(_connectionString);
        var dbPath = builder.DataSource;
        if (!string.IsNullOrWhiteSpace(dbPath) && dbPath != ":memory:")
        {
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS BenchmarkRuns (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                RepositoryUrl TEXT NOT NULL,
                Status TEXT NOT NULL,
                JsonContent TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT
            );

            CREATE TABLE IF NOT EXISTS PageBenchmarks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RunId TEXT NOT NULL,
                PageId TEXT NOT NULL,
                ModuleId TEXT NOT NULL,
                QualityScore REAL,
                JsonContent TEXT NOT NULL,
                FOREIGN KEY(RunId) REFERENCES BenchmarkRuns(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS BenchmarkMetrics (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RunId TEXT NOT NULL,
                MetricsId TEXT NOT NULL,
                ModelName TEXT NOT NULL,
                TaskType TEXT NOT NULL,
                JsonContent TEXT NOT NULL,
                FOREIGN KEY(RunId) REFERENCES BenchmarkRuns(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS BenchmarkComparisons (
                Id TEXT PRIMARY KEY,
                RepositoryUrl TEXT NOT NULL,
                JsonContent TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_benchmarkruns_status ON BenchmarkRuns(Status);
            CREATE INDEX IF NOT EXISTS idx_benchmarkruns_repo ON BenchmarkRuns(RepositoryUrl);
            CREATE INDEX IF NOT EXISTS idx_pagebenchmarks_runid ON PageBenchmarks(RunId);
            CREATE INDEX IF NOT EXISTS idx_benchmarkmetrics_runid ON BenchmarkMetrics(RunId);
        ");

        // Enable WAL mode for better concurrency
        connection.Execute("PRAGMA journal_mode=WAL;");
        connection.Execute("PRAGMA synchronous=NORMAL;");
        connection.Execute("PRAGMA foreign_keys = ON;");
    }
}
