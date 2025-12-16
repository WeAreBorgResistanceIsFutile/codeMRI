using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CS8602

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class WikiGenerationServiceAudienceTests
{
    private Mock<ILLMClient>? _mockLlmClient;
    private Mock<IDiagramGenerator>? _mockDiagramGenerator;
    private Mock<IEnhancedDependencyGraphService>? _mockGraphService;
    private Mock<IDocumentationSynthesisService>? _mockSynthesisService;
    private Mock<IReferenceManagementService>? _mockRefService;
    private Mock<ILogger<WikiGenerationService>>? _mockLogger;
    private WikiGenerationService? _service;

    [SetUp]
    public void Setup()
    {
        _mockLlmClient = new Mock<ILLMClient>();
        _mockDiagramGenerator = new Mock<IDiagramGenerator>();
        _mockGraphService = new Mock<IEnhancedDependencyGraphService>();
        _mockLogger = new Mock<ILogger<WikiGenerationService>>();
        _mockSynthesisService = new Mock<IDocumentationSynthesisService>();
        _mockRefService = new Mock<IReferenceManagementService>();

        _service = new WikiGenerationService(
            _mockLlmClient!.Object,
            _mockDiagramGenerator!.Object,
            _mockGraphService!.Object,
            _mockSynthesisService!.Object,
            _mockRefService!.Object,
            _mockLogger!.Object,
            "dummy_model");
    }

    [Test]
    public async Task GenerateEnhancedPageAsync_ShouldUseUserGuidePrompt_WhenAudienceIsUser()
    {
        // Arrange
        var module = new ModuleNode { Name = "TestModule", Components = new HashSet<string> { "File.cs" } };
        var context = new ModulePageContext();
        var fileContents = new Dictionary<string, string> { { "File.cs", "content" } };
        
        _mockLlmClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, List<ChatMessage>, string?, CancellationToken>((sys, prompt, hist, model, token) => 
            {
                TestContext.WriteLine($"Generated System Prompt: {sys}");
                TestContext.WriteLine($"Generated User Prompt: {prompt}");
            })
            .ReturnsAsync("# TestModule\nUser guide content");
            
        _mockRefService.Setup(x => x.EnrichContentWithLinks(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((c, id) => c);

        // Act
        await _service.GenerateEnhancedPageAsync(module, null, context, fileContents, "English", null, AudienceType.User);

        // Assert
        // Verify prompt contains audience specific text
        _mockLlmClient.Verify(x => x.ChatAsync(
            It.IsAny<string>(),
            It.Is<string>(p => p.Contains("User audience") && p.Contains("Key Capabilities")),
            It.IsAny<List<ChatMessage>>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
            
        // Verify DIAGRAMS are NOT generated for User audience (assuming Implementation Plan said so, confirming I implemented it)
        _mockDiagramGenerator.Verify(x => x.GenerateComponentDiagramAsync(It.IsAny<EnhancedDependencyGraph>(), It.IsAny<string>()), Times.Never);
    }
    
    [Test]
    public async Task GenerateEnhancedPageAsync_ShouldUseDeveloperPrompt_WhenAudienceIsDeveloper()
    {
        // Arrange
        var module = new ModuleNode { Name = "TestModule", Components = new HashSet<string> { "File.cs" } };
        var context = new ModulePageContext();
        var fileContents = new Dictionary<string, string> { { "File.cs", "content" } };
        
        _mockLlmClient.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# TestModule\nDev content");
            
        _mockRefService.Setup(x => x.EnrichContentWithLinks(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((c, id) => c);

        _mockGraphService.Setup(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnhancedDependencyGraph()); // Empty graph but returning one to allow flow to continue

        // Act
        await _service.GenerateEnhancedPageAsync(module, null, context, fileContents, "English", null, AudienceType.Developer);

        // Assert
        // Verify prompt contains Developer specific metrics
        _mockLlmClient.Verify(x => x.ChatAsync(
            It.IsAny<string>(),
            It.Is<string>(p => p.Contains("Quality Metrics") && p.Contains("Cohesion")),
            It.IsAny<List<ChatMessage>>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
            
        // Verify attempt to generate diagrams (it tries, even if graph empty it calls BuildGraph, logic might skip actual diagram gen if node count 0 but intent is there)
        // My implementation adds Try-Catch block calling BuildGraph only if Audience == Developer.
        _mockGraphService.Verify(x => x.BuildGraphAsync(It.IsAny<List<CodeComponent>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GenerateParentPageAsync_ShouldPassAudienceToSynthesisService()
    {
        // Arrange
        var module = new ModuleNode { Name = "ParentModule" };
        var childPages = new List<WikiPage>();
        var audience = AudienceType.DevOps;
        
        _mockSynthesisService.Setup(x => x.SynthesizeParentPageAsync(It.IsAny<ModuleNode>(), It.IsAny<List<WikiPage>>(), It.IsAny<string>(), It.IsAny<AudienceType>()))
            .ReturnsAsync(new WikiPage { Content = "Content" });
            
        _mockRefService.Setup(x => x.EnrichContentWithLinks(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((c, id) => c);

        // Act
        await _service.GenerateParentPageAsync(module, childPages, "English", audience);

        // Assert
        _mockSynthesisService.Verify(x => x.SynthesizeParentPageAsync(module, childPages, "English", audience), Times.Once);
        
        // Assert Diagrams skipped for non-developer
        _mockDiagramGenerator.Verify(x => x.GenerateArchitectureDiagramAsync(It.IsAny<ModuleTree>(), It.IsAny<EnhancedDependencyGraph>()), Times.Never);
    }
}
