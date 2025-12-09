using System.Text.RegularExpressions;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class JudgeAgentServiceTests
{
    private Mock<ILLMClient> _mockLLMClient;
    private Mock<ILogger<IJudgeAgent>> _mockLogger;
    private JudgeAgentService _judgeAgentService;
    private WikiPage _testPage;
    private RubricRequirement _testRequirement;

    [SetUp]
    public void SetUp()
    {
        _mockLLMClient = new Mock<ILLMClient>();
        _mockLogger = new Mock<ILogger<IJudgeAgent>>();
        _judgeAgentService = new JudgeAgentService(_mockLogger.Object, _mockLLMClient.Object);

        _testPage = new WikiPage
        {
            Id = "test-page",
            Title = "Test Component",
            Content = "This is a test component with comprehensive documentation including examples and API references.",
            Description = "Test component description"
        };

        _testRequirement = new RubricRequirement
        {
            Title = "Clarity",
            Description = "The documentation should be clear and easy to understand",
            Weight = 1.0,
            IsLeaf = true
        };
    }

    [Test]
    public async Task EvaluateRequirementAsync_WithPositiveResponse_ReturnsScoreOne()
    {
        // Arrange
        var mockResponse = "Yes. The documentation provides a system overview with clear explanations.";
        _mockLLMClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _judgeAgentService.EvaluateRequirementAsync(_testPage, _testRequirement);

        // Assert
        Assert.That(result.Score, Is.EqualTo(1.0));
        Assert.That(result.RequirementId, Is.EqualTo(_testRequirement.Title));
        Assert.That(result.Reasoning, Is.EqualTo("The documentation provides a system overview with clear explanations."));
        
        _mockLLMClient.Verify(x => x.ChatAsync(
            It.Is<string>(s => s.Contains("You are an expert technical documentation evaluator")),
            It.Is<string>(s => s.Contains(_testPage.Content) && s.Contains(_testRequirement.Description)),
            It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task EvaluateRequirementAsync_WithNegativeResponse_ReturnsScoreZero()
    {
        // Arrange
        var mockResponse = "No. The documentation is too brief and lacks comprehensive details.";
        _mockLLMClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _judgeAgentService.EvaluateRequirementAsync(_testPage, _testRequirement);

        // Assert
        Assert.That(result.Score, Is.EqualTo(0.0));
        Assert.That(result.RequirementId, Is.EqualTo(_testRequirement.Title));
        Assert.That(result.Reasoning, Is.EqualTo("The documentation is too brief and lacks comprehensive details."));
    }

    [Test]
    public async Task EvaluateRequirementAsync_WithAmbiguousResponse_ParsesConfidenceScore()
    {
        // Arrange
        var mockResponse = "Partially yes. The documentation has some clear sections but overall lacks consistency. I would say 60% meets the requirement.";
        _mockLLMClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _judgeAgentService.EvaluateRequirementAsync(_testPage, _testRequirement);

        // Assert
        Assert.That(result.Score, Is.EqualTo(0.6).Within(0.01));
        Assert.That(result.Reasoning, Is.EqualTo(mockResponse));
    }

    [Test]
    public async Task EvaluateRequirementAsync_WithPercentageInResponse_ExtractsPercentage()
    {
        // Arrange
        var mockResponse = "The documentation meets about 75% of the requirement. Some sections are clear but others need improvement.";
        _mockLLMClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _judgeAgentService.EvaluateRequirementAsync(_testPage, _testRequirement);

        // Assert
        Assert.That(result.Score, Is.EqualTo(0.75).Within(0.01));
    }

    [Test]
    public async Task EvaluateRequirementAsync_WithDecimalScore_ExtractsDecimalScore()
    {
        // Arrange
        var mockResponse = "Score: 0.85. The documentation is very well written and comprehensive.";
        _mockLLMClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _judgeAgentService.EvaluateRequirementAsync(_testPage, _testRequirement);

        // Assert
        Assert.That(result.Score, Is.EqualTo(0.85).Within(0.01));
    }

    [Test]
    public async Task EvaluateRequirementAsync_WithInvalidResponse_ReturnsDefaultScore()
    {
        // Arrange
        var mockResponse = "I am unable to evaluate this documentation.";
        _mockLLMClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _judgeAgentService.EvaluateRequirementAsync(_testPage, _testRequirement);

        // Assert
        Assert.That(result.Score, Is.EqualTo(0.5)); // Default middle score
        Assert.That(result.Reasoning, Is.EqualTo(mockResponse));
    }

    [Test]
    public async Task EvaluateRequirementAsync_WithEmptyResponse_ReturnsDefaultScore()
    {
        // Arrange
        _mockLLMClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        // Act
        var result = await _judgeAgentService.EvaluateRequirementAsync(_testPage, _testRequirement);

        // Assert
        Assert.That(result.Score, Is.EqualTo(0.5));
        Assert.That(result.Reasoning, Is.EqualTo("No response provided"));
    }

    [Test]
    public void EvaluateRequirementAsync_WithNullPage_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(
            () => _judgeAgentService.EvaluateRequirementAsync(null!, _testRequirement));
    }

    [Test]
    public void EvaluateRequirementAsync_WithNullRequirement_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(
            () => _judgeAgentService.EvaluateRequirementAsync(_testPage, null!));
    }

    [Test]
    public async Task EvaluateRequirementAsync_WithLLMException_LogsErrorAndReturnsDefaultScore()
    {
        // Arrange
        var exception = new InvalidOperationException("LLM service unavailable");
        _mockLLMClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _judgeAgentService.EvaluateRequirementAsync(_testPage, _testRequirement);

        // Assert
        Assert.That(result.Score, Is.EqualTo(0.5));
        Assert.That(result.Reasoning, Contains.Substring("Error evaluating requirement"));
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error evaluating requirement")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task EvaluateRequirementAsync_ConstructsCorrectPrompt()
    {
        // Arrange
        var mockResponse = "Yes, the requirement is met.";
        _mockLLMClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        await _judgeAgentService.EvaluateRequirementAsync(_testPage, _testRequirement);

        // Assert
        _mockLLMClient.Verify(x => x.ChatAsync(
            It.Is<string>(systemPrompt => systemPrompt.Contains("expert technical documentation evaluator") && 
                                         systemPrompt.Contains("binary decision")),
            It.Is<string>(userPrompt => userPrompt.Contains("Documentation Content:") &&
                                       userPrompt.Contains(_testPage.Content) &&
                                       userPrompt.Contains("Requirement:") &&
                                       userPrompt.Contains(_testRequirement.Description) &&
                                       userPrompt.Contains("Answer with either") &&
                                       userPrompt.Contains("Yes")),
            It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task EvaluateRequirementAsync_WithMultipleScores_UsesFirstValidScore()
    {
        // Arrange
        var mockResponse = "The documentation scores 0.7, but some sections are 0.8 and others are 0.6. Overall it's good.";
        _mockLLMClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _judgeAgentService.EvaluateRequirementAsync(_testPage, _testRequirement);

        // Assert
        Assert.That(result.Score, Is.EqualTo(0.7).Within(0.01));
    }

    [Test]
    public async Task EvaluateRequirementAsync_WithScoreAboveOne_ClampsToOne()
    {
        // Arrange
        var mockResponse = "The documentation is perfect, I give it a score of 1.2 out of 1.0.";
        _mockLLMClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _judgeAgentService.EvaluateRequirementAsync(_testPage, _testRequirement);

        // Assert
        Assert.That(result.Score, Is.EqualTo(1.0));
    }

    [Test]
    public async Task EvaluateRequirementAsync_WithNegativeScore_ClampsToZero()
    {
        // Arrange
        var mockResponse = "The documentation is terrible, I give it a score of -0.2.";
        _mockLLMClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _judgeAgentService.EvaluateRequirementAsync(_testPage, _testRequirement);

        // Assert
        Assert.That(result.Score, Is.EqualTo(0.0));
    }

    [TestCase("YES, absolutely", 1.0)]
    [TestCase("yes, it meets the requirement", 1.0)]
    [TestCase("Yes, with some reservations", 1.0)]
    [TestCase("NO, it does not meet", 0.0)]
    [TestCase("no, completely inadequate", 0.0)]
    [TestCase("No, fails to meet requirement", 0.0)]
    public async Task EvaluateRequirementAsync_WithVariousYesNoResponses_ReturnsCorrectScore(string response, double expectedScore)
    {
        // Arrange
        _mockLLMClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _judgeAgentService.EvaluateRequirementAsync(_testPage, _testRequirement);

        // Assert
        Assert.That(result.Score, Is.EqualTo(expectedScore));
    }

    [TestCase("meets about 80% of requirements", 0.8)]
    [TestCase("approximately 65% complete", 0.65)]
    [TestCase("covers 90 percent of the requirement", 0.9)]
    [TestCase("score is 0.45", 0.45)]
    [TestCase("rating: 0.72 out of 1.0", 0.72)]
    public async Task EvaluateRequirementAsync_WithVariousScoreFormats_ExtractsCorrectScore(string response, double expectedScore)
    {
        // Arrange
        _mockLLMClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _judgeAgentService.EvaluateRequirementAsync(_testPage, _testRequirement);

        // Assert
        Assert.That(result.Score, Is.EqualTo(expectedScore).Within(0.01));
    }
}