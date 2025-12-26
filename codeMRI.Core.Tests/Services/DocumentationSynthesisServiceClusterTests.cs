using NUnit.Framework;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using codeMRI.Core.Services.MessageComposition;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace codeMRI.Core.Tests.Services;

/// <summary>
///     Tests for cluster synthesis functionality in DocumentationSynthesisService.
///     This is the failing test that reproduces the cluster synthesis bug.
/// </summary>
[TestFixture]
public class DocumentationSynthesisServiceClusterTests
{
    [SetUp]
    public void Setup()
    {
        _mockLlmFacade = new Mock<ILLMServiceFacade>();
        _mockValidator = new Mock<ILLMValidator>();
        _mockLogger = new Mock<ILogger<DocumentationSynthesisService>>();

        _mockValidator.Setup(v => v.ContextSize).Returns(4096);

        var options = Options.Create(new CodeWikiOptions());
        _service = new DocumentationSynthesisService(
            _mockLlmFacade.Object,
            _mockValidator.Object,
            _mockLogger.Object,
            options);
    }

    private Mock<ILLMServiceFacade> _mockLlmFacade;
    private Mock<ILLMValidator> _mockValidator;
    private Mock<ILogger<DocumentationSynthesisService>> _mockLogger;
    private DocumentationSynthesisService _service;

