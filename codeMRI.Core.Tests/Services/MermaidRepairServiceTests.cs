using codeMRI.Core.Interfaces;
using codeMRI.Core.Services;
using NUnit.Framework;

namespace codeMRI.Core.Tests.Services;

/// <summary>
///     Tests for MermaidRepairService following TDD principles
///     Starting generic, progressively getting more specific
/// </summary>
[TestFixture]
public class MermaidRepairServiceTests
{
    private IMermaidRepairService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new MermaidRepairService();
    }

    #region Generic Tests - ExtractMermaid

    [Test]
    public void ExtractMermaid_WithValidMermaid_ReturnsMermaid()
    {
        // Arrange - most generic case: valid Mermaid in code block
        var input = """
                    ```mermaid
                    graph TD
                        A --> B
                    ```
                    """;

        // Act
        var result = _service.ExtractMermaid(input);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo("graph TD\n    A --> B"));
    }

    [Test]
    public void ExtractMermaid_WithChattyResponse_ExtractsMermaid()
    {
        // Arrange - more specific: Mermaid with text before and after
        var input = """
                    Sure! Here's your diagram:

                    ```mermaid
                    graph TD
                        A --> B
                    ```

                    Hope this helps!
                    """;

        // Act
        var result = _service.ExtractMermaid(input);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo("graph TD\n    A --> B"));
    }

    [Test]
    public void ExtractMermaid_NoMermaidBlock_ReturnsNull()
    {
        // Arrange
        var input = "This is just text without any mermaid block.";

        // Act
        var result = _service.ExtractMermaid(input);

        // Assert
        Assert.That(result, Is.Null);
    }

    #endregion

    #region Mermaid Repair Tests

    [Test]
    public void RepairMermaid_WithSpecialCharactersInLabels_QuotesLabels()
    {
        // Arrange
        var input = """
                    graph TD
                        A[Input String] --> B[Tokenizer]
                        E1[Value: e.g., "10,5", "+"]
                        E3[Position: start="0" end="3"]
                    """;

        // Act
        var result = _service.RepairMermaid(input);

        // Assert
        Assert.That(result, Does.Contain("E1[\"Value: e.g., \\\"10,5\\\", \\\"+\\\"\"]"));
        Assert.That(result, Does.Contain("E3[\"Position: start=\\\"0\\\" end=\\\"3\\\"\"]"));
    }

    [Test]
    public void RepairMermaid_WithArrowLabels_QuotesLabels()
    {
        // Arrange
        var input = """
                    graph TD
                        A -->|Defines Math Op: PLUS| B
                        C ---|Uses Character Classification: isSymbol()| D
                    """;

        // Act
        var result = _service.RepairMermaid(input);

        // Assert
        Assert.That(result, Does.Contain(@"|""Defines Math Op: PLUS""|"));
        Assert.That(result, Does.Contain(@"|""Uses Character Classification: isSymbol()""|"));
    }

    [Test]
    public void RepairMermaid_WithNestedBrackets_QuotesLabels()
    {
        // Arrange
        var input = """
                    graph TD
                        A -->|returns| TokenList[Token[]]
                        B -->|returns| List[string]
                    """;

        // Act
        var result = _service.RepairMermaid(input);

        // Assert
        Assert.That(result, Does.Contain(@"TokenList[""Token[]""]"));
        Assert.That(result, Does.Contain(@"List[""string""]"));
    }

    #endregion
}
