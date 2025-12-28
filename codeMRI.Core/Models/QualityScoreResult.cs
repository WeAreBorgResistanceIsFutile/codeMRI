namespace codeMRI.Core.Models;

/// <summary>
///     Result of quality score calculation with uncertainty metrics.
/// </summary>
public class QualityScoreResult
{
    /// <summary>
    ///     Overall quality score in the range [0, 1].
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    ///     Standard deviation representing uncertainty in the score.
    ///     Lower values indicate higher confidence/reliability.
    /// </summary>
    public double StandardDeviation { get; set; }

    /// <summary>
    ///     Number of requirements that were assessed.
    /// </summary>
    public int AssessedRequirements { get; set; }

    /// <summary>
    ///     Total number of requirements in the rubric.
    /// </summary>
    public int TotalRequirements { get; set; }

    /// <summary>
    ///     Coverage percentage (assessed / total).
    /// </summary>
    public double Coverage => TotalRequirements > 0 
        ? (double)AssessedRequirements / TotalRequirements 
        : 0.0;
}
