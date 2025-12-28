using NUnit.Framework;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class QualityScoreCalculatorTests
{
    private QualityScoreCalculator _calculator;

    [SetUp]
    public void SetUp()
    {
        _calculator = new QualityScoreCalculator();
    }

    #region CalculatePageQualityScore Tests

    [Test]
    public void CalculatePageQualityScore_WithNoAssessments_ShouldReturnZero()
    {
        // Arrange
        var assessments = new List<RequirementAssessment>();

        // Act
        var result = _calculator.CalculatePageQualityScore(assessments);

        // Assert
        Assert.That(result.Score, Is.EqualTo(0.0));
        Assert.That(result.StandardDeviation, Is.EqualTo(0.0));
    }

    [Test]
    public void CalculatePageQualityScore_WithSingleAssessment_ShouldReturnMeanScore()
    {
        // Arrange
        var assessments = new List<RequirementAssessment>
        {
            new RequirementAssessment
            {
                RequirementId = "req-1",
                MeanScore = 0.85,
                StandardDeviation = 0.1
            }
        };

        // Act
        var result = _calculator.CalculatePageQualityScore(assessments);

        // Assert
        Assert.That(result.Score, Is.EqualTo(0.85).Within(0.001));
        Assert.That(result.StandardDeviation, Is.EqualTo(0.1).Within(0.001));
    }

    [Test]
    public void CalculatePageQualityScore_WithMultipleAssessments_ShouldCalculateWeightedAverage()
    {
        // Arrange - All requirements have equal weight (1.0) by default
        var assessments = new List<RequirementAssessment>
        {
            new RequirementAssessment
            {
                RequirementId = "req-1",
                MeanScore = 0.8,
                StandardDeviation = 0.1
            },
            new RequirementAssessment
            {
                RequirementId = "req-2",
                MeanScore = 0.9,
                StandardDeviation = 0.05
            }
        };

        // Act
        var result = _calculator.CalculatePageQualityScore(assessments);

        // Assert - Simple average when weights are equal
        Assert.That(result.Score, Is.EqualTo(0.85).Within(0.001));
    }

    [Test]
    public void CalculatePageQualityScore_WithWeightedRequirements_ShouldApplyWeights()
    {
        // Arrange
        var rubric = new EvaluationRubric
        {
            Id = "Test Rubric",
            Children = new List<RubricNode>
            {
                new RubricRequirement
                {
                    Id = "req-1",
                    Title = "High Priority",
                    Weight = 2.0,
                    IsLeaf = true
                },
                new RubricRequirement
                {
                    Id = "req-2",
                    Title = "Low Priority",
                    Weight = 1.0,
                    IsLeaf = true
                }
            }
        };

        var assessments = new List<RequirementAssessment>
        {
            new RequirementAssessment
            {
                RequirementId = "req-1",
                MeanScore = 0.6,
                StandardDeviation = 0.1
            },
            new RequirementAssessment
            {
                RequirementId = "req-2",
                MeanScore = 0.9,
                StandardDeviation = 0.05
            }
        };

        // Act
        var result = _calculator.CalculatePageQualityScore(assessments, rubric);

        // Assert - Weighted average: (2.0 * 0.6 + 1.0 * 0.9) / (2.0 + 1.0) = 2.1 / 3.0 = 0.7
        Assert.That(result.Score, Is.EqualTo(0.7).Within(0.001));
    }

    [Test]
    public void CalculatePageQualityScore_ShouldPropagateUncertainty()
    {
        // Arrange - According to the paper: σ_parent = sqrt(Σ(w_i² * σ_i²)) / Σ(w_i)
        var assessments = new List<RequirementAssessment>
        {
            new RequirementAssessment
            {
                RequirementId = "req-1",
                MeanScore = 0.8,
                StandardDeviation = 0.1 // σ₁ = 0.1, w₁ = 1.0
            },
            new RequirementAssessment
            {
                RequirementId = "req-2",
                MeanScore = 0.9,
                StandardDeviation = 0.2 // σ₂ = 0.2, w₂ = 1.0
            }
        };

        // Act
        var result = _calculator.CalculatePageQualityScore(assessments);

        // Assert
        // σ = sqrt((1.0² * 0.1² + 1.0² * 0.2²)) / (1.0 + 1.0)
        // σ = sqrt(0.01 + 0.04) / 2.0
        // σ = sqrt(0.05) / 2.0
        // σ = 0.2236 / 2.0 = 0.1118
        Assert.That(result.StandardDeviation, Is.EqualTo(0.1118).Within(0.001));
    }

    [Test]
    public void CalculatePageQualityScore_WithWeightedRequirements_ShouldPropagateWeightedUncertainty()
    {
        // Arrange
        var rubric = new EvaluationRubric
        {
            Id = "Test Rubric",
            Children = new List<RubricNode>
            {
                new RubricRequirement
                {
                    Id = "req-1",
                    Title = "Requirement 1",
                    Weight = 2.0,
                    IsLeaf = true
                },
                new RubricRequirement
                {
                    Id = "req-2",
                    Title = "Requirement 2",
                    Weight = 1.0,
                    IsLeaf = true
                }
            }
        };

        var assessments = new List<RequirementAssessment>
        {
            new RequirementAssessment
            {
                RequirementId = "req-1",
                MeanScore = 0.8,
                StandardDeviation = 0.1 // w₁ = 2.0, σ₁ = 0.1
            },
            new RequirementAssessment
            {
                RequirementId = "req-2",
                MeanScore = 0.9,
                StandardDeviation = 0.2 // w₂ = 1.0, σ₂ = 0.2
            }
        };

        // Act
        var result = _calculator.CalculatePageQualityScore(assessments, rubric);

        // Assert
        // σ = sqrt((2.0² * 0.1² + 1.0² * 0.2²)) / (2.0 + 1.0)
        // σ = sqrt(0.04 + 0.04) / 3.0
        // σ = sqrt(0.08) / 3.0
        // σ = 0.2828 / 3.0 = 0.0943
        Assert.That(result.StandardDeviation, Is.EqualTo(0.0943).Within(0.001));
    }

    [Test]
    public void CalculatePageQualityScore_WithMissingRequirements_ShouldOnlyUseAvailableAssessments()
    {
        // Arrange
        var rubric = new EvaluationRubric
        {
            Id = "Test Rubric",
            Children = new List<RubricNode>
            {
                new RubricRequirement { Id = "req-1", Weight = 1.0, IsLeaf = true },
                new RubricRequirement { Id = "req-2", Weight = 1.0, IsLeaf = true },
                new RubricRequirement { Id = "req-3", Weight = 1.0, IsLeaf = true }
            }
        };

        var assessments = new List<RequirementAssessment>
        {
            new RequirementAssessment
            {
                RequirementId = "req-1",
                MeanScore = 0.8,
                StandardDeviation = 0.1
            },
            new RequirementAssessment
            {
                RequirementId = "req-2",
                MeanScore = 0.9,
                StandardDeviation = 0.1
            }
            // req-3 is missing
        };

        // Act
        var result = _calculator.CalculatePageQualityScore(assessments, rubric);

        // Assert - Should only average the two available assessments
        Assert.That(result.Score, Is.EqualTo(0.85).Within(0.001));
    }

    #endregion

    #region Edge Cases

    [Test]
    public void CalculatePageQualityScore_WithZeroWeights_ShouldReturnZero()
    {
        // Arrange
        var rubric = new EvaluationRubric
        {
            Id = "Test Rubric",
            Children = new List<RubricNode>
            {
                new RubricRequirement { Id = "req-1", Weight = 0.0, IsLeaf = true }
            }
        };

        var assessments = new List<RequirementAssessment>
        {
            new RequirementAssessment
            {
                RequirementId = "req-1",
                MeanScore = 0.8,
                StandardDeviation = 0.1
            }
        };

        // Act
        var result = _calculator.CalculatePageQualityScore(assessments, rubric);

        // Assert
        Assert.That(result.Score, Is.EqualTo(0.0));
    }

    [Test]
    public void CalculatePageQualityScore_WithNullRubric_ShouldUseEqualWeights()
    {
        // Arrange
        var assessments = new List<RequirementAssessment>
        {
            new RequirementAssessment
            {
                RequirementId = "req-1",
                MeanScore = 0.8,
                StandardDeviation = 0.1
            },
            new RequirementAssessment
            {
                RequirementId = "req-2",
                MeanScore = 0.6,
                StandardDeviation = 0.1
            }
        };

        // Act
        var result = _calculator.CalculatePageQualityScore(assessments, null);

        // Assert - Simple average
        Assert.That(result.Score, Is.EqualTo(0.7).Within(0.001));
    }

    #endregion
}
