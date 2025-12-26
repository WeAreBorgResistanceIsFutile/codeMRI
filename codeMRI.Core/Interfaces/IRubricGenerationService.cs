using System.Text.Json.Serialization;
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
    public string Url { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string RepoPath { get; set; } = string.Empty;
}

public class EvaluationRubric : RubricNode
{
    // No need to hide base properties - use them directly
}

[JsonDerivedType(typeof(RubricCategory), typeDiscriminator: "category")]
[JsonDerivedType(typeof(RubricRequirement), typeDiscriminator: "requirement")]
[JsonDerivedType(typeof(EvaluationRubric), typeDiscriminator: "rubric")]
public abstract class RubricNode
{
    // Ensure Title etc. are properly mapped if needed, but camelCase policy usually suffices
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("is_leaf")]
    [JsonInclude]
    public bool IsLeaf { get; set; }

    // This allows both isLeaf and is_leaf to map if PropertyNameCaseInsensitive = true
    // actually System.Text.Json only allows one mapping. 
    // But we can use a property to bridge.
    [JsonPropertyName("isLeaf")]
    public bool IsLeafCamel { set => IsLeaf = value; }

    [JsonPropertyName("children")]
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