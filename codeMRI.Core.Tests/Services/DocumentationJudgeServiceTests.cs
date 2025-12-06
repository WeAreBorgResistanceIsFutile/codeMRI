using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class DocumentationJudgeServiceTests
{
    private Mock<ILLMClient> _mockLlmClient;
    private Mock<ILogger<DocumentationJudgeService>> _mockLogger;
    private DocumentationJudgeService _service;

    [SetUp]
    public void Setup()
    {
        _mockLlmClient = new Mock<ILLMClient>();
        _mockLogger = new Mock<ILogger<DocumentationJudgeService>>();
        _service = new DocumentationJudgeService(_mockLogger.Object, _mockLlmClient.Object);
    }

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
                It.IsAny<string?>()))
            .ReturnsAsync("{\"score\": 0.8, \"reasoning\": \"Good\"}");

        // Act
        await _service.EvaluateRequirementsAsync(requirements, structure, judgeModels);

        // Assert
        _mockLlmClient.Verify(x => x.ChatAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<ChatMessage>>(),
            "model-a"), Times.Once, "Should call ChatAsync with model-a");

        _mockLlmClient.Verify(x => x.ChatAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<ChatMessage>>(),
            "model-b"), Times.Once, "Should call ChatAsync with model-b");
    }
}
