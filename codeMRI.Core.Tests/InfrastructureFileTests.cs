using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace codeMRI.Core.Tests;

[TestFixture]
public class InfrastructureFileTests
{
    private Mock<ILogger<ComponentIdentificationService>> _mockLogger;
    private ComponentIdentificationService _service;
    private string _testRepoPath;

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<ComponentIdentificationService>>();
        var mockProgress = new Mock<IProgressService>();
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var mockAst = new Mock<IASTServiceClient>();
        
        _service = new ComponentIdentificationService(_mockLogger.Object, mockProgress.Object, mockLoggerFactory.Object, mockAst.Object);
        _testRepoPath = Path.Combine(Path.GetTempPath(), "test_repo_infra_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRepoPath);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testRepoPath)) Directory.Delete(_testRepoPath, true);
    }

    [Test]
    public async Task IdentifyComponentsAsync_ShouldProcessInfrastructureFilesAsConfigurationComponents()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testRepoPath, ".gitignore"), "node_modules/");
        await File.WriteAllTextAsync(Path.Combine(_testRepoPath, "package.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(_testRepoPath, "appsettings.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(_testRepoPath, "Program.cs"), "public class Program { }");

        // Act
        var result = await _service.IdentifyComponentsAsync(_testRepoPath);

        // Assert
        Assert.That(result, Is.Not.Null);
        var infraComponents = result.Where(c => c.Type == "Configuration").ToList();
        Assert.That(infraComponents.Count, Is.EqualTo(3));
        Assert.That(infraComponents.Any(c => c.Name == ".gitignore"), Is.True);
        Assert.That(infraComponents.Any(c => c.Name == "package.json"), Is.True);
        Assert.That(infraComponents.Any(c => c.Name == "appsettings.json"), Is.True);
        
        var codeComponents = result.Where(c => c.Type != "Configuration").ToList();
        Assert.That(codeComponents.Any(c => c.Name == "Program"), Is.True);
    }

    [Test]
    public async Task AnalyzeRelationshipsAsync_ShouldSkipConfigurationFiles()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testRepoPath, "package.json"), "{}");
        var codeFile = Path.Combine(_testRepoPath, "Code.cs");
        await File.WriteAllTextAsync(codeFile, "public class Code { // refers to package.json but should be ignored }");
        
        var components = await _service.IdentifyComponentsAsync(_testRepoPath);

        // Act
        var result = await _service.AnalyzeRelationshipsAsync(_testRepoPath, components);

        // Assert
        Assert.That(result.Dependencies.Any(d => d.FromComponent.Contains("package.json") || d.ToComponent.Contains("package.json")), Is.False);
    }
}
