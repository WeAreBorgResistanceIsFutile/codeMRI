namespace codeMRI.Core.Models;

public class ModelAssessment
{
    public string ModelName { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public List<string> Evidence { get; set; } = new();
}