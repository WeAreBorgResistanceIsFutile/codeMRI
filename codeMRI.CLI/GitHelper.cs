using System.Diagnostics;

namespace codeMRI.CLI;

public static class GitHelper
{
    public static async Task<string> CloneRepositoryAsync(string gitUrl, string? targetDir = null)
    {
        if (string.IsNullOrEmpty(targetDir))
        {
            targetDir = Path.Combine(Path.GetTempPath(), "codeMRI_" + Guid.NewGuid().ToString("N"));
        }

        Directory.CreateDirectory(targetDir);

        Console.WriteLine($"Cloning {gitUrl} to {targetDir}...");

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
        
        process.OutputDataReceived += (sender, e) => 
        {
            if (!string.IsNullOrEmpty(e.Data)) Console.WriteLine($"[GIT] {e.Data}");
        };
        process.ErrorDataReceived += (sender, e) => 
        {
             if (!string.IsNullOrEmpty(e.Data)) Console.WriteLine($"[GIT] {e.Data}");
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Git clone failed with exit code {process.ExitCode}");
        }

        Console.WriteLine("Clone successful.");
        return targetDir;
    }

    public static bool IsGitUrl(string input)
    {
        return input.StartsWith("http") || input.StartsWith("git@") || input.EndsWith(".git");
    }
}
