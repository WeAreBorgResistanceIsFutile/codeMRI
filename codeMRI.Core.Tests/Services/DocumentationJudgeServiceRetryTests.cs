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

        // Use real JsonRepairService for integration testing
        var jsonRepairService = new JsonRepairService();

        _service = new DocumentationJudgeService(
            _mockLogger.Object,
            _mockLlmFacade.Object,
            _mockMeterFactory.Object,
            _mockPromptBuilder.Object,
            _mockRagPromptBuilder.Object,
            jsonRepairService);
    }

    [Test]
    public async Task EvaluateRequirementAsync_ShouldReturnDefaultAssessment_WhenJsonIsInvalid()
    {
        // Arrange
        var requirement = new RubricRequirement { Title = "Req1", Description = "Test Requirement" };
        var structure = new WikiStructure();
        
        // Invalid JSON that JsonRepairService cannot parse (unclosed braces/strings)
        var invalidJson = @"
        {
            ""requirement_id"": ""Req1"",
            ""score"": 0.5,
            ""reasoning"": ""Broken JSON...,
            ""evidence"": [ ""Should fail"" ]
        "; // Missing closing quote for reasoning and closing braces

        // Setup to return invalid JSON
        _mockLlmFacade.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<MessageCompositionOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse { Content = invalidJson, StrategyUsed = "Simple" });

        // Act
        var result = await _service.EvaluateRequirementAsync(requirement, structure);

        // Assert
        // JsonRepairService gracefully handles invalid JSON by returning null,
        // which causes the service to return a default assessment with score 0
        Assert.That(result.MeanScore, Is.EqualTo(0.0), "Should return default assessment for invalid JSON");
        Assert.That(result.Reasoning.First(), Is.EqualTo("Failed to evaluate requirement"));
        
        // Verify LLM was called once (no retry since JsonRepairService doesn't throw)
        _mockLlmFacade.Verify(x => x.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<ChatMessage>>(),
            It.IsAny<MessageCompositionOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once, "Should have called LLM once");
    }
}
