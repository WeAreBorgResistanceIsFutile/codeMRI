namespace codeMRI.Core.Interfaces;

/// <summary>
///     Provides context for LLM invocations (JobId, RepoPath, ComponentId)
/// </summary>
public interface ILLMInvocationContext
{
    string? JobId { get; set; }
    string? RepoPath { get; set; }
    string? ComponentId { get; set; }
    bool Force { get; set; }
}