    [Test]
    public async Task SynthesizeParentPageAsync_ShouldUseMergePrompt_WhenMergeChildContentIsTrue()
    {
        // Arrange
        var module = new ModuleNode
        {
            Id = "parent_module",
            Name = "UserAuthentication",
            Level = 2,
            Description = "User authentication module",
            IsLeaf = false
        };
        module.Metadata["HasClusterChildren"] = "true";

        var clusterPage1 = new WikiPage
        {
            Id = "cluster1",
            Title = "UserAuthentication - Cluster_0",
            Content = "# UserAuthentication - Cluster_0\n\n## Overview\nHandles user login.\n\n## Components\n- LoginController\n- AuthService",
            RelevantFiles = new List<string> { "LoginController.cs", "AuthService.cs" }
        };

        var clusterPage2 = new WikiPage
        {
            Id = "cluster2",
            Title = "UserAuthentication - Cluster_1",
            Content = "# UserAuthentication - Cluster_1\n\n## Overview\nHandles JWT tokens.\n\n## Components\n- TokenManager\n- JwtService",
            RelevantFiles = new List<string> { "TokenManager.cs", "JwtService.cs" }
        };

        var childPages = new List<WikiPage> { clusterPage1, clusterPage2 };

        // Mock the LLM response for the MergeClusterPagesPrompt
        _mockLlmFacade.Setup(x => x.ExecuteAsync(
                It.Is<string>(s => s.Contains("content synthesis and organization")),
                It.Is<string>(s => s.Contains("Merge documentation") && s.Contains("cluster pages")),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<MessageCompositionOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse
            {
                Content = @"# UserAuthentication

## Overview
This module handles both user login and JWT token management.

## Components

### Authentication
- LoginController: Handles login requests
- AuthService: Core authentication logic

### Token Management
- TokenManager: Manages token lifecycle
- JwtService: JWT token generation and validation",
                StrategyUsed = "Simple"
            });

        // Act
        var result = await _service.SynthesizeParentPageAsync(
            module,
            childPages,
            "English",
            AudienceType.Developer,
            mergeChildContent: true);

        // Assert
        Assert.That(result, Is.Not.Null, "Result should not be null");
        Assert.That(result.Title, Is.EqualTo("UserAuthentication"), "Title should match module name");
        Assert.That(result.Content, Does.Contain("# UserAuthentication"), "Content should have main title");
        Assert.That(result.Content, Does.Contain("LoginController"), "Content should include components from cluster 1");
        Assert.That(result.Content, Does.Contain("TokenManager"), "Content should include components from cluster 2");

        // Verify metadata indicates this was merged from clusters
        Assert.That(result.Metadata, Is.Not.Null, "Metadata should not be null");
        Assert.That(result.Metadata.ContainsKey("IsMergedFromClusters"), Is.True, "Metadata should indicate merge");
        Assert.That(result.Metadata["IsMergedFromClusters"], Is.EqualTo(true), "IsMergedFromClusters should be true");
        Assert.That(result.Metadata["ClusterCount"], Is.EqualTo(2), "ClusterCount should be 2");

        // Verify relevant files from both clusters are included
        Assert.That(result.RelevantFiles, Contains.Item("LoginController.cs"), "Should include files from cluster 1");
        Assert.That(result.RelevantFiles, Contains.Item("TokenManager.cs"), "Should include files from cluster 2");
        Assert.That(result.RelevantFiles.Count, Is.EqualTo(4), "Should have all 4 unique files");

        // Verify the merge prompt was used (checking system prompt for specialization keywords)
        _mockLlmFacade.Verify(x => x.ExecuteAsync(
            It.Is<string>(s => s.Contains("content synthesis and organization")),
            It.Is<string>(s => s.Contains("Merge documentation") && s.Contains("cluster pages")),
            It.IsAny<List<ChatMessage>>(),
            It.IsAny<MessageCompositionOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once, "Should call LLM with merge prompt exactly once");
    }

    [Test]
    public async Task SynthesizeParentPageAsync_ShouldNotUseMergePrompt_WhenMergeChildContentIsFalse()
    {
        // Arrange
        var module = new ModuleNode
        {
            Id = "parent_module",
            Name = "UserManagement",
            Level = 2
        };

        var childPage = new WikiPage
        {
            Id = "child1",
            Title = "UserProfile",
            Content = "# UserProfile\n\nUser profile management"
        };

        var childPages = new List<WikiPage> { childPage };

        // Mock the LLM response for standard ParentPageSynthesisPrompt
        _mockLlmFacade.Setup(x => x.ExecuteAsync(
                It.Is<string>(s => s.Contains("master software architect")),
                It.Is<string>(s => s.Contains("Child Modules")),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<MessageCompositionOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse
            {
                Content = "# UserManagement\n\nOverview of user management system",
                StrategyUsed = "Simple"
            });

        // Act
        var result = await _service.SynthesizeParentPageAsync(
            module,
            childPages,
            "English",
            AudienceType.Developer,
            mergeChildContent: false);

        // Assert
        Assert.That(result, Is.Not.Null);

        // Verify the standard synthesis prompt was used, NOT the merge prompt
        _mockLlmFacade.Verify(x => x.ExecuteAsync(
            It.Is<string>(s => s.Contains("master software architect")),
            It.Is<string>(s => s.Contains("Child Modules")),
            It.IsAny<List<ChatMessage>>(),
            It.IsAny<MessageCompositionOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify merge prompt was NOT used
        _mockLlmFacade.Verify(x => x.ExecuteAsync(
            It.Is<string>(s => s.Contains("content synthesis and organization")),
            It.Is<string>(s => s.Contains("Merge documentation")),
            It.IsAny<List<ChatMessage>>(),
            It.IsAny<MessageCompositionOptions?>(),
            It.IsAny<CancellationToken>()), Times.Never, "Merge prompt should not be used when mergeChildContent is false");

        // Metadata should NOT indicate cluster merge
        Assert.That(result.Metadata?.ContainsKey("IsMergedFromClusters") ?? false, Is.False,
            "Metadata should not indicate cluster merge");
    }

    [Test]
    public async Task SynthesizeParentPageAsync_ShouldHandleEmptyChildPages_WhenMergeChildContentIsTrue()
    {
        // Arrange
        var module = new ModuleNode
        {
            Id = "empty_module",
            Name = "EmptyModule",
            IsLeaf = false
        };

        var childPages = new List<WikiPage>(); // Empty list

        // Mock LLM response for simple generation (no children)
        _mockLlmFacade.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.Is<string>(s => s.Contains("no child modules detected")),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<MessageCompositionOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse
            {
                Content = "# EmptyModule\n\nNo children detected.",
                StrategyUsed = "Simple"
            });

        // Act
        var result = await _service.SynthesizeParentPageAsync(
            module,
            childPages,
            "English",
            AudienceType.Developer,
            mergeChildContent: true); // Even with merge flag true, empty children should use simple generation

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Content, Does.Contain("EmptyModule"));

        // Should use simple generation, not merge prompt
        _mockLlmFacade.Verify(x => x.ExecuteAsync(
            It.IsAny<string>(),
            It.Is<string>(s => s.Contains("Merge documentation")),
            It.IsAny<List<ChatMessage>>(),
            It.IsAny<MessageCompositionOptions?>(),
            It.IsAny<CancellationToken>()), Times.Never, "Should not use merge prompt when no child pages exist");
    }
}
