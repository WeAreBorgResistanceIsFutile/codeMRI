using NUnit.Framework;
using Moq;
using Microsoft.Extensions.Logging;
using codeMRI.Core.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Services.MessageComposition;
using codeMRI.Core.Models;
using System.Text.Json;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class NavigationStructureServiceTests
{
    private Mock<ILLMServiceFacade> _mockLlmFacade;
    private Mock<ILogger<NavigationStructureService>> _mockLogger;
    private NavigationStructureService _service;

    [SetUp]
    public void Setup()
    {
        _mockLlmFacade = new Mock<ILLMServiceFacade>();
        _mockLogger = new Mock<ILogger<NavigationStructureService>>();
        _service = new NavigationStructureService(_mockLlmFacade.Object, _mockLogger.Object);
    }

    [Test]
    public async Task GenerateDocumentationStructureAsync_ShouldPopulateMapFromSections_WhenExplicitMapIsMissing()
    {
        // Arrange
        var moduleTree = new ModuleTree { Root = new ModuleNode { Id = "root", Name = "Root" } };
        var repoInfo = new RepositoryInfo { Name = "TestRepo" };
        
        var moduleId = "module-123";
        var sectionId = "section-456";
        
        // JSON response where the section implicitly owns the module, but ModuleMapping is empty
        var jsonResponse = $$"""
            {
                "Title": "Test Docs",
                "Sections": [
                    {
                        "Id": "{{sectionId}}",
                        "Title": "My Section",
                        "ModuleIds": ["{{moduleId}}"],
                        "SubSections": []
                    }
                ],
                "ModuleMapping": {} 
            }
            """;

        _mockLlmFacade
            .Setup(x => x.ExecuteAsync(
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<List<ChatMessage>>(), 
                It.IsAny<MessageCompositionOptions>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse { Content = "```json\n" + jsonResponse + "\n```", StrategyUsed = "Test" });

        // Act
        var result = await _service.GenerateDocumentationStructureAsync(moduleTree, repoInfo);

        // Assert
        Assert.That(result.ModuleToSectionMap, Contains.Key(moduleId), "Module map should contain the module ID backfilled from the section.");
        Assert.That(result.ModuleToSectionMap[moduleId], Is.EqualTo(sectionId), "Module should act mapped to the correct section ID.");
    }
}
