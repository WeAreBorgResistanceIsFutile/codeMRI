using codeMRI.Agents.Interfaces;
using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using codeMRI.Core.Models;
using codeMRI.Core.Interfaces;
using codeMRI.Shared.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace codeMRI.Core.Tests;

public class DocumentationGenerationPipelineTests
{
    private readonly Mock<IAgentCoordinator> _mockCoordinator;
    private readonly Mock<ILogger<DocumentationGenerationPipeline>> _mockLogger;
    private readonly DocumentationGenerationPipeline _pipeline;

    public DocumentationGenerationPipelineTests()
    {
        _mockCoordinator = new Mock<IAgentCoordinator>();
        _mockLogger = new Mock<ILogger<DocumentationGenerationPipeline>>();
        _pipeline = new DocumentationGenerationPipeline(_mockCoordinator.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GenerateDocumentationAsync_ShouldOrchestrateAgents()
    {
        // Arrange
        var repoPath = "/test/repo";
        var options = new DocumentationOptions();

        // Mock Analyzer
        var analysisResult = new AnalysisResult 
        { 
            Structure = new RepositoryStructure { Name = "TestRepo" }, 
            Components = new List<CodeComponent> { new CodeComponent { Id = "comp1", Name = "Component1" } } 
        };
        _mockCoordinator.Setup(c => c.CoordinateTaskAsync(It.Is<AgentTask>(t => t.Type == "Analyzer"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResult { Success = true, Output = analysisResult });

        // Mock Documenter
        _mockCoordinator.Setup(c => c.CoordinateTaskAsync(It.Is<AgentTask>(t => t.Type == "Documenter"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResult { Success = true, Output = new WikiPage { Id = "comp1", Title = "Component1" } });

        // Mock Synthesizer
        var expectedStructure = new WikiStructure { Title = "TestRepo Documentation" };
        _mockCoordinator.Setup(c => c.CoordinateTaskAsync(It.Is<AgentTask>(t => t.Type == "Synthesizer"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResult { Success = true, Output = expectedStructure });

        // Act
        var result = await _pipeline.GenerateDocumentationAsync(repoPath, options);

        // Assert
        Assert.Equal(expectedStructure, result);
        _mockCoordinator.Verify(c => c.CoordinateTaskAsync(It.Is<AgentTask>(t => t.Type == "Analyzer"), It.IsAny<CancellationToken>()), Times.Once);
        _mockCoordinator.Verify(c => c.CoordinateTaskAsync(It.Is<AgentTask>(t => t.Type == "Documenter"), It.IsAny<CancellationToken>()), Times.Once);
        _mockCoordinator.Verify(c => c.CoordinateTaskAsync(It.Is<AgentTask>(t => t.Type == "Synthesizer"), It.IsAny<CancellationToken>()), Times.Once);
    }
}