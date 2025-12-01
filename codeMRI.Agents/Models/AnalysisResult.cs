using codeMRI.Core.Interfaces;

namespace codeMRI.Agents.Models;

public record AnalysisResult
{
    public required RepositoryStructure Structure { get; init; }
    public required List<CodeComponent> Components { get; init; }
}
