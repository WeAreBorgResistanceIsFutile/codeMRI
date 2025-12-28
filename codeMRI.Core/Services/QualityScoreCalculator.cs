using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;

namespace codeMRI.Core.Services;

/// <summary>
///     Calculates quality scores from Judge assessments using hierarchical weighted aggregation
///     with uncertainty propagation as described in the CodeWiki paper.
/// </summary>
public class QualityScoreCalculator
{
    /// <summary>
    ///     Calculates the overall quality score for a page based on requirement assessments.
    ///     Uses weighted averaging with uncertainty propagation.
    /// </summary>
    /// <param name="assessments">List of requirement assessments from Judge agents</param>
    /// <param name="rubric">Optional rubric containing requirement weights. If null, equal weights are assumed.</param>
    /// <returns>Quality score result with score and standard deviation</returns>
    public QualityScoreResult CalculatePageQualityScore(
        List<RequirementAssessment> assessments,
        EvaluationRubric? rubric = null)
    {
        if (assessments == null || assessments.Count == 0)
        {
            return new QualityScoreResult
            {
                Score = 0.0,
                StandardDeviation = 0.0,
                AssessedRequirements = 0,
                TotalRequirements = 0
            };
        }

        // Build weight map from rubric
        var weightMap = BuildWeightMap(rubric);

        // Calculate weighted average and propagated uncertainty
        double weightedSum = 0.0;
        double totalWeight = 0.0;
        double varianceSum = 0.0;

        foreach (var assessment in assessments)
        {
            // Get weight for this requirement (default to 1.0 if not in rubric)
            var weight = weightMap.TryGetValue(assessment.RequirementId, out var w) ? w : 1.0;

            if (weight <= 0.0)
                continue;

            weightedSum += weight * assessment.MeanScore;
            totalWeight += weight;

            // Propagate uncertainty: σ² contribution = w² * σ²
            varianceSum += weight * weight * assessment.StandardDeviation * assessment.StandardDeviation;
        }

        if (totalWeight == 0.0)
        {
            return new QualityScoreResult
            {
                Score = 0.0,
                StandardDeviation = 0.0,
                AssessedRequirements = assessments.Count,
                TotalRequirements = weightMap.Count > 0 ? weightMap.Count : assessments.Count
            };
        }

        // Calculate final score and standard deviation
        var score = weightedSum / totalWeight;
        
        // Propagated standard deviation: σ = sqrt(Σ(w_i² * σ_i²)) / Σ(w_i)
        var standardDeviation = Math.Sqrt(varianceSum) / totalWeight;

        return new QualityScoreResult
        {
            Score = score,
            StandardDeviation = standardDeviation,
            AssessedRequirements = assessments.Count,
            TotalRequirements = weightMap.Count > 0 ? weightMap.Count : assessments.Count
        };
    }

    /// <summary>
    ///     Builds a map of requirement IDs to their weights from the rubric.
    /// </summary>
    private Dictionary<string, double> BuildWeightMap(EvaluationRubric? rubric)
    {
        var weightMap = new Dictionary<string, double>();

        if (rubric == null)
            return weightMap;

        // Recursively extract leaf requirements and their weights
        ExtractLeafWeights(rubric, weightMap);

        return weightMap;
    }

    /// <summary>
    ///     Recursively extracts leaf requirement weights from the rubric tree.
    /// </summary>
    private void ExtractLeafWeights(RubricNode node, Dictionary<string, double> weightMap)
    {
        if (node == null)
            return;

        // If this is a leaf requirement, add its weight
        if (node.IsLeaf && node is RubricRequirement requirement)
        {
            weightMap[requirement.Id] = requirement.Weight;
        }

        // Recurse into children
        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                ExtractLeafWeights(child, weightMap);
            }
        }
    }
}
