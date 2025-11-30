using codeMRI.Core.Interfaces;

namespace codeMRI.Agents.Models;

public record AnalysisResult
{
    public RepositoryStructure Structure { get; init; }
    public List<CodeComponent> Components { get; init; }
}
