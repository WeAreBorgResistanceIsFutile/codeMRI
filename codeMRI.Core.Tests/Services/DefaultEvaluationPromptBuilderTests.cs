using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class DefaultEvaluationPromptBuilderTests
{
    [SetUp]
    public void Setup()
    {
        _builder = new DefaultEvaluationPromptBuilder();
    }

    private DefaultEvaluationPromptBuilder _builder;

    [Test]
    public void BuildPrompt_ShouldIncludeRequirementTitle()
    {
        // Arrange
        var requirement = new RubricRequirement
        {
            Title = "API Documentation",
            Description = "Must document all public APIs"
        };
        var structure = new WikiStructure();

        // Act
        var prompt = _builder.BuildPrompt(requirement, structure);

        // Assert
        Assert.That(prompt, Does.Contain("API Documentation"));
    }

    [Test]
    public void BuildPrompt_ShouldIncludeRequirementDescription()
    {
        // Arrange
        var requirement = new RubricRequirement
        {
            Title = "Test Requirement",
            Description = "Custom description text"
        };
        var structure = new WikiStructure();

        // Act
        var prompt = _builder.BuildPrompt(requirement, structure);

        // Assert
        Assert.That(prompt, Does.Contain("Custom description text"));
    }

    [Test]
    public void BuildPrompt_ShouldIncludeJsonFormatInstructions()
    {
        // Arrange
        var requirement = new RubricRequirement { Title = "Req1", Description = "Desc1" };
        var structure = new WikiStructure();

        // Act
        var prompt = _builder.BuildPrompt(requirement, structure);

        // Assert
        Assert.That(prompt, Does.Contain("valid JSON"));
        Assert.That(prompt, Does.Contain("requirement_id"));
        Assert.That(prompt, Does.Contain("score"));
        Assert.That(prompt, Does.Contain("reasoning"));
        Assert.That(prompt, Does.Contain("evidence"));
    }

    [Test]
    public void BuildPrompt_ShouldIncludeScoringCriteria()
    {
        // Arrange
        var requirement = new RubricRequirement { Title = "Req1", Description = "Desc1" };
        var structure = new WikiStructure();

        // Act
        var prompt = _builder.BuildPrompt(requirement, structure);

        // Assert
        Assert.That(prompt, Does.Contain("Scoring Criteria"));
        Assert.That(prompt, Does.Contain("Score 1 if"));
        Assert.That(prompt, Does.Contain("Score 0 if"));
    }

    [Test]
    public void BuildPrompt_ShouldIncludeDocumentationStructure()
    {
        // Arrange
        var requirement = new RubricRequirement { Title = "Req1", Description = "Desc1" };
        var structure = new WikiStructure
        {
            Title = "Test Wiki",
            Pages = new List<WikiPage>()
        };

        // Act
        var prompt = _builder.BuildPrompt(requirement, structure);

        // Assert
        Assert.That(prompt, Does.Contain("Documentation Structure"));
        Assert.That(prompt, Does.Contain("Test Wiki"));
    }
}