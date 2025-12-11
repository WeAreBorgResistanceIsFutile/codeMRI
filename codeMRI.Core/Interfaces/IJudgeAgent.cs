using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

public interface IJudgeAgent
{
    Task<RequirementScore> EvaluateRequirementAsync(WikiPage page, RubricRequirement? requirement);
}

public class RequirementScore
{
    public string RequirementId { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    
    /// <summary>
    /// Indicates whether the evaluation failed (e.g., LLM error, timeout).
    /// When true, the Score should not be included in aggregations.
    /// </summary>
    public bool EvaluationFailed { get; set; }
    
    /// <summary>
    /// If EvaluationFailed is true, contains the reason for the failure.
    /// </summary>
    public string? FailureReason { get; set; }
}

public class QualityScore
{
    public double OverallScore { get; set; }
    public Dictionary<string, RequirementScore>? Breakdown { get; set; }
    public double Reliability { get; set; }
    public Dictionary<string, double>? StandardDeviation { get; set; }
}