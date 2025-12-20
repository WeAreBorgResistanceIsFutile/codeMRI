using System.Diagnostics.Metrics;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class DocumentationJudgeServiceTests
{
    [SetUp]
    public void Setup()
    {
        _mockLlmClient = new Mock<ILLMClient>();
        _mockLlmClient.Setup(x => x.ContextSize).Returns(4096);
        _mockLogger = new Mock<ILogger<DocumentationJudgeService>>();
        _mockMeterFactory = new Mock<IMeterFactory>();
        _mockPromptBuilder = new Mock<IEvaluationPromptBuilder>();

        // Setup meter factory to return a real meter for testing
        _mockMeterFactory.Setup(x => x.Create(It.IsAny<MeterOptions>()))
            .Returns(new Meter("TestMeter"));

        // Setup prompt builder to return a basic prompt
        _mockPromptBuilder.Setup(x => x.BuildPrompt(It.IsAny<RubricRequirement>(), It.IsAny<WikiStructure>()))
            .Returns("Mock evaluation prompt");

        _service = new DocumentationJudgeService(
            _mockLogger.Object,
            _mockLlmClient.Object,
            _mockMeterFactory.Object,
            _mockPromptBuilder.Object);
    }

    private Mock<ILLMClient> _mockLlmClient;
    private Mock<ILogger<DocumentationJudgeService>> _mockLogger;
    private Mock<IMeterFactory> _mockMeterFactory;
    private Mock<IEvaluationPromptBuilder> _mockPromptBuilder;
    private DocumentationJudgeService _service;

    [Test]
    public async Task EvaluateRequirementsAsync_ShouldUseCorrectModelForEachJudge()
    {
        // Arrange
        var requirements = new List<RubricRequirement>
        {
            new() { Title = "Req1", Description = "Desc1", IsLeaf = true }
        };
        var structure = new WikiStructure();
        var judgeModels = new List<string> { "model-a", "model-b" };

        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"score\": 0.8, \"reasoning\": \"Good\"}");

        // Act
        await _service.EvaluateRequirementsAsync(requirements, structure, judgeModels);

        // Assert
        _mockLlmClient.Verify(x => x.ChatAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<ChatMessage>>(),
            "model-a", It.IsAny<CancellationToken>()), Times.Once, "Should call ChatAsync with model-a");

        _mockLlmClient.Verify(x => x.ChatAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<ChatMessage>>(),
            "model-b", It.IsAny<CancellationToken>()), Times.Once, "Should call ChatAsync with model-b");
    }

    [Test]
    public async Task EvaluateRequirementAsync_ShouldParseJsonWithMarkdownFences()
    {
        // Arrange
        var requirement = new RubricRequirement { Title = "Req1", Description = "Desc1" };
        var structure = new WikiStructure();
        var jsonContent = "{\"score\": 0.9, \"reasoning\": \"Excellent\", \"evidence\": []}";
        var markdownResponse = $"```json\n{jsonContent}\n```";

        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(markdownResponse);

        // Act
        var result = await _service.EvaluateRequirementAsync(requirement, structure);

        // Assert
        Assert.That(result.MeanScore, Is.EqualTo(0.9));
        Assert.That(result.Reasoning, Does.Contain("Excellent"));
    }

    [Test]
    public async Task EvaluateRequirementAsync_ShouldClampScoreBelowZero()
    {
        // Arrange
        var requirement = new RubricRequirement { Title = "Req1", Description = "Desc1" };
        var structure = new WikiStructure();
        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"score\": -0.5, \"reasoning\": \"Test\"}");

        // Act
        var result = await _service.EvaluateRequirementAsync(requirement, structure);

        // Assert
        Assert.That(result.MeanScore, Is.EqualTo(0.0), "Score below 0 should be clamped to 0");
    }

    [Test]
    public async Task EvaluateRequirementAsync_ShouldClampScoreAboveOne()
    {
        // Arrange
        var requirement = new RubricRequirement { Title = "Req1", Description = "Desc1" };
        var structure = new WikiStructure();
        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"score\": 1.5, \"reasoning\": \"Test\"}");

        // Act
        var result = await _service.EvaluateRequirementAsync(requirement, structure);

        // Assert
        Assert.That(result.MeanScore, Is.EqualTo(1.0), "Score above 1 should be clamped to 1");
    }

    [Test]
    public async Task EvaluateRequirementAsync_ShouldHandleNullReasoning()
    {
        // Arrange
        var requirement = new RubricRequirement { Title = "Req1", Description = "Desc1" };
        var structure = new WikiStructure();
        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"score\": 0.8}");

        // Act
        var result = await _service.EvaluateRequirementAsync(requirement, structure);

        // Assert
        Assert.That(result.Reasoning, Is.Not.Null);
        Assert.That(result.Reasoning[0], Is.EqualTo("No reasoning provided"));
    }

    [Test]
    public async Task EvaluateRequirementAsync_ShouldHandleNullEvidence()
    {
        // Arrange
        var requirement = new RubricRequirement { Title = "Req1", Description = "Desc1" };
        var structure = new WikiStructure();
        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"score\": 0.8, \"reasoning\": \"Good\"}");

        // Act
        var result = await _service.EvaluateRequirementAsync(requirement, structure);

        // Assert
        Assert.That(result.Evidence, Is.Not.Null);
        Assert.That(result.Evidence, Is.Empty);
    }

    [Test]
    public async Task EvaluateRequirementsAsync_ShouldTrackFailedModels()
    {
        // Arrange
        var requirements = new List<RubricRequirement>
        {
            new() { Title = "Req1", Description = "Desc1" }
        };
        var structure = new WikiStructure();
        var judgeModels = new List<string> { "model-a", "model-b", "model-c" };

        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                "model-a",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"score\": 0.8, \"reasoning\": \"Good\"}");

        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                "model-b",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Model unavailable"));

        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                "model-c",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"score\": 0.9, \"reasoning\": \"Excellent\"}");

        // Act
        var results = await _service.EvaluateRequirementsAsync(requirements, structure, judgeModels);

        // Assert
        Assert.That(results[0].FailedModels, Has.Count.EqualTo(1));
        Assert.That(results[0].FailedModels, Does.Contain("model-b"));
    }

    [Test]
    public async Task EvaluateRequirementsAsync_ShouldCalculateVarianceCorrectly()
    {
        // Arrange
        var requirements = new List<RubricRequirement>
        {
            new() { Title = "Req1", Description = "Desc1" }
        };
        var structure = new WikiStructure();
        var judgeModels = new List<string> { "model-a", "model-b", "model-c" };

        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                "model-a",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"score\": 0.6, \"reasoning\": \"OK\"}");

        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                "model-b",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"score\": 0.8, \"reasoning\": \"Good\"}");

        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                "model-c",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"score\": 1.0, \"reasoning\": \"Perfect\"}");

        // Act
        var results = await _service.EvaluateRequirementsAsync(requirements, structure, judgeModels);

        // Assert
        // Mean = (0.6 + 0.8 + 1.0) / 3 = 0.8
        Assert.That(results[0].MeanScore, Is.EqualTo(0.8).Within(0.001));
        // Variance = ((0.6-0.8)^2 + (0.8-0.8)^2 + (1.0-0.8)^2) / 3 = 0.0267
        // StdDev = sqrt(0.0267) ≈ 0.163
        Assert.That(results[0].StandardDeviation, Is.EqualTo(0.163).Within(0.01));
    }

    [Test]
    public async Task EvaluateRequirementAsync_ShouldHandleMultipleJsonBlocksInResponse()
    {
        // Arrange
        var requirement = new RubricRequirement { Title = "Req1", Description = "Desc1" };
        var structure = new WikiStructure();
        var response =
            "Here's my analysis:\n```json\n{\"score\": 0.85, \"reasoning\": \"First JSON\"}\n```\nAnd another:\n```json\n{\"other\": \"data\"}\n```";

        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _service.EvaluateRequirementAsync(requirement, structure);

        // Assert - Should extract first valid JSON block
        Assert.That(result.MeanScore, Is.EqualTo(0.85));
    }

    [Test]
    public async Task EvaluateRequirementAsync_ShouldRecoverFromMalformedJson()
    {
        // Arrange
        var requirement = new RubricRequirement { Title = "Req1", Description = "Desc1" };
        var structure = new WikiStructure();
        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("{invalid json syntax}}");

        // Act
        var result = await _service.EvaluateRequirementAsync(requirement, structure);

        // Assert
        Assert.That(result.MeanScore, Is.EqualTo(0.0));
        Assert.That(result.Reasoning, Does.Contain("Failed to evaluate requirement"));
    }

    [Test]
    public async Task EvaluateRequirementAsync_ShouldHandleChattyResponse()
    {
        // Arrange
        var requirement = new RubricRequirement { Title = "Req1", Description = "Desc1" };
        var structure = new WikiStructure();
        var response =
            "Okay, I have analyzed the documentation. Here is the JSON you requested:\n\n{ \"score\": 0.95, \"reasoning\": \"Extremely clear\", \"evidence\": [] }\n\nHope this helps!";

        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _service.EvaluateRequirementAsync(requirement, structure);

        // Assert
        Assert.That(result.MeanScore, Is.EqualTo(0.95));
        Assert.That(result.Reasoning[0], Is.EqualTo("Extremely clear"));
    }

    [Test]
    public async Task EvaluateRequirementsAsync_ShouldRunCorrectlyWithConcurrencyParameter()
    {
        // Arrange
        var requirements = new List<RubricRequirement>
        {
            new() { Title = "Req1", Description = "Desc1" },
            new() { Title = "Req2", Description = "Desc2" }
        };
        var structure = new WikiStructure();
        var judgeModels = new List<string> { "model-a" };

        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"score\": 0.9, \"reasoning\": \"Fast\"}");

        // Act
        var results = await _service.EvaluateRequirementsAsync(requirements, structure, judgeModels, 2);

        // Assert
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0].MeanScore, Is.EqualTo(0.9));
        Assert.That(results[1].MeanScore, Is.EqualTo(0.9));
    }
}