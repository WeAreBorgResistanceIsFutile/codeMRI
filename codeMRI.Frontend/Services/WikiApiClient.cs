using System.Net;
using System.Net.Http.Json;
using codeMRI.Core.Models;
using codeMRI.Core.Interfaces;
using codeMRI.Agents.Models;

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
            ConnectionId = connectionId,
            Audience = AudienceType.Developer // Default for now, or update signature if needed
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WikiStructure>()
               ?? throw new Exception("Failed to deserialize structure");
    }

    public async Task<WikiPage> GeneratePageAsync(string repoPath, string title, List<string> contextFiles,
        bool forceRegenerate = false)
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

    public async Task<List<RepositorySummary>> GetRepositorySummariesAsync()
    {
        return await _http.GetFromJsonAsync<List<RepositorySummary>>("api/Wiki/repositories-summary")
               ?? new List<RepositorySummary>();
    }

    public async Task<ChatResponse> ChatAsync(List<ChatMessage> history, string? repoPath = null)
    {
        var dtoHistory = history.Select(m => new ChatMessageDto { Role = m.Role, Content = m.Content }).ToList();
        var response = await _http.PostAsJsonAsync("api/Chat", new ChatRequest { History = dtoHistory, RepoPath = repoPath });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatResponse>()
               ?? throw new Exception("Failed to deserialize chat response");
    }

    public async Task<RepositoryStatusResponse> GetRepositoryStatusAsync(string repoPath)
    {
        var encoded = Uri.EscapeDataString(repoPath);
        return await _http.GetFromJsonAsync<RepositoryStatusResponse>($"api/Wiki/repository-status?repoPath={encoded}")
               ?? throw new Exception("Failed to get repository status");
    }

    public async Task<WikiStructure?> GetNavigationAsync(string repoPath)
    {
        var encoded = Uri.EscapeDataString(repoPath);
        var result = await _http.GetAsync($"api/Wiki/navigation?repoPath={encoded}");

        if (result.StatusCode == HttpStatusCode.NoContent) return null;

        result.EnsureSuccessStatusCode();

        return await result.Content.ReadFromJsonAsync<WikiStructure>()
               ?? throw new Exception("Failed to get navigation structure");
    }


    public async Task<IngestionJob> StartIngestionAsync(string gitUrl, AudienceType audience = AudienceType.Developer)
    {
        var request = new IngestionRequest { Url = gitUrl, Audience = audience };
        var response = await _http.PostAsJsonAsync("api/Wiki/ingest", request);
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
        // Create JSON options with string enum converter
        var jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        
        var response = await _http.GetAsync("api/Wiki/ingestions/active");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return System.Text.Json.JsonSerializer.Deserialize<List<IngestionJob>>(json, jsonOptions)
               ?? new List<IngestionJob>();
    }

    // Deprecated but kept for compatibility if needed (simplified wrapper)
    public async Task<IngestionResult> IngestGitRepositoryAsync(string gitUrl)
    {
        var job = await StartIngestionAsync(gitUrl);
        return new IngestionResult { Name = "", Path = "" }; // Placeholder as this flow is changing
    }

    public async Task<WikiStructure> IngestRepositoryAsync(string repoPath, bool forceRegenerate = false,
        string? connectionId = null)
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

    #region Benchmark API Methods

    public async Task<List<BenchmarkRun>> ListBenchmarksAsync()
    {
        return await _http.GetFromJsonAsync<List<BenchmarkRun>>("api/Wiki/benchmarks") 
               ?? new List<BenchmarkRun>();
    }

    public async Task<BenchmarkRun> GetBenchmarkAsync(string runId)
    {
        return await _http.GetFromJsonAsync<BenchmarkRun>($"api/Wiki/benchmarks/{runId}")
               ?? throw new Exception("Benchmark run not found");
    }

    public async Task<BenchmarkRun> StartBenchmarkAsync(StartBenchmarkRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/Wiki/benchmarks", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BenchmarkRun>()
               ?? throw new Exception("Failed to start benchmark");
    }

    public async Task DeleteBenchmarkAsync(string runId)
    {
        var response = await _http.DeleteAsync($"api/Wiki/benchmarks/{runId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task PauseBenchmarkAsync(string runId)
    {
        var response = await _http.PostAsync($"api/Wiki/benchmarks/{runId}/pause", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<BenchmarkRun> ResumeBenchmarkAsync(string runId)
    {
        var response = await _http.PostAsync($"api/Wiki/benchmarks/{runId}/resume", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BenchmarkRun>()
               ?? throw new Exception("Failed to resume benchmark");
    }

    public async Task<ModelBenchmarkComparison> CompareBenchmarksAsync(List<string> runIds)
    {
        var response = await _http.PostAsJsonAsync("api/Wiki/benchmarks/compare", runIds);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ModelBenchmarkComparison>()
               ?? throw new Exception("Failed to compare benchmarks");
    }

    public async Task<string> GetBenchmarkReportAsync(string runId)
    {
        var result = await _http.GetFromJsonAsync<Dictionary<string, string>>($"api/Wiki/benchmarks/{runId}/report");
        return result != null && result.TryGetValue("report", out var report) ? report : string.Empty;
    }

    #endregion
}