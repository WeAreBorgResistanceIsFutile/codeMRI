namespace codeMRI.Core.Models;

/// <summary>
/// Represents a decision about whether to delegate (subdivide) a module
/// Based on CodeWiki paper: "Dynamic Delegation" for adaptive scalability
/// </summary>
public class DelegationDecision
{
    /// <summary>
    /// Whether the module should be delegated (subdivided)
    /// </summary>
    public bool ShouldDelegate { get; set; }
    
    /// <summary>
    /// The reason for delegation
    /// </summary>
    public DelegationReason Reason { get; set; }
    
    /// <summary>
    /// Metrics that led to the delegation decision
    /// </summary>
    public Dictionary<string, object> Metrics { get; set; } = new();
    
    /// <summary>
    /// Human-readable description of the delegation decision
    /// </summary>
    public string Description => Reason switch
    {
        DelegationReason.None => "No delegation required",
        DelegationReason.TokenLimitExceeded => $"Module exceeds token limit ({Metrics.GetValueOrDefault("TokenCount", 0)} tokens)",
        DelegationReason.ComplexityThresholdExceeded => $"Module complexity too high ({Metrics.GetValueOrDefault("ComplexityScore", 0)})",
        DelegationReason.SemanticDiversityHigh => $"Module has high semantic diversity ({Metrics.GetValueOrDefault("SemanticDiversity", 0):P0})",
        DelegationReason.MaxDepthReached => $"Maximum delegation depth reached ({Metrics.GetValueOrDefault("Depth", 0)})",
        _ => "Unknown delegation reason"
    };
    
    public static DelegationDecision NoDelegation() => new() { ShouldDelegate = false, Reason = DelegationReason.None };
    
    public static DelegationDecision ForTokenLimit(double tokenCount, int maxTokens) => new()
    {
        ShouldDelegate = true,
        Reason = DelegationReason.TokenLimitExceeded,
        Metrics = new Dictionary<string, object>
        {
            ["TokenCount"] = tokenCount,
            ["MaxTokens"] = maxTokens
        }
    };
    
    public static DelegationDecision ForComplexity(int complexityScore, int maxComplexity) => new()
    {
        ShouldDelegate = true,
        Reason = DelegationReason.ComplexityThresholdExceeded,
        Metrics = new Dictionary<string, object>
        {
            ["ComplexityScore"] = complexityScore,
            ["MaxComplexity"] = maxComplexity
        }
    };
    
    public static DelegationDecision ForSemanticDiversity(double diversity, double threshold) => new()
    {
        ShouldDelegate = true,
        Reason = DelegationReason.SemanticDiversityHigh,
        Metrics = new Dictionary<string, object>
        {
            ["SemanticDiversity"] = diversity,
            ["Threshold"] = threshold
        }
    };
    
    public static DelegationDecision MaxDepth(int depth) => new()
    {
        ShouldDelegate = false,
        Reason = DelegationReason.MaxDepthReached,
        Metrics = new Dictionary<string, object> { ["Depth"] = depth }
    };
}

/// <summary>
/// Reasons for delegating (subdividing) a module
/// Based on CodeWiki paper delegation criteria
/// </summary>
public enum DelegationReason
{
    /// <summary>
    /// No delegation needed
    /// </summary>
    None,
    
    /// <summary>
    /// Module exceeds the token limit for single-pass LLM processing
    /// </summary>
    TokenLimitExceeded,
    
    /// <summary>
    /// Module's cyclomatic complexity exceeds threshold
    /// </summary>
    ComplexityThresholdExceeded,
    
    /// <summary>
    /// Module contains semantically diverse subcomponents
    /// </summary>
    SemanticDiversityHigh,
    
    /// <summary>
    /// Maximum delegation depth has been reached (cannot subdivide further)
    /// </summary>
    MaxDepthReached
}

/// <summary>
/// Configuration options for dynamic delegation
/// Values based on CodeWiki paper implementation details
/// </summary>
public class DelegationOptions
{
    /// <summary>
    /// Maximum tokens per module. Defaults to 80% of typical context window.
    /// When null, will be calculated from LLM context size configuration.
    /// </summary>
    public int? MaxTokensPerModule { get; set; }
    
    /// <summary>
    /// Default context size to use if no LLM configuration is available
    /// </summary>
    public int DefaultContextSize { get; set; } = 32768;
    
    /// <summary>
    /// What percentage of context window to use (reserves space for prompts/output)
    /// </summary>
    public double ContextUtilizationRatio { get; set; } = 0.8;
    
    /// <summary>
    /// Maximum cyclomatic complexity score before delegation is triggered
    /// </summary>
    public int MaxComplexityScore { get; set; } = 100;
    
    /// <summary>
    /// Maximum levels of delegation/subdivision allowed
    /// Paper specifies: 3 levels
    /// </summary>
    public int MaxDelegationDepth { get; set; } = 3;
    
    /// <summary>
    /// Threshold for semantic diversity triggering delegation (0.0 - 1.0)
    /// </summary>
    public double SemanticDiversityThreshold { get; set; } = 0.6;
    
    /// <summary>
    /// Whether dynamic delegation is enabled
    /// </summary>
    public bool EnableDelegation { get; set; } = true;
    
    /// <summary>
    /// Gets the effective max tokens per module, considering LLM context size
    /// </summary>
    public int GetEffectiveMaxTokens(int? llmContextSize = null)
    {
        if (MaxTokensPerModule.HasValue)
            return MaxTokensPerModule.Value;
        
        var contextSize = llmContextSize ?? DefaultContextSize;
        return (int)(contextSize * ContextUtilizationRatio);
    }
}
