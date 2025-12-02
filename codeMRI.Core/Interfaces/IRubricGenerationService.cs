using codeMRI.Shared.Models;

namespace codeMRI.Core.Interfaces;

public interface IRubricGenerationService
{
    Task<EvaluationRubric> GenerateRubricAsync(
        WikiStructure documentationStructure,
        RepositoryInfo repositoryInfo,
        CancellationToken cancellationToken = default);

    Task<EvaluationRubric> GenerateConsensusRubricAsync(
        WikiStructure documentationStructure,
        RepositoryInfo repositoryInfo,
        List<string> modelNames,
        CancellationToken cancellationToken = default);
}

public class RepositoryInfo
{
    public string Name { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public int LinesOfCode { get; set; }
    public int ComponentCount { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class EvaluationRubric : RubricNode
{
    public new string Title { get; set; } = string.Empty;
    public new double Weight { get; set; }
    public new List<RubricNode>? Children { get; set; }
}

public abstract class RubricNode
{
    public string Title { get; set; } = string.Empty;
    public double Weight { get; set; }
    public bool IsLeaf { get; set; }
    public List<RubricNode>? Children { get; set; }
}

public class RubricCategory : RubricNode
{
    public new List<RubricNode>? Children { get; set; }
}

public class RubricRequirement : RubricNode
{
    public string Description { get; set; } = string.Empty;
    public new List<RubricNode>? Children { get; set; }
}