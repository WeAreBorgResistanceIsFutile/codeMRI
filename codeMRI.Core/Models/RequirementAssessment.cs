namespace codeMRI.Core.Models;

public class RequirementAssessment
{
    public string RequirementId { get; set; } = string.Empty;
    public string RequirementTitle { get; set; } = string.Empty;
    public double MeanScore { get; set; }
    public double StandardDeviation { get; set; }
    public List<double> IndividualScores { get; set; } = new();
    public List<string> Reasoning { get; set; } = new();
    public List<string> Evidence { get; set; } = new();
    public List<string> FailedModels { get; set; } = new();
}
