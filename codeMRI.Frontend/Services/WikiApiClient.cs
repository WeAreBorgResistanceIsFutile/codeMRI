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

    public async Task<WikiStructure> GenerateStructureAsync(string repoPath)
    {
        var response =
            await _httpClient.PostAsJsonAsync("api/wiki/structure", new StructureRequest { RepoPath = repoPath });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WikiStructure>() ?? new WikiStructure();
    }

    public async Task<WikiPage> GeneratePageAsync(string title, List<string> filePaths)
    {
        // For MVP, we assume the backend or ingest process handles content retrieval if not passed
        // But our API expects FileContents. Since we ingested them, we might need an API to fetch file content 
        // OR let the backend handle it.
        // The current WikiController expects FileContents in the request. 
        // This means the Client needs to provide them. 
        // BUT, the client (Blazor) doesn't have access to the files directly if they are on the server (for IngestController).
        // Wait, IngestController runs on Server. Blazor runs in Browser. 
        // If the user enters a local path on the server machine, the Browser cannot read those files.
        // So the Backend must use the stored files in VectorDB or read from disk.

        // Refactoring thought: The WikiGenerationService should probably fetch content from the VectorDB/Disk 
        // if not provided. 
        // However, keeping it simple: I will modify WikiController to load content if missing. 

        var response = await _httpClient.PostAsJsonAsync("api/wiki/page", new PageGenerationRequest
        {
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
        httpRequest.SetBrowserResponseStreamingEnabled(true); // Enable streaming in WASM

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(); // This might buffer line by line
            // If the server sends raw text chunks, ReadLine might block until newline.
            // ChatController writes chunks. Ideally we read char buffer.
            // But for simplicity let's assume chunks come with newlines or we read block.
            if (!string.IsNullOrEmpty(line)) yield return line + "\n";
        }
    }
}