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
        _connectionString = connectionString;
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
        var json = await connection.QuerySingleOrDefaultAsync<string>(
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

    private void InitializeDatabase()
    {
        var dbPath = _connectionString.Replace("Data Source=", "").Trim();
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS Repositories (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RepoPath TEXT NOT NULL UNIQUE
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
        ");

        // Enable foreign keys
        connection.Execute("PRAGMA foreign_keys = ON;");
    }

    private async Task<int> GetOrCreateRepoIdAsync(IDbConnection connection, string repoPath)
    {
        var id = await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT Id FROM Repositories WHERE RepoPath = @RepoPath", new { RepoPath = repoPath });

        if (id.HasValue) return id.Value;

        await connection.ExecuteAsync(
            "INSERT INTO Repositories (RepoPath) VALUES (@RepoPath)", new { RepoPath = repoPath });

        return await connection.QuerySingleAsync<int>(
            "SELECT Id FROM Repositories WHERE RepoPath = @RepoPath", new { RepoPath = repoPath });
    }

    private async Task<int?> GetRepoIdAsync(IDbConnection connection, string repoPath)
    {
        return await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT Id FROM Repositories WHERE RepoPath = @RepoPath", new { RepoPath = repoPath });
    }
}