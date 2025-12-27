using System.Data;
using System.Text.Json;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Dapper;
using Microsoft.Data.Sqlite;

namespace codeMRI.Infrastructure.Services;

public class SqliteWikiRepository : IWikiRepository
{
    private readonly string _connectionString;

    public SqliteWikiRepository(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        // Default Timeout is in seconds for Microsoft.Data.Sqlite
        builder["Default Timeout"] = 30; 
        _connectionString = builder.ToString();
        InitializeDatabase();
    }

    public async Task SaveStructureAsync(string repoPath, WikiStructure structure)
    {
        using var connection = new SqliteConnection(_connectionString);
        var repoId = await GetOrCreateRepoIdAsync(connection, repoPath);
        var json = JsonSerializer.Serialize(structure);

        await connection.ExecuteAsync(@"
            INSERT INTO WikiStructures (RepoId, JsonContent)
            VALUES (@RepoId, @Json)
            ON CONFLICT(RepoId) DO UPDATE SET JsonContent = @Json",
            new { RepoId = repoId, Json = json });
    }

    public async Task<WikiStructure?> GetStructureAsync(string repoPath)
    {
        using var connection = new SqliteConnection(_connectionString);

        var repoId = await GetRepoIdAsync(connection, repoPath);
        if (repoId == null) return null;

        var json = await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT JsonContent FROM WikiStructures WHERE RepoId = @RepoId", new { RepoId = repoId });

        return json == null ? null : JsonSerializer.Deserialize<WikiStructure>(json);
    }

    public async Task DeleteStructureAsync(string repoPath)
    {
        using var connection = new SqliteConnection(_connectionString);
        var repoId = await GetRepoIdAsync(connection, repoPath);
        if (repoId != null)
            await connection.ExecuteAsync("DELETE FROM WikiStructures WHERE RepoId = @RepoId", new { RepoId = repoId });
    }

    public async Task SavePageAsync(string repoPath, WikiPage page)
    {
        using var connection = new SqliteConnection(_connectionString);
        var repoId = await GetOrCreateRepoIdAsync(connection, repoPath);
        var json = JsonSerializer.Serialize(page);

        await connection.ExecuteAsync(@"
            INSERT INTO WikiPages (RepoId, PageId, Title, JsonContent)
            VALUES (@RepoId, @PageId, @Title, @Json)
            ON CONFLICT(RepoId, PageId) DO UPDATE SET
                Title = @Title,
                JsonContent = @Json",
            new { RepoId = repoId, PageId = page.Id, page.Title, Json = json });
    }

    public async Task<WikiPage?> GetPageAsync(string repoPath, string pageId)
    {
        using var connection = new SqliteConnection(_connectionString);
        var repoId = await GetRepoIdAsync(connection, repoPath);
        if (repoId == null) return null;

        var json = await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT JsonContent FROM WikiPages WHERE RepoId = @RepoId AND PageId = @PageId",
            new { RepoId = repoId, PageId = pageId });

