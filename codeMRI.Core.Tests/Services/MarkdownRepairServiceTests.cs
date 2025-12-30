using codeMRI.Core.Interfaces;
using codeMRI.Core.Services;
using NUnit.Framework;

namespace codeMRI.Core.Tests.Services;

/// <summary>
///     Tests for MarkdownRepairService following TDD principles
/// </summary>
[TestFixture]
public class MarkdownRepairServiceTests
{
    private IMarkdownRepairService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new MarkdownRepairService();
    }

    #region ExtractMarkdown Tests

    [Test]
    public void ExtractMarkdown_WithMarkdownBlock_ExtractsContent()
    {
        // Arrange
        var input = """
                    Sure! Here it is:
                    ```markdown
                    # Title
                    Content
                    ```
                    """;

        // Act
        var result = _service.ExtractMarkdown(input);

        // Assert
        Assert.That(result, Is.EqualTo("# Title\nContent"));
    }

    [Test]
    public void ExtractMarkdown_WithPreamble_StripsPreamble()
    {
        // Arrange
        var input = """
                    Here is the documentation:
                    
                    # Title
                    Content
                    """;

        // Act
        var result = _service.ExtractMarkdown(input);

        // Assert
        Assert.That(result, Is.EqualTo("# Title\nContent"));
    }

    #endregion

    #region RepairMarkdown Tests

    [Test]
    public void RepairMarkdown_WithWikiLinks_ConvertsToMarkdownLinks()
    {
        // Arrange
        var input = "Check [[Page Name]] or [[Target Page|Display]].";

        // Act
        var result = _service.RepairMarkdown(input);

        // Assert
        Assert.That(result, Is.EqualTo("Check [Page Name](#page-name) or [Display](#target-page)."));
    }

    [Test]
    public void RepairMarkdown_WithBrokenMermaid_SanitizesNestedBrackets()
    {
        // Arrange
        var input = """
                    # Module
                    ```mermaid
                    graph TD
                        A -->|returns| TokenList[Token[]]
                    ```
                    """;

        // Act
        var result = _service.RepairMarkdown(input);

        // Assert
        Assert.That(result, Does.Contain(@"TokenList[""Token[]""]"));
    }

    [Test]
    public void RepairMarkdown_WithArrowLabels_QuotesThem()
    {
        // Arrange
        var input = """
                    ```mermaid
                    graph TD
                        A -->|Defines Math Op: PLUS| B
                    ```
                    """;

        // Act
        var result = _service.RepairMarkdown(input);

        // Assert
        Assert.That(result, Does.Contain(@"|""Defines Math Op: PLUS""|"));
    }

    #endregion
}
