using NUnit.Framework;
using codeMRI.Frontend.Services;
using System.Collections.Generic;

namespace codeMRI.Frontend.Tests
{
    [TestFixture]
    public class BenchmarkFormatterTests
    {
        [Test]
        public void GetDisplayName_ReturnsOriginalString_WhenNotJson()
        {
            // Arrange
            string input = "asdfas";

            // Act
            string result = BenchmarkFormatter.GetDisplayName(input);

            // Assert
            Assert.That(result, Is.EqualTo("asdfas"));
        }

        [Test]
        public void GetDisplayName_ReturnsDocumentationModel_WhenJsonContainsIt()
        {
            // Arrange
            string input = "{\"DocumentationModel\": \"mistral-large\", \"Other\": \"value\"}";

            // Act
            string result = BenchmarkFormatter.GetDisplayName(input);

            // Assert
            Assert.That(result, Is.EqualTo("mistral-large"));
        }

        [Test]
        public void GetDisplayName_ReturnsFallback_WhenJsonDoesNotContainDocumentationModel()
        {
            // Arrange
            string input = "{\"Other\": \"value\"}";

            // Act
            string result = BenchmarkFormatter.GetDisplayName(input);

            // Assert
            Assert.That(result, Is.EqualTo("Custom Configuration"));
        }

        [Test]
        public void ParseConfiguration_ReturnsEmpty_WhenNotJson()
        {
            // Arrange
            string input = "asdfas";

            // Act
            var result = BenchmarkFormatter.ParseConfiguration(input);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ParseConfiguration_ReturnsDictionary_WhenJson()
        {
            // Arrange
            string input = "{\"DocumentationModel\": \"mistral\", \"Temperature\": 0.7}";

            // Act
            var result = BenchmarkFormatter.ParseConfiguration(input);

            // Assert
            Assert.That(result["DocumentationModel"].ToString(), Is.EqualTo("mistral"));
            Assert.That(result["Temperature"].ToString(), Is.EqualTo("0.7"));
        }
    }
}
