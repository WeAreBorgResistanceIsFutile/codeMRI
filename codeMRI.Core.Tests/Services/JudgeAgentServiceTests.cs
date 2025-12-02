using codeMRI.Core.Interfaces;
using codeMRI.Core.Services;
using codeMRI.Shared.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class JudgeAgentServiceTests
{
    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<IJudgeAgent>>();
        _mockLlmClient = new Mock<ILLMClient>();
        _service = new JudgeAgentService(_mockLogger.Object, _mockLlmClient.Object);
    }

    private Mock<ILogger<IJudgeAgent>> _mockLogger;
    private Mock<ILLMClient> _mockLlmClient;
    private IJudgeAgent _service;

    [Test]
    public async Task EvaluateRequirementAsync_ShouldReturnScoreAndReasoning_WhenValidInputProvided()
    {
        // Arrange
        var page = new WikiPage
        {
            Id = "TestPage",
            Title = "Test Documentation",
            Content = "This documentation explains how the system works."
        };

        var requirement = new RubricRequirement
        {
            Title = "Test Requirement",
            Description = "Documentation should include system overview",
            Weight = 0.5,
            IsLeaf = true
        };

        var expectedResponse = "Yes. The documentation provides a system overview with clear explanations.";
        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.Is<string>(p => p.Contains("documentation") && p.Contains(requirement.Description)),
                It.IsAny<List<ChatMessage>>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _service.EvaluateRequirementAsync(page, requirement);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Score, Is.EqualTo(1.0));
        Assert.That(result.Reasoning,
            Is.EqualTo("The documentation provides a system overview with clear explanations."));
        Assert.That(result.RequirementId, Is.EqualTo(requirement.Title));
    }

    [Test]
    public async Task EvaluateRequirementAsync_ShouldReturnScoreZero_WhenResponseIsNo()
    {
        // Arrange
        var page = new WikiPage
        {
            Content = "Brief documentation."
        };

        var requirement = new RubricRequirement
        {
            Description = "Documentation should be comprehensive",
            IsLeaf = true
        };

        var expectedResponse = "No. The documentation is too brief and lacks comprehensive details.";
        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _service.EvaluateRequirementAsync(page, requirement);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Score, Is.EqualTo(0.0));
        Assert.That(result.Reasoning, Is.EqualTo("The documentation is too brief and lacks comprehensive details."));
    }

    [Test]
    public async Task EvaluateRequirementAsync_ShouldHandleLlmException_Gracefully()
    {
        // Arrange
        var page = new WikiPage();
        var requirement = new RubricRequirement { Description = "Test", IsLeaf = true };
        _mockLlmClient.Setup(x => x.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>()))
            .ThrowsAsync(new Exception("LLM service unavailable"));

        // Act
        var result = await _service.EvaluateRequirementAsync(page, requirement);

        // Assert
        Assert.That(result.Score, Is.EqualTo(0.0));
        Assert.That(result.Reasoning, Is.EqualTo("LLM evaluation failed: LLM service unavailable"));

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("LLM evaluation failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}