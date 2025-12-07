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
}
