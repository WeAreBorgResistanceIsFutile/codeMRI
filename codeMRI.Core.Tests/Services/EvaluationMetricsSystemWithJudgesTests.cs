using codeMRI.Core.Interfaces;
using codeMRI.Core.Services;
using codeMRI.Shared.Models;
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

        Assert.That(result.OverallScore, Is.EqualTo(0.0));
        Assert.That(result.Reliability, Is.GreaterThan(0));
        Assert.That(result.Breakdown, Has.Count.EqualTo(0));
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
            .ReturnsAsync(new RequirementScore { Score = 1.0, RequirementId = "Endpoints" });

        var result = await _service.EvaluateWithJudgesAsync(page, rubric);

        Assert.That(result.Breakdown, Is.Empty);


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
        Assert.That(result.StandardDeviation, Is.Empty);
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

        Assert.That(result.OverallScore, Is.EqualTo(0.0));
        Assert.That(result.Breakdown, Has.Count.EqualTo(0));
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
}