        return json == null ? null : JsonSerializer.Deserialize<WikiPage>(json);
    }

    public async Task<WikiPage?> GetPageByTitleAsync(string repoPath, string pageTitle)
    {
        using var connection = new SqliteConnection(_connectionString);
        var repoId = await GetRepoIdAsync(connection, repoPath);
        if (repoId == null) return null;

        // Case-insensitive search for title
        var json = await connection.QueryFirstOrDefaultAsync<string>(
            "SELECT JsonContent FROM WikiPages WHERE RepoId = @RepoId AND Title COLLATE NOCASE = @Title",
            new { RepoId = repoId, Title = pageTitle });

        return json == null ? null : JsonSerializer.Deserialize<WikiPage>(json);
    }

    public async Task DeletePageAsync(string repoPath, string pageId)
    {
        using var connection = new SqliteConnection(_connectionString);
        var repoId = await GetRepoIdAsync(connection, repoPath);
        if (repoId != null)
            await connection.ExecuteAsync(
                "DELETE FROM WikiPages WHERE RepoId = @RepoId AND PageId = @PageId",
                new { RepoId = repoId, PageId = pageId });
    }

    public async Task SaveIngestionManifestAsync(string repoPath, Dictionary<string, string> manifest)
    {
        using var connection = new SqliteConnection(_connectionString);
        var repoId = await GetOrCreateRepoIdAsync(connection, repoPath);
        var json = JsonSerializer.Serialize(manifest);

        await connection.ExecuteAsync(@"
            INSERT INTO IngestionManifests (RepoId, JsonContent)
            VALUES (@RepoId, @Json)
            ON CONFLICT(RepoId) DO UPDATE SET JsonContent = @Json",
            new { RepoId = repoId, Json = json });
    }

    public async Task<Dictionary<string, string>> GetIngestionManifestAsync(string repoPath)
    {
        using var connection = new SqliteConnection(_connectionString);
        var repoId = await GetRepoIdAsync(connection, repoPath);
        if (repoId == null) return new Dictionary<string, string>();

        var json = await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT JsonContent FROM IngestionManifests WHERE RepoId = @RepoId", new { RepoId = repoId });

        return json == null
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
    }

    public async Task DeleteIngestionManifestAsync(string repoPath)
    {
        using var connection = new SqliteConnection(_connectionString);
        var repoId = await GetRepoIdAsync(connection, repoPath);
        if (repoId != null)
            await connection.ExecuteAsync("DELETE FROM IngestionManifests WHERE RepoId = @RepoId",
                new { RepoId = repoId });
    }

    public async Task SaveIngestionProcessingStateAsync(string repoPath, IngestionProcessingState state)
    {
        using var connection = new SqliteConnection(_connectionString);
        var repoId = await GetOrCreateRepoIdAsync(connection, repoPath);
        var json = JsonSerializer.Serialize(state);

        await connection.ExecuteAsync(@"
            INSERT INTO IngestionProcessingStates (RepoId, JsonContent)
            VALUES (@RepoId, @Json)
            ON CONFLICT(RepoId) DO UPDATE SET JsonContent = @Json",
            new { RepoId = repoId, Json = json });
    }

    public async Task<IngestionProcessingState> GetIngestionProcessingStateAsync(string repoPath)
    {
        using var connection = new SqliteConnection(_connectionString);
        var repoId = await GetRepoIdAsync(connection, repoPath);
        if (repoId == null) return new IngestionProcessingState();

        var json = await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT JsonContent FROM IngestionProcessingStates WHERE RepoId = @RepoId", new { RepoId = repoId });

        return json == null
            ? new IngestionProcessingState()
            : JsonSerializer.Deserialize<IngestionProcessingState>(json) ?? new IngestionProcessingState();
    }

    public async Task DeleteIngestionProcessingStateAsync(string repoPath)
    {
        using var connection = new SqliteConnection(_connectionString);
        var repoId = await GetRepoIdAsync(connection, repoPath);
        if (repoId != null)
            await connection.ExecuteAsync("DELETE FROM IngestionProcessingStates WHERE RepoId = @RepoId",
                new { RepoId = repoId });
    }

    public async Task<List<string>> GetAllRepositoriesAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        var repos = await connection.QueryAsync<string>("SELECT RepoPath FROM Repositories ORDER BY id DESC");
        return repos.ToList();
    }

    public async Task<List<RepositorySummary>> GetAllRepositorySummariesAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        var query = @"
            SELECT r.RepoPath as Path, 
                   r.RemoteUrl,
                   (CASE WHEN w.RepoId IS NOT NULL THEN 1 ELSE 0 END) as IsIngested 
            FROM Repositories r 
            LEFT JOIN WikiStructures w ON r.Id = w.RepoId 
            ORDER BY r.Id DESC";

        var summaries = await connection.QueryAsync<RepositorySummary>(query);

        var result = summaries.ToList();
        foreach (var s in result) s.Name = Path.GetFileName(s.Path);
        // Timestamps not available in current schema, leaving default
        return result;
    }

    public async Task<List<WikiPage>> GetAllPagesAsync(string repoPath)
    {
        using var connection = new SqliteConnection(_connectionString);
        var repoId = await GetRepoIdAsync(connection, repoPath);
        if (repoId == null) return new List<WikiPage>();

        var jsonPages = await connection.QueryAsync<string>(
            "SELECT JsonContent FROM WikiPages WHERE RepoId = @RepoId ORDER BY Title",
            new { RepoId = repoId });

        return jsonPages
            .Select(json => JsonSerializer.Deserialize<WikiPage>(json))
            .Where(page => page != null)
            .Cast<WikiPage>()
            .ToList();
    }

    public async Task SetRepositoryRemoteUrlAsync(string repoPath, string remoteUrl)
    {
        using var connection = new SqliteConnection(_connectionString);
        var repoId = await GetOrCreateRepoIdAsync(connection, repoPath);
        await connection.ExecuteAsync("UPDATE Repositories SET RemoteUrl = @RemoteUrl WHERE Id = @Id",
            new { RemoteUrl = remoteUrl, Id = repoId });
    }

    public async Task DeleteRepositoryAsync(string repoPath)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync("DELETE FROM Repositories WHERE RepoPath = @RepoPath", new { RepoPath = repoPath });

        // Also delete the directory on disk if it exists
        if (Directory.Exists(repoPath))
        {
            try
            {
                Directory.Delete(repoPath, true);
            }
            catch (Exception)
            {
                // Best effort
            }
        }
    }

    private void InitializeDatabase()
    {
        var builder = new SqliteConnectionStringBuilder(_connectionString);
        var dbPath = builder.DataSource;
        if (!string.IsNullOrWhiteSpace(dbPath) && dbPath != ":memory:")
        {
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS Repositories (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RepoPath TEXT NOT NULL UNIQUE,
                RemoteUrl TEXT
            );

            CREATE TABLE IF NOT EXISTS WikiStructures (
                RepoId INTEGER PRIMARY KEY,
                JsonContent TEXT NOT NULL,
                FOREIGN KEY(RepoId) REFERENCES Repositories(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS WikiPages (
                RepoId INTEGER NOT NULL,
                PageId TEXT NOT NULL,
                Title TEXT NOT NULL,
                JsonContent TEXT NOT NULL,
                PRIMARY KEY (RepoId, PageId),
                FOREIGN KEY(RepoId) REFERENCES Repositories(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS IngestionManifests (
                RepoId INTEGER PRIMARY KEY,
                JsonContent TEXT NOT NULL,
                FOREIGN KEY(RepoId) REFERENCES Repositories(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS IngestionProcessingStates (
                RepoId INTEGER PRIMARY KEY,
                JsonContent TEXT NOT NULL,
                FOREIGN KEY(RepoId) REFERENCES Repositories(Id) ON DELETE CASCADE
            );
        ");

        // Enable WAL mode for better concurrency
        connection.Execute("PRAGMA journal_mode=WAL;");
        connection.Execute("PRAGMA synchronous=NORMAL;");

        // Enable foreign keys
        connection.Execute("PRAGMA foreign_keys = ON;");

        // Migration: Add RemoteUrl if missing
        var columns = connection.Query("PRAGMA table_info(Repositories)");
        var hasRemoteUrl = columns.Any(c => (string)c.name == "RemoteUrl");
        if (!hasRemoteUrl)
        {
            try
            {
                connection.Execute("ALTER TABLE Repositories ADD COLUMN RemoteUrl TEXT");
            }
            catch
            {
                // Ignore if it fails (e.g. race condition)
            }
        }
    }

    private async Task<int> GetOrCreateRepoIdAsync(IDbConnection connection, string repoPath)
    {
        // Use INSERT OR IGNORE to handle concurrency
        await connection.ExecuteAsync(
            "INSERT OR IGNORE INTO Repositories (RepoPath) VALUES (@RepoPath)", new { RepoPath = repoPath });

        return await connection.QuerySingleAsync<int>(
            "SELECT Id FROM Repositories WHERE RepoPath = @RepoPath", new { RepoPath = repoPath });
    }

    private async Task<int?> GetRepoIdAsync(IDbConnection connection, string repoPath)
    {
        return await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT Id FROM Repositories WHERE RepoPath = @RepoPath", new { RepoPath = repoPath });
    }
}