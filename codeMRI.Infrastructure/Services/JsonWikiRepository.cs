using System.Text.Json;
using codeMRI.Core.Interfaces;
using codeMRI.Shared.Models;

namespace codeMRI.Infrastructure.Services;

public class JsonWikiRepository : IWikiRepository
{
    private readonly string _basePath;

    public JsonWikiRepository()
    {
        // Base path for saving wiki data. Ensure this directory exists or create it.
        _basePath = Path.Combine(Directory.GetCurrentDirectory(), "data", "wiki");
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
    }

    private string GetRepoStoragePath(string repoPath)
    {
        // Encode the repo path to be safe for file system
        var safeName = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(repoPath))
            .Replace('/', '_').Replace('+', '-').Replace('=', '0'); // URL-safe base64ish
        
        var path = Path.Combine(_basePath, safeName);
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        return path;
    }

    public async Task SaveStructureAsync(string repoPath, WikiStructure structure)
    {
        var path = Path.Combine(GetRepoStoragePath(repoPath), "structure.json");
        var json = JsonSerializer.Serialize(structure, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }

    public async Task<WikiStructure?> GetStructureAsync(string repoPath)
    {
        var path = Path.Combine(GetRepoStoragePath(repoPath), "structure.json");
        if (!File.Exists(path)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<WikiStructure>(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task DeleteStructureAsync(string repoPath)
    {
        var dir = GetRepoStoragePath(repoPath);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }
        await Task.CompletedTask;
    }

    public async Task SavePageAsync(string repoPath, WikiPage page)
    {
        var pagesDir = Path.Combine(GetRepoStoragePath(repoPath), "pages");
        if (!Directory.Exists(pagesDir)) Directory.CreateDirectory(pagesDir);

        // Use Page ID for filename
        var path = Path.Combine(pagesDir, $"{page.Id}.json");
        var json = JsonSerializer.Serialize(page, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }

    public async Task<WikiPage?> GetPageAsync(string repoPath, string pageId)
    {
        var path = Path.Combine(GetRepoStoragePath(repoPath), "pages", $"{pageId}.json");
        if (!File.Exists(path)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<WikiPage>(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task<WikiPage?> GetPageByTitleAsync(string repoPath, string pageTitle)
    {
        var pagesDir = Path.Combine(GetRepoStoragePath(repoPath), "pages");
        if (!Directory.Exists(pagesDir)) return null;

        var files = Directory.GetFiles(pagesDir, "*.json");
        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var page = JsonSerializer.Deserialize<WikiPage>(json);
                if (page != null && string.Equals(page.Title, pageTitle, StringComparison.OrdinalIgnoreCase))
                {
                    return page;
                }
            }
            catch
            {
                // Ignore corrupted files
            }
        }
        return null;
    }

    public Task DeletePageAsync(string repoPath, string pageId)
    {
        var path = Path.Combine(GetRepoStoragePath(repoPath), "pages", $"{pageId}.json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        return Task.CompletedTask;
    }

    public async Task SaveIngestionManifestAsync(string repoPath, Dictionary<string, string> manifest)
    {
        var path = Path.Combine(GetRepoStoragePath(repoPath), "manifest.json");
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }

    public async Task<Dictionary<string, string>> GetIngestionManifestAsync(string repoPath)
    {
        var path = Path.Combine(GetRepoStoragePath(repoPath), "manifest.json");
        if (!File.Exists(path)) return new Dictionary<string, string>();

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    public Task DeleteIngestionManifestAsync(string repoPath)
    {
        var path = Path.Combine(GetRepoStoragePath(repoPath), "manifest.json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        return Task.CompletedTask;
    }
}
