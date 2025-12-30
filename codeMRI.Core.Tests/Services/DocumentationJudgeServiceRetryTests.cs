using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using codeMRI.Core.Services.MessageComposition;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics.Metrics;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class DocumentationJudgeServiceRetryTests
{
    private Mock<ILLMServiceFacade> _mockLlmFacade;
    private Mock<ILogger<DocumentationJudgeService>> _mockLogger;
    private Mock<IMeterFactory> _mockMeterFactory;
    private Mock<IEvaluationPromptBuilder> _mockPromptBuilder;
    private Mock<RagEvaluationPromptBuilder> _mockRagPromptBuilder;
    private DocumentationJudgeService _service;

    [SetUp]
    public void Setup()
    {
        _mockLlmFacade = new Mock<ILLMServiceFacade>();
        _mockLogger = new Mock<ILogger<DocumentationJudgeService>>();
        _mockMeterFactory = new Mock<IMeterFactory>();
        _mockPromptBuilder = new Mock<IEvaluationPromptBuilder>();
        
        // Mock dependencies for RagEvaluationPromptBuilder
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockVectorStore = new Mock<IVectorStoreService>();
        var mockRagLogger = new Mock<ILogger<RagEvaluationPromptBuilder>>();
        
        _mockRagPromptBuilder = new Mock<RagEvaluationPromptBuilder>(
            mockEmbedding.Object, mockVectorStore.Object, mockRagLogger.Object);

        // Setup meter factory
        _mockMeterFactory.Setup(x => x.Create(It.IsAny<MeterOptions>()))
            .Returns(new Meter("TestMeter"));

        // Setup generic prompt builder
        _mockPromptBuilder.Setup(x => x.BuildPromptAsync(It.IsAny<RubricRequirement>(), It.IsAny<WikiStructure>()))
            .ReturnsAsync("Mock Prompt");

        _service = new DocumentationJudgeService(
            _mockLogger.Object,
            _mockLlmFacade.Object,
            _mockMeterFactory.Object,
            _mockPromptBuilder.Object,
            _mockRagPromptBuilder.Object);
    }

    [Test]
    public async Task EvaluateRequirementAsync_ShouldRetryAndSucceed_WhenFirstResponseHasInvalidEvidenceFormat()
    {
        // Arrange
        var requirement = new RubricRequirement { Title = "Req1", Description = "Test Requirement" };
        var structure = new WikiStructure();
        
        // 1. First response: Truly invalid JSON (unclosed brace/string)
        var invalidJson = @"
        {
            ""requirement_id"": ""Req1"",
            ""score"": 0.5,
            ""reasoning"": ""Broken JSON...,
            ""evidence"": [ ""Should fail"" ]
        "; // Missing closing quote for reasoning and closing braces

        // 2. Second response: Valid JSON
        var validJson = @"
        {
            ""requirement_id"": ""Req1"",
            ""score"": 0.95,
            ""reasoning"": ""Retry succeeded"",
            ""evidence"": [ ""Should work now"" ]
        }";

        // Setup sequence
        _mockLlmFacade.SetupSequence(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<MessageCompositionOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse { Content = invalidJson, StrategyUsed = "Simple" })
            .ReturnsAsync(new LLMResponse { Content = validJson, StrategyUsed = "Simple" });

        // Act
        var result = await _service.EvaluateRequirementAsync(requirement, structure);

        // Assert
        // Current behavior: It will catch the exception on first call, log error, and return default assessment (Score 0)
        // Desired behavior: It retries, gets the second valid response, and returns Score 0.95
        
        Assert.That(result.MeanScore, Is.EqualTo(0.95), "Should return the score from the successful retry");
        Assert.That(result.Reasoning.First(), Is.EqualTo("Retry succeeded"));
        
        // Verify LLM was called twice
        _mockLlmFacade.Verify(x => x.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<ChatMessage>>(),
            It.IsAny<MessageCompositionOptions?>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2), "Should have retried once");
    }
}
