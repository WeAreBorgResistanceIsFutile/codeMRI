using codeMRI.Agents.Interfaces;
using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace codeMRI.Agents.Agents;

public abstract class BaseAgent : IAgent
{
    protected const int MaxTokens = 2000;
    protected const int MaxComplexity = 10;
    protected readonly IASTServiceClient? _astService;
    protected readonly ILogger _logger;
    protected readonly AgentMessageBus _messageBus;

    protected BaseAgent(
        AgentMessageBus messageBus,
        ILogger logger,
        IASTServiceClient? astService = null)
    {
        _messageBus = messageBus;
        _logger = logger;
        _astService = astService;
    }

    public string Id { get; } = Guid.NewGuid().ToString();
    public abstract string Role { get; }

    public abstract Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken);

    public virtual bool CanHandle(AgentTask task)
    {
        return task.Type == Role;
    }

    public virtual async Task<DelegationRequest?> ShouldDelegate(AgentTask task, CancellationToken cancellationToken)
    {
        if (task.Payload is string code && !string.IsNullOrWhiteSpace(code))
        {
            // Avoid delegating if already delegated to prevent infinite loops if logic is naive
            if (task.Metadata.ContainsKey("IsDelegated")) return null;

            var complexity = await CalculateComplexity(code, cancellationToken);

            if (complexity.TokenCount > MaxTokens)
                return CreateDelegationRequest(task, "TokenCount", complexity.TokenCount);

            if (complexity.CyclomaticComplexity > MaxComplexity)
                return CreateDelegationRequest(task, "CyclomaticComplexity", complexity.CyclomaticComplexity);
        }

        return null;
    }

    protected virtual DelegationRequest CreateDelegationRequest(AgentTask task, string reasonType, int value)
    {
        return new DelegationRequest
        {
            TaskId = task.Id,
            TargetAgentType = Role,
            SubTask = task with
            {
                Id = Guid.NewGuid().ToString(),
                Metadata = new Dictionary<string, string>(task.Metadata)
                {
                    ["IsDelegated"] = "true",
                    ["ParentTaskId"] = task.Id
                }
            },
            Reason = $"Delegation required due to high complexity ({reasonType}: {value})"
        };
    }

    protected virtual async Task<CodeComplexityMetrics> CalculateComplexity(string code,
        CancellationToken cancellationToken)
    {
        // 1. Try using AST Service
        if (_astService != null)
            try
            {
                var result = await _astService.ParseCodeAsync(code, "csharp", "", cancellationToken);
                if (result?.Metrics != null)
                {
                    var json = JsonSerializer.Serialize(result.Metrics);
                    var metrics = JsonSerializer.Deserialize<CodeComplexityMetrics>(json);
                    if (metrics != null)
                    {
                        return metrics;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AST Service failed, falling back to basic complexity calculation");
            }

        // 2. Fallback / Basic Heuristic
        var tokenCount = code.Length / 4;

        // Fix: Use StringSplitOptions.None to preserve empty lines as empty strings
        var lines = code.Split(new[] { '\r', '\n' }, StringSplitOptions.None);

        var complexity = lines.Sum(l =>
        {
            if (string.IsNullOrWhiteSpace(l)) return 0;

            var count = 0;
            if (l.Contains("if")) count++;
            if (l.Contains("for")) count++;
            if (l.Contains("while")) count++;
            if (l.Contains("case")) count++;
            if (l.Contains("catch")) count++;
            if (l.Contains("&&")) count++;
            if (l.Contains("||")) count++;
            return count;
        });

        var nesting = lines
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.TakeWhile(char.IsWhiteSpace).Count() / 4)
            .DefaultIfEmpty(0)
            .Max();

        return new CodeComplexityMetrics
        {
            TokenCount = tokenCount,
            CyclomaticComplexity = complexity,
            NestingDepth = nesting
        };
    }

    protected async Task PublishStatusAsync(string status, string taskId)
    {
        await _messageBus.PublishAsync(new AgentMessage
        {
            SenderId = Id,
            MessageType = "AgentStatus",
            Content = new { Status = status, TaskId = taskId }
        });
    }
}