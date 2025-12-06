using System.Net.Http.Json;
using System.Text.Json;
using codeMRI.Server.Api;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace codeMRI.Frontend.Services;

public class WikiApiClient
{
    private readonly HttpClient _httpClient;

    public WikiApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> IngestRepoAsync(string repoPath, bool force = false, bool delete = false)
    {
        var response = await _httpClient.PostAsJsonAsync("api/ingest", new IngestRequest
        {
            RepoPath = repoPath,
            Force = force,
            Delete = delete
        });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("message").GetString() ?? "Ingestion complete";
    }

    public async Task<IngestionStatusResponse> GetIngestionStatusAsync(string repoPath)
    {
        var response = await _httpClient.GetAsync($"api/ingest/status?repoPath={Uri.EscapeDataString(repoPath)}");
        if (!response.IsSuccessStatusCode) return new IngestionStatusResponse { Status = "unknown" };
        return await response.Content.ReadFromJsonAsync<IngestionStatusResponse>() ?? new IngestionStatusResponse();
    }

    public async Task<WikiStructure> GenerateStructureAsync(string repoPath)
    {
        var response =
            await _httpClient.PostAsJsonAsync("api/wiki/structure", new StructureRequest { RepoPath = repoPath });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WikiStructure>() ?? new WikiStructure();
    }

    public async Task<WikiPage> GeneratePageAsync(string repoPath, string title, List<string> filePaths)
    {
        var response = await _httpClient.PostAsJsonAsync("api/wiki/page", new PageGenerationRequest
        {
            RepoPath = repoPath,
            Title = title,
            FilePaths = filePaths
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WikiPage>() ?? new WikiPage();
    }

    public async IAsyncEnumerable<string> ChatAsync(string query, List<ChatMessage> history)
    {
        var request = new ChatRequest { Query = query, History = history };
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/chat");
        httpRequest.Content = JsonContent.Create(request);
        httpRequest.SetBrowserResponseStreamingEnabled(true);

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        var buffer = new byte[1024];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            var chunk = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
            yield return chunk;
        }
    }
}