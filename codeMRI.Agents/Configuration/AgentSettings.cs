namespace codeMRI.Agents.Configuration;

public class AgentSettings
{
    public bool EnableDelegation { get; set; } = true;
    public int MaxRecursionDepth { get; set; } = 3;

    public Dictionary<string, int> ComplexityThresholds { get; set; } = new()
    {
        { "ComplexityScore", 8 },
        { "LineCount", 500 }
    };
}