using codeMRI.Shared.Models;

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
}

public class QualityScore
{
    public double OverallScore { get; set; }
    public Dictionary<string, RequirementScore>? Breakdown { get; set; }
    public double Reliability { get; set; }
    public Dictionary<string, double>? StandardDeviation { get; set; }
}