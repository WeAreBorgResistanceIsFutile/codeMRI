using codeMRI.Core.Interfaces;

namespace codeMRI.Core.Services;

/// <summary>
///     Implementation of ILLMInvocationContext using AsyncLocal to persist context across call stacks
/// </summary>
public class LLMInvocationContext : ILLMInvocationContext
{
    private static readonly AsyncLocal<string?> _jobId = new();
    private static readonly AsyncLocal<string?> _repoPath = new();
    private static readonly AsyncLocal<string?> _componentId = new();
    private static readonly AsyncLocal<bool> _force = new();

    public string? JobId
    {
        get => _jobId.Value;
        set => _jobId.Value = value;
    }

    public string? RepoPath
    {
        get => _repoPath.Value;
        set => _repoPath.Value = value;
    }

    public string? ComponentId
    {
        get => _componentId.Value;
        set => _componentId.Value = value;
    }

    public bool Force
    {
        get => _force.Value;
        set => _force.Value = value;
    }
}
