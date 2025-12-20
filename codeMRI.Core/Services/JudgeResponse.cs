namespace codeMRI.Core.Services;

internal class JudgeResponse
{
    public string RequirementId { get; set; } = string.Empty;
    public double Score { get; set; }
    public string? Reasoning { get; set; }
    public List<string>? Evidence { get; set; }
}