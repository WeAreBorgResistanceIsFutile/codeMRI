using System.Text.Json;
using System.Text.Json.Serialization;
using codeMRI.Core.Interfaces;
using NUnit.Framework;

namespace codeMRI.Core.Tests.Converters;

[TestFixture]
public class RubricSerializationTests
{
    private JsonSerializerOptions _options;

    [SetUp]
    public void Setup()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };
    }

    [Test]
    public void Serialize_Polymorphic_ShouldIncludeDiscriminators()
    {
        // Arrange
        var rubric = new EvaluationRubric
        {
            Title = "Root",
            Children = new List<RubricNode>
            {
                new RubricCategory { Title = "Cat 1" },
                new RubricRequirement { Title = "Req 1", Description = "Desc" }
            }
        };

        // Act
        var json = JsonSerializer.Serialize<RubricNode>(rubric, _options);
        System.Console.WriteLine(json);

        // Assert
        Assert.That(json, Contains.Substring("\"$type\":\"rubric\""));
        Assert.That(json, Contains.Substring("\"$type\":\"category\""));
        Assert.That(json, Contains.Substring("\"$type\":\"requirement\""));
    }

    [Test]
    public void Deserialize_Polymorphic_ShouldWorkWithDiscriminators()
    {
        // Arrange
        var json = "{\"$type\":\"rubric\",\"title\":\"Root\",\"children\":[{\"$type\":\"requirement\",\"title\":\"Req 1\",\"description\":\"Desc\"}]}";

        // Act
        var result = JsonSerializer.Deserialize<RubricNode>(json, _options);

        // Assert
        Assert.That(result, Is.InstanceOf<EvaluationRubric>());
        Assert.That(result.Children![0], Is.InstanceOf<RubricRequirement>());
        Assert.That(((RubricRequirement)result.Children[0]).Description, Is.EqualTo("Desc"));
    }

    [Test]
    public void Deserialize_ShouldHandleSnakeCase_IsLeaf()
    {
        // Arrange - using the snake_case alias we added
        var json = "{\"$type\":\"requirement\",\"title\":\"Req 1\",\"is_leaf\":true}";

        // Act
        var result = JsonSerializer.Deserialize<RubricNode>(json, _options);

        // Assert
        Assert.That(result!.IsLeaf, Is.True);
    }
}
