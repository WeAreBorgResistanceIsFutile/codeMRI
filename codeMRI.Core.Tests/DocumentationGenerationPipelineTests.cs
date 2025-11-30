using Microsoft.Extensions.Logging;
using Moq;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Services;
using codeMRI.Shared.Models;

namespace codeMRI.Core.Tests;

public class DocumentationGenerationPipelineTests
{
    private readonly Mock<ILogger<ComponentIdentificationService>> _mockComponentLogger;
    private readonly Mock<ILogger<DocumentationGenerationPipeline>> _mockPipelineLogger;
    private readonly IComponentIdentificationService _componentService;
    private readonly string _testRepoPath;

    public DocumentationGenerationPipelineTests()
    {
        _mockComponentLogger = new Mock<ILogger<ComponentIdentificationService>>();
        _mockPipelineLogger = new Mock<ILogger<DocumentationGenerationPipeline>>();
        _componentService = new ComponentIdentificationService(_mockComponentLogger.Object);
        _testRepoPath = Path.Combine(Path.GetTempPath(), "test_repo");
    }

    [Fact]
    public async Task GenerateDocumentationAsync_ShouldReturnWikiStructure_WhenValidRepositoryProvided()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        
        var options = new DocumentationOptions
        {
            IncludeArchitecture = true,
            IncludeApiDocumentation = true,
            TargetAudience = "Developers"
        };

        // Act & Assert - This test will now pass since we implemented the service
        var pipeline = new DocumentationGenerationPipeline(_componentService, _mockPipelineLogger.Object);
        var result = await pipeline.GenerateDocumentationAsync(_testRepoPath, options);
        
        Assert.NotNull(result);
        Assert.Equal("test_repo Documentation", result.Title);
        
        // Cleanup
        if (Directory.Exists(_testRepoPath))
        {
            Directory.Delete(_testRepoPath, true);
        }
    }

    [Fact]
    public async Task GenerateComponentDocumentationAsync_ShouldReturnWikiPage_WhenValidComponentProvided()
    {
        // Arrange
        var component = new CodeComponent
        {
            Id = "test_component",
            Name = "TestComponent",
            Type = "Class",
            FilePath = "/test/path/TestComponent.cs",
            Language = "C#"
        };
        var context = new RepositoryStructure
        {
            Name = "TestRepo",
            Language = "C#"
        };

        // Act & Assert - This test will now pass since we implemented the service
        var pipeline = new DocumentationGenerationPipeline(_componentService, _mockPipelineLogger.Object);
        var result = await pipeline.GenerateComponentDocumentationAsync(component, context);
        
        Assert.NotNull(result);
        Assert.Equal("TestComponent", result.Title);
    }

    [Fact]
    public async Task GenerateOverviewPagesAsync_ShouldReturnListOfWikiPages_WhenValidStructureProvided()
    {
        // Arrange
        var structure = new RepositoryStructure
        {
            Name = "TestRepo",
            Language = "C#"
        };
        var components = new List<CodeComponent>
        {
            new CodeComponent { Id = "comp1", Name = "Component1", Type = "Class" },
            new CodeComponent { Id = "comp2", Name = "Component2", Type = "Interface" }
        };

        // Act & Assert - This test will now pass since we implemented the service
        var pipeline = new DocumentationGenerationPipeline(_componentService, _mockPipelineLogger.Object);
        var result = await pipeline.GenerateOverviewPagesAsync(structure, components);
        
        Assert.NotNull(result);
        Assert.Equal(2, result.Count); // Should generate architecture and components overview
    }
}