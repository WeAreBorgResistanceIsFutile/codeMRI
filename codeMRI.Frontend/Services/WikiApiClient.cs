using System.Net.Http.Json;
using codeMRI.Server.Api;

namespace codeMRI.Frontend.Services;

public class WikiApiClient
{
    private readonly HttpClient _http;

    public WikiApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<WikiStructure> GenerateStructureAsync(string repoPath)
    {
        var response = await _http.PostAsJsonAsync("api/Wiki/structure", new StructureRequest 
        { 
            RepoPath = repoPath,
            ForceRegenerate = false // Default to cached
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WikiStructure>() 
               ?? throw new Exception("Failed to deserialize structure");
    }

    public async Task<WikiStructure> GenerateAdvancedStructureAsync(string repoPath, string? connectionId = null)
    {
        var response = await _http.PostAsJsonAsync("api/Wiki/generate-advanced", new StructureRequest 
        { 
            RepoPath = repoPath,
            ForceRegenerate = true, 
            ConnectionId = connectionId
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WikiStructure>() 
               ?? throw new Exception("Failed to deserialize structure");
    }

    public async Task<WikiPage> GeneratePageAsync(string repoPath, string title, List<string> contextFiles, bool forceRegenerate = false)
    {
        var response = await _http.PostAsJsonAsync("api/Wiki/page", new PageGenerationRequest 
        { 
            RepoPath = repoPath,
            Title = title,
            FilePaths = contextFiles ?? new List<string>(),
            ForceRegenerate = forceRegenerate
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WikiPage>() 
               ?? throw new Exception("Failed to deserialize page");
    }
    
    public async Task<List<string>> GetRepositoriesAsync()
    {
         return await _http.GetFromJsonAsync<List<string>>("api/Wiki/repositories") 
                ?? new List<string>();
    }

    public async Task<string> ChatAsync(List<ChatMessage> history)
    {
        var response = await _http.PostAsJsonAsync("api/Chat", new ChatRequest { History = history });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<RepositoryStatusResponse> GetRepositoryStatusAsync(string repoPath)
    {
        var encoded = Uri.EscapeDataString(repoPath);
        return await _http.GetFromJsonAsync<RepositoryStatusResponse>($"api/Wiki/repository-status?repoPath={encoded}")
               ?? throw new Exception("Failed to get repository status");
    }

    public async Task<WikiStructure> GetNavigationAsync(string repoPath)
    {
        var encoded = Uri.EscapeDataString(repoPath);
        return await _http.GetFromJsonAsync<WikiStructure>($"api/Wiki/navigation/{encoded}")
               ?? throw new Exception("Failed to get navigation structure");
    }

    
    public async Task<IngestionJob> StartIngestionAsync(string gitUrl)
    {
        var response = await _http.PostAsJsonAsync("api/Wiki/ingest", new { Url = gitUrl });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<StartIngestionResponse>()
               ?? throw new Exception("Failed to start ingestion");
        
        return new IngestionJob { Id = result.JobId, Status = Enum.Parse<IngestionStatus>(result.Status) };
    }

    public async Task<IngestionJob?> GetIngestionJobAsync(string jobId)
    {
         return await _http.GetFromJsonAsync<IngestionJob>($"api/Wiki/ingestion/{jobId}");
    }

    public async Task CancelIngestionAsync(string jobId)
    {
        var response = await _http.DeleteAsync($"api/Wiki/ingestion/{jobId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<IngestionJob>> ListActiveIngestionsAsync()
    {
         return await _http.GetFromJsonAsync<List<IngestionJob>>("api/Wiki/ingestions/active") 
                ?? new List<IngestionJob>();
    }

    // Deprecated but kept for compatibility if needed (simplified wrapper)
    public async Task<IngestionResult> IngestGitRepositoryAsync(string gitUrl)
    {
        var job = await StartIngestionAsync(gitUrl);
        return new IngestionResult { Name = "", Path = "" }; // Placeholder as this flow is changing
    }

    public async Task<WikiStructure> IngestRepositoryAsync(string repoPath, bool forceRegenerate = false, string? connectionId = null)
    {
        var response = await _http.PostAsJsonAsync("api/Wiki/generate-advanced", new StructureRequest 
        { 
            RepoPath = repoPath,
            ForceRegenerate = forceRegenerate,
            ConnectionId = connectionId
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WikiStructure>()
               ?? throw new Exception("Failed to ingest repository");
    }
}

public class StartIngestionResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class IngestionResult
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

// Replicate Enum/Model on Client (or move to shared project, but for now duplicate)
public enum IngestionStatus
{
    Queued,
    Cloning,
    Analyzing,
    Generating,
    Completed,
    Failed,
    Cancelling,
    Cancelled
}

public class IngestionJob
{
    public string Id { get; set; } = string.Empty;
    public string RepoUrl { get; set; } = string.Empty;
    public string RepoPath { get; set; } = string.Empty;
    public string RepoName { get; set; } = string.Empty;
    public IngestionStatus Status { get; set; }
    public int ProgressPercentage { get; set; }
    public string CurrentPhase { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string WorkerId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }
    public string? Error { get; set; }
}
