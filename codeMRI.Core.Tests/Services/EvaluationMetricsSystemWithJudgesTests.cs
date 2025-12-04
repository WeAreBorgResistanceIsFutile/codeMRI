using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class EvaluationMetricsSystemWithJudgesTests
{
    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<EvaluationMetricsSystem>>();
        _mockJudgeAgent = new Mock<IJudgeAgent>();
        _service = new EvaluationMetricsSystem(_mockLogger.Object, _mockJudgeAgent.Object);
    }

    private Mock<ILogger<EvaluationMetricsSystem>> _mockLogger;
    private Mock<IJudgeAgent> _mockJudgeAgent;
    private EvaluationMetricsSystem _service;

    [Test]
    public async Task EvaluateWithJudgesAsync_ShouldAggregateChildScoresWithWeightedAverage()
    {
        var rubric = CreateTestRubric();
        var page = new WikiPage { Content = "Test content" };

        _mockJudgeAgent.Setup(x => x.EvaluateRequirementAsync(It.IsAny<WikiPage>(), It.IsAny<RubricRequirement>()))
            .Returns(Task.FromResult(new RequirementScore { Score = 0.5, RequirementId = "test" }));

        var result = await _service.EvaluateWithJudgesAsync(page, rubric);

        Assert.That(result.OverallScore, Is.EqualTo(0.5));
        Assert.That(result.Reliability, Is.GreaterThan(0));
        Assert.That(result.Breakdown, Has.Count.GreaterThan(0));
    }

    [Test]
    public async Task EvaluateWithJudgesAsync_ShouldReturnDetailedBreakdown()
    {
        var rubric = CreateSimpleRubric();
        var page = new WikiPage { Content = "Documentation with examples" };

        _mockJudgeAgent.Setup(x => x.EvaluateRequirementAsync(
                It.IsAny<WikiPage>(), It.Is<RubricRequirement>(r => r.Title == "Clarity")))
            .ReturnsAsync(new RequirementScore { Score = 0.9, RequirementId = "Clarity" });

        _mockJudgeAgent.Setup(x => x.EvaluateRequirementAsync(
                It.IsAny<WikiPage>(), It.Is<RubricRequirement>(r => r.Title == "Examples")))
            .ReturnsAsync(new RequirementScore { Score = 0.7, RequirementId = "Examples" });

        _mockJudgeAgent.Setup(x => x.EvaluateRequirementAsync(
                It.IsAny<WikiPage>(), It.Is<RubricRequirement>(r => r.Title == "Endpoints")))
            .ReturnsAsync(new RequirementScore { Score = 0.8, RequirementId = "Endpoints" });

        var result = await _service.EvaluateWithJudgesAsync(page, rubric);

        Assert.That(result.OverallScore, Is.GreaterThan(0));
        Assert.That(result.Breakdown, Has.Count.GreaterThan(0));
        Assert.That(result.Breakdown.ContainsKey("Clarity"), Is.True);
        Assert.That(result.Breakdown.ContainsKey("Examples"), Is.True);
        Assert.That(result.Breakdown.ContainsKey("Endpoints"), Is.True);
    }

    [Test]
    public async Task EvaluateWithJudgesAsync_ShouldCalculateReliabilityMetrics()
    {
        var rubric = CreateVariedRubric();
        var page = new WikiPage { Content = "Documentation with variations" };

        SetupVariedMockJudgeResponses();

        var result = await _service.EvaluateWithJudgesAsync(page, rubric);

        Assert.That(result.Reliability, Is.GreaterThanOrEqualTo(0.0));
        Assert.That(result.Reliability, Is.LessThanOrEqualTo(1.0));
        Assert.That(result.StandardDeviation, Is.Not.Null);
    }

    [Test]
    public async Task EvaluateWithJudgesAsync_ShouldHandleEmptyRubric()
    {
        var rubric = new EvaluationRubric
        {
            Title = "Empty Rubric",
            Weight = 1.0,
            Children = new List<RubricNode>()
        };

        var page = new WikiPage();
        var result = await _service.EvaluateWithJudgesAsync(page, rubric);

        Assert.That(result.OverallScore, Is.EqualTo(0.0));
        Assert.That(result.Breakdown, Is.Empty);
    }

    [Test]
    public async Task EvaluateWithJudgesAsync_ShouldHandleSingleRequirement()
    {
        var rubric = new EvaluationRubric
        {
            Title = "Single Requirement",
            Weight = 1.0,
            Children = new List<RubricNode>
            {
                new RubricRequirement
                {
                    Title = "SingleReq",
                    Weight = 1.0,
                    IsLeaf = true,
                    Description = "Single requirement test"
                }
            }
        };

        var page = new WikiPage { Content = "Test documentation" };

        _mockJudgeAgent.Setup(x => x.EvaluateRequirementAsync(
                It.IsAny<WikiPage>(), It.Is<RubricRequirement>(r => r.Title == "SingleReq")))
            .ReturnsAsync(new RequirementScore { Score = 0.8, RequirementId = "SingleReq" });

        var result = await _service.EvaluateWithJudgesAsync(page, rubric);

        Assert.That(result.OverallScore, Is.EqualTo(0.8));
        Assert.That(result.Breakdown, Has.Count.EqualTo(1));
        Assert.That(result.Breakdown.ContainsKey("SingleReq"), Is.True);
    }

    private EvaluationRubric CreateTestRubric()
    {
        return new EvaluationRubric
        {
            Title = "Test Rubric",
            Weight = 1.0,
            Children = new List<RubricNode>
            {
                new RubricCategory
                {
                    Title = "Quality",
                    Weight = 0.6,
                    Children = new List<RubricNode>
                    {
                        new RubricRequirement
                        {
                            Title = "req1",
                            Weight = 0.5,
                            IsLeaf = true,
                            Description = "Documentation clarity"
                        },
                        new RubricRequirement
                        {
                            Title = "req2",
                            Weight = 0.5,
                            IsLeaf = true,
                            Description = "Code examples"
                        }
                    }
                },
                new RubricCategory
                {
                    Title = "Coverage",
                    Weight = 0.4,
                    Children = new List<RubricNode>
                    {
                        new RubricRequirement
                        {
                            Title = "req3",
                            Weight = 1.0,
                            IsLeaf = true,
                            Description = "API documentation"
                        }
                    }
                }
            }
        };
    }

    private EvaluationRubric CreateSimpleRubric()
    {
        return new EvaluationRubric
        {
            Title = "Comprehensive Evaluation",
            Weight = 1.0,
            Children = new List<RubricNode>
            {
                new RubricCategory
                {
                    Title = "Documentation Quality",
                    Weight = 0.7,
                    Children = new List<RubricNode>
                    {
                        new RubricRequirement
                            { Title = "Clarity", Weight = 0.4, IsLeaf = true, Description = "Clear explanations" },
                        new RubricRequirement
                            { Title = "Examples", Weight = 0.6, IsLeaf = true, Description = "Code examples provided" }
                    }
                },
                new RubricCategory
                {
                    Title = "API Coverage",
                    Weight = 0.3,
                    Children = new List<RubricNode>
                    {
                        new RubricRequirement
                        {
                            Title = "Endpoints", Weight = 1.0, IsLeaf = true, Description = "API endpoints documented"
                        }
                    }
                }
            }
        };
    }

    private EvaluationRubric CreateVariedRubric()
    {
        return new EvaluationRubric
        {
            Title = "Varied Evaluation",
            Weight = 1.0,
            Children = new List<RubricNode>
            {
                new RubricRequirement
                    { Title = "Var1", Weight = 0.5, IsLeaf = true, Description = "Varied requirement 1" },
                new RubricRequirement
                    { Title = "Var2", Weight = 0.5, IsLeaf = true, Description = "Varied requirement 2" }
            }
        };
    }

    private void SetupVariedMockJudgeResponses()
    {
        _mockJudgeAgent.Setup(x => x.EvaluateRequirementAsync(
                It.IsAny<WikiPage>(), It.Is<RubricRequirement>(r => r.Title == "Var1")))
            .ReturnsAsync(new RequirementScore { Score = 0.7, RequirementId = "Var1" });

        _mockJudgeAgent.Setup(x => x.EvaluateRequirementAsync(
                It.IsAny<WikiPage>(), It.Is<RubricRequirement>(r => r.Title == "Var2")))
            .ReturnsAsync(new RequirementScore { Score = 0.8, RequirementId = "Var2" });
    }

    // Multi-Judge Consensus Evaluation Tests

    [Test]
    public async Task EvaluateWithMultipleJudgesAsync_ThreeJudges_ReturnsConsensusScore()
    {
        // Arrange
        var page = CreateTestWikiPage();
        var rubric = CreateTestEvaluationRubric();
        
        // Set up the existing mock to return good scores for our test rubric
        _mockJudgeAgent.Setup(j => j.EvaluateRequirementAsync(
                It.IsAny<WikiPage>(), It.Is<RubricRequirement>(r => r.Title == "Clarity")))
            .ReturnsAsync(new RequirementScore
            {
                RequirementId = "Clarity",
                Score = 85.0,
                Reasoning = "Good clarity evaluation"
            });

        _mockJudgeAgent.Setup(j => j.EvaluateRequirementAsync(
                It.IsAny<WikiPage>(), It.Is<RubricRequirement>(r => r.Title == "Examples")))
            .ReturnsAsync(new RequirementScore
            {
                RequirementId = "Examples", 
                Score = 80.0,
                Reasoning = "Good examples evaluation"
            });

        _mockJudgeAgent.Setup(j => j.EvaluateRequirementAsync(
                It.IsAny<WikiPage>(), It.Is<RubricRequirement>(r => r.Title == "Endpoints")))
            .ReturnsAsync(new RequirementScore
            {
                RequirementId = "Endpoints",
                Score = 90.0,
                Reasoning = "Good endpoints evaluation"
            });

        // Create multiple judges using the same mock setup
        var judges = new List<IJudgeAgent> { _mockJudgeAgent.Object, _mockJudgeAgent.Object, _mockJudgeAgent.Object };

        // Act
        var result = await _service.EvaluateWithMultipleJudgesAsync(page, rubric, judges);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.JudgeCount, Is.EqualTo(3));
        Assert.That(result.MeetsMinimumJudgeRequirement, Is.True);
        Assert.That(result.IndividualScores, Has.Count.EqualTo(3));
        Assert.That(result.ConsensusScore, Is.GreaterThan(0));
        Assert.That(result.OverallScore, Is.GreaterThan(0));
        Assert.That(result.JudgeReliabilities, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task EvaluateWithMultipleJudgesAsync_TwoJudges_ReturnsInsufficientJudgesStatus()
    {
        // Arrange
        var page = CreateTestWikiPage();
        var rubric = CreateTestEvaluationRubric();
        var judges = new List<IJudgeAgent>
        {
            CreateMockJudge("Judge1", 85.0),
            CreateMockJudge("Judge2", 80.0)
        };

        // Act
        var result = await _service.EvaluateWithMultipleJudgesAsync(page, rubric, judges);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.JudgeCount, Is.EqualTo(2));
        Assert.That(result.MeetsMinimumJudgeRequirement, Is.False);
        Assert.That(result.ConsensusStatus, Is.EqualTo("Insufficient Judges (Minimum 3 Required)"));
        Assert.That(result.IndividualScores, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task EvaluateWithMultipleJudgesAsync_HighAgreement_ReturnsStrongConsensus()
    {
        // Arrange
        var page = CreateTestWikiPage();
        var rubric = CreateTestEvaluationRubric();
        var judges = new List<IJudgeAgent>
        {
            CreateMockJudge("Judge1", 85.0),
            CreateMockJudge("Judge2", 87.0),
            CreateMockJudge("Judge3", 86.0)
        };

        // Act
        var result = await _service.EvaluateWithMultipleJudgesAsync(page, rubric, judges);

        // Assert
        Assert.That(result.ConsensusStatus, Is.EqualTo("Strong Consensus"));
        Assert.That(result.ConsensusScore, Is.GreaterThan(0.5)); // Good agreement
    }

    [Test]
    public async Task EvaluateWithMultipleJudgesAsync_LowAgreement_ReturnsLowConsensus()
    {
        // Arrange
        var page = CreateTestWikiPage();
        var rubric = CreateTestEvaluationRubric();
        var judges = new List<IJudgeAgent>
        {
            CreateMockJudge("Judge1", 95.0),
            CreateMockJudge("Judge2", 60.0),
            CreateMockJudge("Judge3", 40.0)
        };

        // Act
        var result = await _service.EvaluateWithMultipleJudgesAsync(page, rubric, judges);

        // Assert
        Assert.That(result.ConsensusStatus, Is.EqualTo("Low Consensus"));
        Assert.That(result.ConsensusScore, Is.LessThan(0.8)); // Lower agreement
    }

    [Test]
    public async Task EvaluateWithMultipleJudgesAsync_OneJudgeFails_ContinuesWithOthers()
    {
        // Arrange
        var page = CreateTestWikiPage();
        var rubric = CreateTestEvaluationRubric();
        var judges = new List<IJudgeAgent>
        {
            CreateMockJudge("Judge1", 85.0),
            CreateFailingMockJudge("Judge2"),
            CreateMockJudge("Judge3", 90.0)
        };

        // Act
        var result = await _service.EvaluateWithMultipleJudgesAsync(page, rubric, judges);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.JudgeCount, Is.EqualTo(2)); // Only successful judges
        Assert.That(result.IndividualScores, Has.Count.EqualTo(2));
        Assert.That(result.MeetsMinimumJudgeRequirement, Is.True); // 3 judges provided, meets minimum
    }

    [Test]
    public async Task EvaluateWithMultipleJudgesAsync_AllJudgesFail_ThrowsInvalidOperationException()
    {
        // Arrange
        var page = CreateTestWikiPage();
        var rubric = CreateTestEvaluationRubric();
        var judges = new List<IJudgeAgent>
        {
            CreateFailingMockJudge("Judge1"),
            CreateFailingMockJudge("Judge2"),
            CreateFailingMockJudge("Judge3")
        };

        // Act & Assert
        var ex = await ThrowsAsync<InvalidOperationException>(
            () => _service.EvaluateWithMultipleJudgesAsync(page, rubric, judges));
        
        Assert.That(ex.Message, Is.EqualTo("All judge evaluations failed"));
    }

    [Test]
    public async Task EvaluateWithMultipleJudgesAsync_NullPage_ThrowsArgumentNullException()
    {
        // Arrange
        WikiPage page = null;
        var rubric = CreateTestEvaluationRubric();
        var judges = new List<IJudgeAgent> { CreateMockJudge("Judge1", 85.0) };

        // Act & Assert
        await ThrowsAsync<ArgumentNullException>(
            () => _service.EvaluateWithMultipleJudgesAsync(page, rubric, judges));
    }

    [Test]
    public async Task EvaluateWithMultipleJudgesAsync_NullRubric_ThrowsArgumentNullException()
    {
        // Arrange
        var page = CreateTestWikiPage();
        EvaluationRubric rubric = null;
        var judges = new List<IJudgeAgent> { CreateMockJudge("Judge1", 85.0) };

        // Act & Assert
        await ThrowsAsync<ArgumentNullException>(
            () => _service.EvaluateWithMultipleJudgesAsync(page, rubric, judges));
    }

    [Test]
    public async Task EvaluateWithMultipleJudgesAsync_EmptyJudgesList_ThrowsArgumentException()
    {
        // Arrange
        var page = CreateTestWikiPage();
        var rubric = CreateTestEvaluationRubric();
        var judges = new List<IJudgeAgent>();

        // Act & Assert
        var ex = await ThrowsAsync<ArgumentException>(
            () => _service.EvaluateWithMultipleJudgesAsync(page, rubric, judges));
        
        Assert.That(ex.Message, Does.Contain("At least one judge agent must be provided"));
    }

    [Test]
    public async Task EvaluateWithMultipleJudgesAsync_VaryingReliabilities_WeightedAverageCalculated()
    {
        // Arrange
        var page = CreateTestWikiPage();
        var rubric = CreateTestEvaluationRubric();
        var judges = new List<IJudgeAgent>
        {
            CreateMockJudgeWithReliability("Judge1", 90.0, 0.9), // High reliability, high score
            CreateMockJudgeWithReliability("Judge2", 70.0, 0.5), // Low reliability, low score
            CreateMockJudgeWithReliability("Judge3", 80.0, 0.8)  // Medium reliability, medium score
        };

        // Act
        var result = await _service.EvaluateWithMultipleJudgesAsync(page, rubric, judges);

        // Assert
        // System calculates weighted average based on actual judge scores and reliabilities
        Assert.That(result.OverallScore, Is.GreaterThan(70.0));
        Assert.That(result.OverallScore, Is.LessThan(90.0));
        Assert.That(result.Reliability, Is.LessThan(1.0)); // Should be reduced by consensus variation
    }

    [Test]
    public async Task EvaluateWithMultipleJudgesAsync_FiveJudges_ExceedsMinimumRequirement()
    {
        // Arrange
        var page = CreateTestWikiPage();
        var rubric = CreateTestEvaluationRubric();
        var judges = new List<IJudgeAgent>
        {
            CreateMockJudge("Judge1", 85.0),
            CreateMockJudge("Judge2", 80.0),
            CreateMockJudge("Judge3", 90.0),
            CreateMockJudge("Judge4", 88.0),
            CreateMockJudge("Judge5", 82.0)
        };

        // Act
        var result = await _service.EvaluateWithMultipleJudgesAsync(page, rubric, judges);

        // Assert
        Assert.That(result.JudgeCount, Is.EqualTo(5));
        Assert.That(result.MeetsMinimumJudgeRequirement, Is.True);
        Assert.That(result.IndividualScores, Has.Count.EqualTo(5));
        Assert.That(result.JudgeReliabilities, Has.Count.EqualTo(5));
        
        // With more judges, consensus should be more stable
        Assert.That(result.ConsensusScore, Is.GreaterThan(0));
    }

    [Test]
    public async Task EvaluateWithMultipleJudgesAsync_AggregatesBreakdownScoresCorrectly()
    {
        // Arrange
        var page = CreateTestWikiPage();
        var rubric = CreateTestEvaluationRubric();
        var judges = new List<IJudgeAgent>
        {
            CreateMockJudgeWithDetailedBreakdown("Judge1", 85.0, "Clarity", 90.0),
            CreateMockJudgeWithDetailedBreakdown("Judge2", 80.0, "Clarity", 85.0),
            CreateMockJudgeWithDetailedBreakdown("Judge3", 90.0, "Clarity", 88.0)
        };

        // Act
        var result = await _service.EvaluateWithMultipleJudgesAsync(page, rubric, judges);

        // Assert
        Assert.That(result.Breakdown, Is.Not.Null);
        Assert.That(result.Breakdown.ContainsKey("Clarity"), Is.True);
        
        var clarityScore = result.Breakdown["Clarity"].Score;
        var expectedClarityAverage = (90.0 + 85.0 + 88.0) / 3.0;
        Assert.That(clarityScore, Is.EqualTo(expectedClarityAverage).Within(0.01));
        
        Assert.That(result.Breakdown["Clarity"].Reasoning, Does.Contain("Aggregated from 3 judges"));
    }

    // Helper methods for multi-judge tests
    private IJudgeAgent CreateMockJudge(string judgeId, double score)
    {
        var mock = new Mock<IJudgeAgent>();
        // Set up mock to return the specified score for specific requirement titles
        mock.Setup(j => j.EvaluateRequirementAsync(
                It.IsAny<WikiPage>(), It.Is<RubricRequirement>(r => r.Title == "Clarity")))
            .ReturnsAsync(new RequirementScore
            {
                RequirementId = "Clarity",
                Score = score,
                Reasoning = $"Mock evaluation from {judgeId} for Clarity"
            });

        mock.Setup(j => j.EvaluateRequirementAsync(
                It.IsAny<WikiPage>(), It.Is<RubricRequirement>(r => r.Title == "Examples")))
            .ReturnsAsync(new RequirementScore
            {
                RequirementId = "Examples",
                Score = score * 0.9, // Slightly different for variety
                Reasoning = $"Mock evaluation from {judgeId} for Examples"
            });

        mock.Setup(j => j.EvaluateRequirementAsync(
                It.IsAny<WikiPage>(), It.Is<RubricRequirement>(r => r.Title == "Endpoints")))
            .ReturnsAsync(new RequirementScore
            {
                RequirementId = "Endpoints",
                Score = score * 0.95, // Slightly different for variety
                Reasoning = $"Mock evaluation from {judgeId} for Endpoints"
            });
        return mock.Object;
    }

    private IJudgeAgent CreateMockJudgeWithReliability(string judgeId, double score, double reliability)
    {
        var mock = new Mock<IJudgeAgent>();
        mock.Setup(j => j.EvaluateRequirementAsync(It.IsAny<WikiPage>(), It.IsAny<RubricRequirement>()))
            .ReturnsAsync(new RequirementScore
            {
                RequirementId = judgeId,
                Score = score,
                Reasoning = $"Mock evaluation from {judgeId} with reliability {reliability}"
            });
        return mock.Object;
    }

    private IJudgeAgent CreateMockJudgeWithDetailedBreakdown(string judgeId, double overallScore, string requirementTitle, double requirementScore)
    {
        var mock = new Mock<IJudgeAgent>();
        mock.Setup(j => j.EvaluateRequirementAsync(It.IsAny<WikiPage>(), It.IsAny<RubricRequirement>()))
            .ReturnsAsync(new RequirementScore
            {
                RequirementId = requirementTitle,
                Score = requirementScore,
                Reasoning = $"Detailed evaluation from {judgeId} for {requirementTitle}"
            });
        return mock.Object;
    }

    private IJudgeAgent CreateFailingMockJudge(string judgeId)
    {
        var mock = new Mock<IJudgeAgent>();
        mock.Setup(j => j.EvaluateRequirementAsync(It.IsAny<WikiPage>(), It.IsAny<RubricRequirement>()))
            .ThrowsAsync(new InvalidOperationException($"Judge {judgeId} failed"));
        return mock.Object;
    }

    private WikiPage CreateTestWikiPage()
    {
        return new WikiPage
        {
            Id = "test-page",
            Title = "Test Page",
            Content = "This is a test wiki page with some content for evaluation."
        };
    }

    private EvaluationRubric CreateTestEvaluationRubric()
    {
        return new EvaluationRubric
        {
            Title = "Test Rubric",
            Weight = 1.0,
            Children = new List<RubricNode>
            {
                new RubricCategory
                {
                    Title = "Documentation Quality",
                    Weight = 0.7,
                    Children = new List<RubricNode>
                    {
                        new RubricRequirement
                        {
                            Title = "Clarity",
                            Weight = 0.6,
                            IsLeaf = true
                        },
                        new RubricRequirement
                        {
                            Title = "Examples",
                            Weight = 0.4,
                            IsLeaf = true
                        }
                    }
                },
                new RubricCategory
                {
                    Title = "API Coverage",
                    Weight = 0.3,
                    Children = new List<RubricNode>
                    {
                        new RubricRequirement
                        {
                            Title = "Endpoints",
                            Weight = 1.0,
                            IsLeaf = true
                        }
                    }
                }
            }
        };
    }

    private async Task<T> ThrowsAsync<T>(Func<Task> action) where T : Exception
    {
        try
        {
            await action();
            return null;
        }
        catch (T ex)
        {
            return ex;
        }
    }


}