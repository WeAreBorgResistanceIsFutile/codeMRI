using codeMRI.Core.Models;

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
    // No need to hide base properties - use them directly
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
    // No need to hide base properties - use them directly
}

public class RubricRequirement : RubricNode
{
    public string Description { get; set; } = string.Empty;
    // No need to hide base properties - use them directly
}