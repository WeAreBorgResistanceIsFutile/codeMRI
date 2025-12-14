using System.Diagnostics;

namespace codeMRI.Infrastructure.Services;

public static class GitHelper
{
    public static async Task<string> CloneRepositoryAsync(string gitUrl, string? targetDir = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(targetDir))
        {
            targetDir = Path.Combine(Path.GetTempPath(), "codeMRI_" + Guid.NewGuid().ToString("N"));
        }

        Directory.CreateDirectory(targetDir);

        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"clone {gitUrl} .",
            WorkingDirectory = targetDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        
        var output = new List<string>();
        var errors = new List<string>();
        
        process.OutputDataReceived += (sender, e) => 
        {
            if (!string.IsNullOrEmpty(e.Data)) output.Add(e.Data);
        };
        process.ErrorDataReceived += (sender, e) => 
        {
            if (!string.IsNullOrEmpty(e.Data)) errors.Add(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var errorMessage = string.Join("\n", errors);
            throw new Exception($"Git clone failed with exit code {process.ExitCode}: {errorMessage}");
        }

        return targetDir;
    }

    public static bool IsGitUrl(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        return input.StartsWith("http", StringComparison.OrdinalIgnoreCase) || 
               input.StartsWith("git@") || 
               input.EndsWith(".git", StringComparison.OrdinalIgnoreCase);
    }
}
