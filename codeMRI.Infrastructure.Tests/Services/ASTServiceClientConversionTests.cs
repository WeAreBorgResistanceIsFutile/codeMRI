using System.Text.Json;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Infrastructure.Configuration;
using codeMRI.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace codeMRI.Infrastructure.Tests.Services;

[TestFixture]
public class ASTServiceClientConversionTests
{
    private ASTServiceClient _service;
    private Mock<ILogger<ASTServiceClient>> _loggerMock;
    private Mock<IOptions<ASTServiceSettings>> _settingsMock;
    private Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private HttpClient _httpClient;

    [SetUp]
    public void Setup()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:3000")
        };

        _loggerMock = new Mock<ILogger<ASTServiceClient>>();
        _settingsMock = new Mock<IOptions<ASTServiceSettings>>();
        
        _settingsMock.Setup(s => s.Value).Returns(new ASTServiceSettings 
        { 
            BaseUrl = "http://localhost:3000",
            Enabled = true
        });

        var csharpParserMock = new Mock<ICSharpParser>();
        _service = new ASTServiceClient(_httpClient, _loggerMock.Object, _settingsMock.Object, csharpParserMock.Object);
    }
    
    [TearDown]
    public void TearDown()
    {
        _httpClient.Dispose();
    }

    [Test]
    public async Task ConvertToCodeComponentsAsync_ShouldHandleJsonElement_Correctly()
    {
        // Arrange
        var jsonStructure = @"
        {
            ""classes"": [
                {
                    ""name"": ""MyClass"",
                    ""type"": ""class_definition"",
                    ""metrics"": { ""lines"": 100, ""complexity"": 5 },
                    ""methods"": [""Method1"", ""Method2""],
                    ""properties"": [""Prop1""]
                }
            ],
            ""functions"": [
                {
                    ""name"": ""MyFunc"",
                    ""metrics"": { ""lines"": 20, ""complexity"": 2 },
                    ""parent"": null
                }
            ]
        }";

        // Deserialize to object to simulate what happens in ParseCodeAsync (it gets deserialized as JsonElement)
        var structure = JsonSerializer.Deserialize<object>(jsonStructure);
        
        var astResult = new ASTParseResult
        {
            FilePath = "TestFile.cs",
            Language = "CSharp",
            HierarchicalStructure = structure
        };

        // Act
        var components = await _service.ConvertToCodeComponentsAsync(astResult);

        // Assert
        Assert.That(components, Is.Not.Null);
        Assert.That(components.Count, Is.EqualTo(2));

        var classComponent = components.FirstOrDefault(c => c.Name == "MyClass");
        Assert.That(classComponent, Is.Not.Null);
        Assert.That(classComponent.Type, Is.EqualTo("class_definition"));
        Assert.That(classComponent.LineCount, Is.EqualTo(100));
        // Methods might be empty if we don't fix GetJsonList casing, but let's assume we fix it.
        // If the JS service doesn't return methods, this part of the test might be testing the C# mapping logic rather than E2E accuracy for methods.
        // For now, let's keep assertions but expect the C# fix to handle the casing.
        Assert.That(classComponent.Methods.Count, Is.EqualTo(2));
        Assert.That(classComponent.Methods[0], Is.EqualTo("Method1"));

        var funcComponent = components.FirstOrDefault(c => c.Name == "MyFunc");
        Assert.That(funcComponent, Is.Not.Null);
        Assert.That(funcComponent.Type, Is.EqualTo("Function"));
        Assert.That(funcComponent.LineCount, Is.EqualTo(20));
    }

    [Test]
    public async Task ConvertToCodeComponentsAsync_ShouldCreateScriptComponent_WhenNoClassesOrFunctionsFound()
    {
        // Arrange
        var jsonStructure = @"
        {
            ""classes"": [],
            ""functions"": []
        }";

        var structure = JsonSerializer.Deserialize<object>(jsonStructure);
        
        // Explicitly deserialize as JsonElement to match runtime behavior for Metrics
        var metricsJson = @"{ ""linesOfCode"": 50, ""cyclomaticComplexity"": 10 }";
        var metricsElement = JsonSerializer.Deserialize<JsonElement>(metricsJson);

        var astResult = new ASTParseResult
        {
            FilePath = "script.py",
            Language = "Python",
            HierarchicalStructure = structure,
            Metrics = metricsElement
        };

        // Act
        var components = await _service.ConvertToCodeComponentsAsync(astResult);

        // Assert
        Assert.That(components, Is.Not.Null);
        Assert.That(components.Count, Is.EqualTo(1));

        var scriptComponent = components.First();
        Assert.That(scriptComponent.Name, Is.EqualTo("script"));
        Assert.That(scriptComponent.Type, Is.EqualTo("Script"));
        Assert.That(scriptComponent.LineCount, Is.EqualTo(50));
        Assert.That(scriptComponent.ComplexityScore, Is.EqualTo(10));
    }
}
