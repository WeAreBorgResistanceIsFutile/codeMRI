using System.Text.Json;
using codeMRI.Core.Converters;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class RubricGenerationServiceTests
{
    private Mock<ILogger<RubricGenerationService>> _loggerMock;
    private Mock<ILLMClient> _llmClientMock;
    private RubricGenerationService _service;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<RubricGenerationService>>();
        _llmClientMock = new Mock<ILLMClient>();
        _llmClientMock.Setup(x => x.ContextSize).Returns(4096);
        _service = new RubricGenerationService(_loggerMock.Object, _llmClientMock.Object);
    }

    [Test]
    public async Task GenerateRubricAsync_ShouldParseJsonCorrectly()
    {
        // Arrange
        var json = @"
{
    ""title"": ""tunesynctool Documentation Evaluation"",
    ""weight"": 1.0,
    ""children"": [
        {
            ""title"": ""Core Architectural Components"",
            ""weight"": 0.25,
            ""children"": [
                {
                    ""title"": ""High-Level System Architecture"",
                    ""weight"": 0.2,
                    ""is_leaf"": true,
                    ""description"": ""Diagram and description of the overall system architecture.""
                }
            ]
        }
    ]
}";
        
        _llmClientMock.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        var structure = new WikiStructure();
        var repoInfo = new RepositoryInfo();

        // Act
        var rubric = await _service.GenerateRubricAsync(structure, repoInfo);

        // Assert
        Assert.That(rubric, Is.Not.Null);
        Assert.That(rubric.Title, Is.EqualTo("tunesynctool Documentation Evaluation"));
        Assert.That(rubric.Children, Is.Not.Null);
        Assert.That(rubric.Children.Count, Is.EqualTo(1));
        
        var category = rubric.Children[0];
        Assert.That(category, Is.InstanceOf<RubricCategory>());
        Assert.That(category.Title, Is.EqualTo("Core Architectural Components"));
        Assert.That(category.IsLeaf, Is.False);
        
        var requirement = category.Children[0];
        Assert.That(requirement, Is.InstanceOf<RubricRequirement>());
        Assert.That(requirement.Title, Is.EqualTo("High-Level System Architecture"));
        Assert.That(requirement.IsLeaf, Is.True);
        
        var reqCast = (RubricRequirement)requirement;
        Assert.That(reqCast.Description, Is.EqualTo("Diagram and description of the overall system architecture."));
    }

    [Test]
    public async Task GenerateRubricAsync_ShouldParseLargerJsonCorrectly()
    {
        // Arrange
        var largeJson = @"
{
    ""title"": ""tunesynctool Documentation Evaluation"",
    ""weight"": 1.0,
    ""children"": [
        {
            ""title"": ""Core Architectural Components"",
            ""weight"": 0.25,
            ""children"": [
                {
                    ""title"": ""High-Level Architecture Overview"",
                    ""weight"": 0.2,
                    ""is_leaf"": true,
                    ""description"": ""Documentation should include a high-level diagram and description of the system architecture, including all major components and their interactions.""
                },
                {
                    ""title"": ""Component Breakdown"",
                    ""weight"": 0.2,
                    ""is_leaf"": true,
                    ""description"": ""Each core component should be documented with its purpose, responsibilities, and relationships to other components.""
                },
                {
                    ""title"": ""Data Flow Diagrams"",
                    ""weight"": 0.15,
                    ""is_leaf"": true,
                    ""description"": ""Include diagrams illustrating how data flows through the system, from input to output.""
                },
                {
                    ""title"": ""Dependency Graph"",
                    ""weight"": 0.1,
                    ""is_leaf"": true,
                    ""description"": ""Document external and internal dependencies, including versions and rationale for their use.""
                },
                {
                    ""title"": ""Modularity and Extensibility"",
                    ""weight"": 0.15,
                    ""is_leaf"": true,
                    ""description"": ""Explain how the system is modularized and how new components can be added or existing ones extended.""
                },
                {
                    ""title"": ""Configuration Management"",
                    ""weight"": 0.1,
                    ""is_leaf"": true,
                    ""description"": ""Describe how the system is configured, including environment variables, config files, and default settings.""
                },
                {
                    ""title"": ""Error Handling Architecture"",
                    ""weight"": 0.1,
                    ""is_leaf"": true,
                    ""description"": ""Document the error handling strategy, including logging, error propagation, and recovery mechanisms.""
                }
            ]
        },
        {
            ""title"": ""Key Features and Capabilities"",
            ""weight"": 0.25,
            ""children"": [
                {
                    ""title"": ""Feature List"",
                    ""weight"": 0.2,
                    ""is_leaf"": true,
                    ""description"": ""Provide a comprehensive list of all features, categorized by functionality (e.g., sync, backup, restore).""
                },
                {
                    ""title"": ""Feature Descriptions"",
                    ""weight"": 0.25,
                    ""is_leaf"": true,
                    ""description"": ""Each feature should have a detailed description, including its purpose, use cases, and benefits.""
                },
                {
                    ""title"": ""Feature Limitations"",
                    ""weight"": 0.15,
                    ""is_leaf"": true,
                    ""description"": ""Document known limitations or constraints for each feature, including edge cases and unsupported scenarios.""
                },
                {
                    ""title"": ""Performance Characteristics"",
                    ""weight"": 0.15,
                    ""is_leaf"": true,
                    ""description"": ""Include performance benchmarks or expectations for key features, such as sync speed, resource usage, and scalability.""
                },
                {
                    ""title"": ""Security Features"",
                    ""weight"": 0.1,
                    ""is_leaf"": true,
                    ""description"": ""Document security-related features, such as encryption, authentication, and authorization mechanisms.""
                },
                {
                    ""title"": ""Compatibility Matrix"",
                    ""weight"": 0.1,
                    ""is_leaf"": true,
                    ""description"": ""Provide a matrix showing compatibility with different operating systems, file systems, and third-party tools.""
                },
                {
                    ""title"": ""Feature Roadmap"",
                    ""weight"": 0.05,
                    ""is_leaf"": true,
                    ""description"": ""Include a roadmap or future plans for feature development, if available.""
                }
            ]
        },
        {
            ""title"": ""API/Interface Specifications"",
            ""weight"": 0.25,
            ""children"": [
                {
                    ""title"": ""API Overview"",
                    ""weight"": 0.15,
                    ""is_leaf"": true,
                    ""description"": ""Provide an overview of the API, including its purpose, design principles, and intended audience.""
                },
                {
                    ""title"": ""Endpoint Documentation"",
                    ""weight"": 0.25,
                    ""is_leaf"": true,
                    ""description"": ""Document all API endpoints, including HTTP methods, paths, request/response formats, and examples.""
                },
                {
                    ""title"": ""Authentication and Authorization"",
                    ""weight"": 0.15,
                    ""is_leaf"": true,
                    ""description"": ""Document how to authenticate and authorize API requests, including token management and permission scopes.""
                },
                {
                    ""title"": ""Error Codes and Messages"",
                    ""weight"": 0.15,
                    ""is_leaf"": true,
                    ""description"": ""List all possible error codes and messages, along with their meanings and suggested resolutions.""
                },
                {
                    ""title"": ""Rate Limiting and Throttling"",
                    ""weight"": 0.1,
                    ""is_leaf"": true,
                    ""description"": ""Document any rate limits or throttling mechanisms, including how to check usage and handle limits.""
                },
                {
                    ""title"": ""SDKs and Libraries"",
                    ""weight"": 0.1,
                    ""is_leaf"": true,
                    ""description"": ""Document any official or community-supported SDKs or libraries for interacting with the API.""
                },
                {
                    ""title"": ""Webhooks and Callbacks"",
                    ""weight"": 0.1,
                    ""is_leaf"": true,
                    ""description"": ""Document webhook or callback mechanisms, including how to register, verify, and handle events.""
                }
            ]
        },
        {
            ""title"": ""Usage Patterns and Examples"",
            ""weight"": 0.25,
            ""children"": [
                {
                    ""title"": ""Getting Started Guide"",
                    ""weight"": 0.2,
                    ""is_leaf"": true,
                    ""description"": ""Provide a step-by-step guide for installing, configuring, and running the tool for the first time.""
                },
                {
                    ""title"": ""Basic Usage Examples"",
                    ""weight"": 0.2,
                    ""is_leaf"": true,
                    ""description"": ""Include examples of basic usage scenarios, such as syncing a directory or backing up files.""
                },
                {
                    ""title"": ""Advanced Usage Examples"",
                    ""weight"": 0.2,
                    ""is_leaf"": true,
                    ""description"": ""Provide examples of advanced usage, such as custom configurations, automation, or integration with other tools.""
                },
                {
                    ""title"": ""CLI Documentation"",
                    ""weight"": 0.15,
                    ""is_leaf"": true,
                    ""description"": ""Document all command-line interface (CLI) commands, options, and flags, including examples.""
                },
                {
                    ""title"": ""Configuration Examples"",
                    ""weight"": 0.1,
                    ""is_leaf"": true,
                    ""description"": ""Include examples of different configuration setups, such as for different environments or use cases.""
                },
                {
                    ""title"": ""Troubleshooting Guide"",
                    ""weight"": 0.1,
                    ""is_leaf"": true,
                    ""description"": ""Provide a guide for troubleshooting common issues, including error messages and their resolutions.""
                },
                {
                    ""title"": ""FAQ"",
                    ""weight"": 0.05,
                    ""is_leaf"": true,
                    ""description"": ""Include a frequently asked questions (FAQ) section addressing common queries and concerns.""
                }
            ]
        },
        {
            ""title"": ""Documentation Quality and Maintenance"",
            ""weight"": 0.1,
            ""children"": [
                {
                    ""title"": ""Clarity and Readability"",
                    ""weight"": 0.25,
                    ""is_leaf"": true,
                    ""description"": ""Documentation should be clear, concise, and free of jargon. Use plain language and avoid ambiguity.""
                },
                {
                    ""title"": ""Consistency"",
                    ""weight"": 0.2,
                    ""is_leaf"": true,
                    ""description"": ""Ensure consistency in terminology, formatting, and style across all documentation sections.""
                },
                {
                    ""title"": ""Completeness"",
                    ""weight"": 0.2,
                    ""is_leaf"": true,
                    ""description"": ""All aspects of the system should be documented, with no missing sections or incomplete descriptions.""
                },
                {
                    ""title"": ""Accuracy"",
                    ""weight"": 0.2,
                    ""is_leaf"": true,
                    ""description"": ""Documentation should accurately reflect the current state of the codebase, with no outdated or incorrect information.""
                },
                {
                    ""title"": ""Versioning and Changelog"",
                    ""weight"": 0.15,
                    ""is_leaf"": true,
                    ""description"": ""Include versioning information and a changelog to track updates, new features, and breaking changes.""
                }
            ]
        }
    ]
}";
        
        _llmClientMock.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(largeJson);

        var structure = new WikiStructure();
        var repoInfo = new RepositoryInfo();

        // Act
        var rubric = await _service.GenerateRubricAsync(structure, repoInfo);

        // Assert
        Assert.That(rubric, Is.Not.Null);
        Assert.That(rubric.Title, Is.EqualTo("tunesynctool Documentation Evaluation"));
        Assert.That(rubric.Children, Is.Not.Null);
        Assert.That(rubric.Children.Count, Is.EqualTo(5)); // There are 5 top-level children

        // Check a deeply nested leaf node
        var qualityCategory = rubric.Children[4]; // "Documentation Quality and Maintenance"
        Assert.That(qualityCategory, Is.InstanceOf<RubricCategory>());
        Assert.That(qualityCategory.Title, Is.EqualTo("Documentation Quality and Maintenance"));

        var clarityRequirement = qualityCategory.Children[0]; // "Clarity and Readability"
        Assert.That(clarityRequirement, Is.InstanceOf<RubricRequirement>());
        Assert.That(clarityRequirement.Title, Is.EqualTo("Clarity and Readability"));
        Assert.That(clarityRequirement.IsLeaf, Is.True);
        var clarityReqCast = (RubricRequirement)clarityRequirement;
        Assert.That(clarityReqCast.Description, Does.Contain("clear, concise, and free of jargon"));
    }

    [Test]
    public async Task GenerateRubricAsync_ShouldHandleDirtyJson()
    {
        // Arrange
        var dirtyJson = @"
Here is the rubric you requested:
```json
{
    ""title"": ""Dirty JSON Test"",
    ""weight"": 1.0,
    ""children"": []
}
```
I hope this helps!
";
        
        _llmClientMock.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dirtyJson);

        var structure = new WikiStructure();
        var repoInfo = new RepositoryInfo();

        // Act
        var rubric = await _service.GenerateRubricAsync(structure, repoInfo);

        // Assert
        Assert.That(rubric, Is.Not.Null);
        Assert.That(rubric.Title, Is.EqualTo("Dirty JSON Test"));
    }
}
