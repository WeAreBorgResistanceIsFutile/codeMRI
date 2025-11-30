using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using NUnit.Framework;

namespace codeMRI.E2E;

[TestFixture]
public class ApiHealthTests
{
    private WebApplicationFactory<Program>? _factory;

    [OneTimeSetUp]
    public void Setup()
    {
        _factory = new WebApplicationFactory<Program>();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _factory?.Dispose();
    }

    [Test]
    public async Task Get_SwaggerUI_ReturnsSuccessAndHtml()
    {
        // Arrange
        var client = _factory!.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/index.html");

        // Assert
        response.EnsureSuccessStatusCode(); // Status Code 200-299
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }
}
