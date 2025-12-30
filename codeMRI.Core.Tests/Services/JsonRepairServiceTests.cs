using codeMRI.Core.Interfaces;
using codeMRI.Core.Services;

namespace codeMRI.Core.Tests.Services;

/// <summary>
///     Tests for JsonRepairService following TDD principles
///     Starting generic, progressively getting more specific
/// </summary>
[TestFixture]
public class JsonRepairServiceTests
{
    private IJsonRepairService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new JsonRepairService();
    }

    #region Generic Tests - ExtractJsonString

    [Test]
    public void ExtractJsonString_WithValidJson_ReturnsJson()
    {
        // Arrange - most generic case: valid JSON
        var input = @"{""name"": ""test""}";

        // Act
        var result = _service.ExtractJsonString(input);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo(@"{""name"": ""test""}"));
    }

    [Test]
    public void ExtractJsonString_WithMarkdownJsonBlock_ExtractsJson()
    {
        // Arrange - more specific: JSON in markdown code block
        var input = @"```json
{""name"": ""test""}
```";

        // Act
        var result = _service.ExtractJsonString(input);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo(@"{""name"": ""test""}"));
    }

    [Test]
    public void ExtractJsonString_WithChattyResponse_ExtractsJson()
    {
        // Arrange - more specific: JSON with text before and after
        var input = @"Sure! Here's your JSON:

{""score"": 0.95, ""data"": ""value""}

Hope this helps!";

        // Act
        var result = _service.ExtractJsonString(input);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain(@"{""score"": 0.95"));
    }

    [Test]
    public void ExtractJsonString_WithNestedBraces_ExtractsCompleteJson()
    {
        // Arrange - more specific: nested JSON objects
        var input = @"{""outer"": {""inner"": {""deep"": ""value""}}}";

        // Act
        var result = _service.ExtractJsonString(input);

        //Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo(@"{""outer"": {""inner"": {""deep"": ""value""}}}"));
    }

    [Test]
    public void ExtractJsonString_WithStringContainingBraces_HandlesCorrectly()
    {
        // Arrange - more specific: JSON with braces in string values
        var input = @"{""message"": ""Use {braces} carefully""}";

        // Act
        var result = _service.ExtractJsonString(input);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo(@"{""message"": ""Use {braces} carefully""}"));
    }

    #endregion

    #region JSON Repair Tests

    [Test]
    public void RepairJson_WithTrailingCommaInObject_RemovesComma()
    {
        // Arrange
        var input = @"{""name"": ""test"", ""value"": 123,}";

        // Act
        var result = _service.RepairJson(input);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Not.Contain(",}"));
    }

    [Test]
    public void RepairJson_WithTrailingCommaInArray_RemovesComma()
    {
        // Arrange
        var input = @"{""items"": [1, 2, 3,]}";

        // Act
        var result = _service.RepairJson(input);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Not.Contain(",]"));
    }

    #endregion

    #region ExtractAndDeserialize Tests

    private class TestModel
    {
        public string? Name { get; set; }
        public double Score { get; set; }
    }

    [Test]
    public void ExtractAndDeserialize_WithValidJson_DeserializesCorrectly()
    {
        // Arrange
        var input = @"{""name"": ""test"", ""score"": 0.95}";

        // Act
        var result = _service.ExtractAndDeserialize<TestModel>(input);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo("test"));
        Assert.That(result.Score, Is.EqualTo(0.95));
    }

    [Test]
    public void ExtractAndDeserialize_WithMarkdownWrappedJson_DeserializesCorrectly()
    {
        // Arrange
        var input = @"```json
{""name"": ""test"", ""score"": 0.85}
```";

        // Act
        var result = _service.ExtractAndDeserialize<TestModel>(input);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo("test"));
        Assert.That(result.Score, Is.EqualTo(0.85));
    }

    [Test]
    public void ExtractAndDeserialize_WithInvalidJson_ReturnsNull()
    {
        // Arrange
        var input = @"{invalid json}";

        // Act
        var result = _service.ExtractAndDeserialize<TestModel>(input);

        // Assert
        Assert.That(result, Is.Null);
    }

    #endregion
}
