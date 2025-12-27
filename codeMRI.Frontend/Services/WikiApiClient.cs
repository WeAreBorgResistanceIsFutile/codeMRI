using System.Net;
using System.Net.Http.Json;
using codeMRI.Core.Models;
using codeMRI.Core.Interfaces;
using codeMRI.Agents.Models;

namespace codeMRI.Frontend.Services;

public class WikiApiClient
{
    private readonly HttpClient _http;
    private static readonly System.Text.Json.JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

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
        }, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WikiStructure>(_jsonOptions)
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
        }, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WikiStructure>(_jsonOptions)
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
        }, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WikiPage>(_jsonOptions)
               ?? throw new Exception("Failed to deserialize page");
    }

    public async Task<List<string>> GetRepositoriesAsync()
    {
        return await _http.GetFromJsonAsync<List<string>>("api/Wiki/repositories", _jsonOptions)
               ?? new List<string>();
    }

    public async Task<List<RepositorySummary>> GetRepositorySummariesAsync()
    {
        return await _http.GetFromJsonAsync<List<RepositorySummary>>("api/Wiki/repositories-summary", _jsonOptions)
               ?? new List<RepositorySummary>();
    }

    public async Task DeleteRepositoryAsync(string repoPath)
    {
        var encoded = Uri.EscapeDataString(repoPath);
        var response = await _http.DeleteAsync($"api/Wiki/repository?repoPath={encoded}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<ChatResponse> ChatAsync(List<ChatMessage> history, string? repoPath = null)
    {
        var dtoHistory = history.Select(m => new ChatMessageDto { Role = m.Role, Content = m.Content }).ToList();
        var response = await _http.PostAsJsonAsync("api/Chat", new ChatRequest { History = dtoHistory, RepoPath = repoPath }, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatResponse>(_jsonOptions)
               ?? throw new Exception("Failed to deserialize chat response");
    }

    public async Task<RepositoryStatusResponse> GetRepositoryStatusAsync(string repoPath)
    {
        var encoded = Uri.EscapeDataString(repoPath);
        return await _http.GetFromJsonAsync<RepositoryStatusResponse>($"api/Wiki/repository-status?repoPath={encoded}", _jsonOptions)
               ?? throw new Exception("Failed to get repository status");
    }

    public async Task<WikiStructure?> GetNavigationAsync(string repoPath)
    {
        var encoded = Uri.EscapeDataString(repoPath);
        var result = await _http.GetAsync($"api/Wiki/navigation?repoPath={encoded}");

        if (result.StatusCode == HttpStatusCode.NoContent) return null;

        result.EnsureSuccessStatusCode();

        return await result.Content.ReadFromJsonAsync<WikiStructure>(_jsonOptions)
               ?? throw new Exception("Failed to get navigation structure");
    }


    public async Task<IngestionJob> StartIngestionAsync(string gitUrl, bool forceIngest = true, AudienceType audience = AudienceType.Developer)
    {
        var request = new IngestionRequest { Url = gitUrl, Audience = audience, ForceIngest = forceIngest };
        var response = await _http.PostAsJsonAsync("api/Wiki/ingest", request, _jsonOptions);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<StartIngestionResponse>(_jsonOptions)
                     ?? throw new Exception("Failed to start ingestion");

        return new IngestionJob { Id = result.JobId, Status = Enum.Parse<IngestionStatus>(result.Status) };
    }

    public async Task<IngestionJob?> GetIngestionJobAsync(string jobId)
    {
        return await _http.GetFromJsonAsync<IngestionJob>($"api/Wiki/ingestion/{jobId}", _jsonOptions);
    }

    public async Task CancelIngestionAsync(string jobId)
    {
        var response = await _http.DeleteAsync($"api/Wiki/ingestion/{jobId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<IngestionJob> RestartIngestionAsync(string jobId, bool forceRegenerate)
    {
        var response = await _http.PostAsync($"api/Wiki/ingestion/{jobId}/restart?forceRegenerate={forceRegenerate}", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IngestionJob>(_jsonOptions)
               ?? throw new Exception("Failed to restart ingestion");
    }


    public async Task<List<IngestionJob>> ListActiveIngestionsAsync()
    {
        var response = await _http.GetAsync("api/Wiki/ingestions/active");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return System.Text.Json.JsonSerializer.Deserialize<List<IngestionJob>>(json, _jsonOptions)
               ?? new List<IngestionJob>();
    }

    public async Task<List<IngestionJob>> ListAllIngestionsAsync()
    {
        var response = await _http.GetAsync("api/Wiki/ingestions/all");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return System.Text.Json.JsonSerializer.Deserialize<List<IngestionJob>>(json, _jsonOptions)
               ?? new List<IngestionJob>();
    }

    public async Task DeleteIngestionJobAsync(string jobId)
    {
        var response = await _http.DeleteAsync($"api/Wiki/ingestion-job/{jobId}");
        response.EnsureSuccessStatusCode();
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
        }, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WikiStructure>(_jsonOptions)
               ?? throw new Exception("Failed to ingest repository");
    }

    #region Benchmark API Methods

    public async Task<List<BenchmarkRun>> ListBenchmarksAsync()
    {
        return await _http.GetFromJsonAsync<List<BenchmarkRun>>("api/Wiki/benchmarks", _jsonOptions) 
               ?? new List<BenchmarkRun>();
    }

    public async Task<BenchmarkRun> GetBenchmarkAsync(string runId)
    {
        return await _http.GetFromJsonAsync<BenchmarkRun>($"api/Wiki/benchmarks/{runId}", _jsonOptions)
               ?? throw new Exception("Benchmark run not found");
    }

    public async Task<BenchmarkRun> StartBenchmarkAsync(StartBenchmarkRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/Wiki/benchmarks", request, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BenchmarkRun>(_jsonOptions)
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
        return await response.Content.ReadFromJsonAsync<BenchmarkRun>(_jsonOptions)
               ?? throw new Exception("Failed to resume benchmark");
    }

    public async Task<ModelBenchmarkComparison> CompareBenchmarksAsync(List<string> runIds)
    {
        var response = await _http.PostAsJsonAsync("api/Wiki/benchmarks/compare", runIds, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ModelBenchmarkComparison>(_jsonOptions)
               ?? throw new Exception("Failed to compare benchmarks");
    }

    public async Task<string> GetBenchmarkReportAsync(string runId)
    {
        var result = await _http.GetFromJsonAsync<Dictionary<string, string>>($"api/Wiki/benchmarks/{runId}/report", _jsonOptions);
        return result != null && result.TryGetValue("report", out var report) ? report : string.Empty;
    }

    #endregion
    public async Task<List<GenerationJob>> ListAllGenerationsAsync()
    {
        return await _http.GetFromJsonAsync<List<GenerationJob>>("api/Wiki/generations/all", _jsonOptions)
               ?? new List<GenerationJob>();
    }

    public async Task DeleteGenerationJobAsync(string jobId)
    {
        var response = await _http.DeleteAsync($"api/Wiki/generation-job/{jobId}");
        response.EnsureSuccessStatusCode();
    }
}