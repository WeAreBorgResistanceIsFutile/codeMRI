using codeMRI.Agents.Interfaces;
using codeMRI.Agents.Models;
using codeMRI.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace codeMRI.Agents.Agents;

public abstract class BaseAgent : IAgent
{
    protected readonly ILogger _logger;
    protected readonly Services.AgentMessageBus _messageBus;
    protected readonly IASTServiceClient? _astService;

    protected const int MaxTokens = 2000;
    protected const int MaxComplexity = 10;

    public string Id { get; } = Guid.NewGuid().ToString();
    public abstract string Role { get; }

    protected BaseAgent(
        Services.AgentMessageBus messageBus, 
        ILogger logger,
        IASTServiceClient? astService = null)
    {
        _messageBus = messageBus;
        _logger = logger;
        _astService = astService;
    }

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
            if (task.Metadata.ContainsKey("IsDelegated"))
            {
                return null;
            }

            var complexity = await CalculateComplexity(code, cancellationToken);
            
            if (complexity.TokenCount > MaxTokens)
            {
                return CreateDelegationRequest(task, "TokenCount", complexity.TokenCount);
            }

            if (complexity.CyclomaticComplexity > MaxComplexity)
            {
                return CreateDelegationRequest(task, "CyclomaticComplexity", complexity.CyclomaticComplexity);
            }
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

    protected virtual async Task<CodeComplexityMetrics> CalculateComplexity(string code, CancellationToken cancellationToken)
    {
        // 1. Try using AST Service
        if (_astService != null)
        {
            try 
            {
                // Basic heuristic to guess language or default to C#
                // In a real scenario, we'd pass language in Metadata
                var result = await _astService.ParseCodeAsync(code, "csharp", "", cancellationToken);
                // Note: ParseCodeAsync might return null or throw if not supported
                
                // If result.Metrics is available, we'd use it. 
                // Assuming we fallback for now as we don't have strong typing on Metrics object yet
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AST Service failed, falling back to basic complexity calculation");
            }
        }

        // 2. Fallback / Basic Heuristic
        var tokenCount = code.Length / 4; // Rough estimate: 4 chars per token
        var lines = code.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        
        var complexity = lines.Count(l => 
            l.Contains("if") || 
            l.Contains("for") || 
            l.Contains("while") || 
            l.Contains("case") || 
            l.Contains("catch") || 
            l.Contains("&&") || 
            l.Contains("||"));

        var nesting = 0;
        if (lines.Length > 0)
        {
             nesting = lines.Max(l => l.TakeWhile(char.IsWhiteSpace).Count() / 4);
        }

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
