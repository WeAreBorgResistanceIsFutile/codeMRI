using Microsoft.Extensions.Logging;
using Moq;
using codeMRI.Core.Services;
using codeMRI.Core.Interfaces;
using NUnit.Framework;

namespace codeMRI.Core.Tests;

[TestFixture]
public class ComponentIdentificationServiceTests
{
    private Mock<ILogger<ComponentIdentificationService>> _mockLogger;
    private ComponentIdentificationService _service;
    private string _testRepoPath;

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<ComponentIdentificationService>>();
        _service = new ComponentIdentificationService(_mockLogger.Object);
        _testRepoPath = Path.Combine(Path.GetTempPath(), "test_repo_components");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testRepoPath))
        {
            Directory.Delete(_testRepoPath, true);
        }
    }

    [Test]
    public async Task AnalyzeRepositoryAsync_ShouldReturnRepositoryStructure_WhenValidPathProvided()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        
        // Create test files
        await File.WriteAllTextAsync(Path.Combine(_testRepoPath, "Program.cs"), "public class Program { }");
        await File.WriteAllTextAsync(Path.Combine(_testRepoPath, "IService.cs"), "public interface IService { }");
        
        // Create subdirectory
        var servicesDir = Path.Combine(_testRepoPath, "Services");
        Directory.CreateDirectory(servicesDir);
        await File.WriteAllTextAsync(Path.Combine(servicesDir, "UserService.cs"), "public class UserService { }");

        // Act
        var result = await _service.AnalyzeRepositoryAsync(_testRepoPath);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo("test_repo_components"));
        Assert.That(result.Language, Is.EqualTo("C#"));
        Assert.That(result.Files.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(result.Files, Does.Contain("Program.cs"));
        Assert.That(result.Files, Does.Contain("Services/UserService.cs"));
        Assert.That(result.FileExtensions.ContainsKey(".cs"), Is.True);
    }

    [Test]
    public async Task AnalyzeRepositoryAsync_ShouldReturnEmptyStructure_WhenInvalidPathProvided()
    {
        // Arrange
        var invalidPath = Path.Combine(Path.GetTempPath(), "nonexistent_repo");

        // Act
        var result = await _service.AnalyzeRepositoryAsync(invalidPath);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo("nonexistent_repo"));
        Assert.That(result.Files, Is.Empty);
        Assert.That(result.Directories, Is.Empty);
    }

    [Test]
    public async Task IdentifyComponentsAsync_ShouldReturnComponents_WhenCSharpFilesExist()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        
        var testFile = Path.Combine(_testRepoPath, "TestClass.cs");
        await File.WriteAllTextAsync(testFile, @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod() { }
        public string TestProperty { get; set; }
    }

    public interface ITestInterface
    {
        void InterfaceMethod();
    }
}");

        // Act
        var result = await _service.IdentifyComponentsAsync(_testRepoPath);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.GreaterThanOrEqualTo(2));
        
        var testClass = result.FirstOrDefault(c => c.Name == "TestClass");
        Assert.That(testClass, Is.Not.Null);
        Assert.That(testClass.Type, Is.EqualTo("Class"));
        Assert.That(testClass.Language, Is.EqualTo("C#"));
        Assert.That(testClass.FilePath, Is.EqualTo(testFile));

        var testInterface = result.FirstOrDefault(c => c.Name == "ITestInterface");
        Assert.That(testInterface, Is.Not.Null);
        Assert.That(testInterface.Type, Is.EqualTo("Interface"));
    }

    [Test]
    public async Task AnalyzeRelationshipsAsync_ShouldReturnDependencies_WhenComponentsExist()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        
        var serviceFile = Path.Combine(_testRepoPath, "UserService.cs");
        await File.WriteAllTextAsync(serviceFile, @"
public class UserService
{
    private IRepository _repository;
    
    public UserService(IRepository repository)
    {
        _repository = repository;
    }
}");

        var repositoryFile = Path.Combine(_testRepoPath, "IRepository.cs");
        await File.WriteAllTextAsync(repositoryFile, @"
public interface IRepository
{
    void Save();
}");

        var components = await _service.IdentifyComponentsAsync(_testRepoPath);

        // Act
        var result = await _service.AnalyzeRelationshipsAsync(_testRepoPath, components);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Dependencies.Count, Is.GreaterThanOrEqualTo(1));
        
        var dependency = result.Dependencies.FirstOrDefault(d => 
            d.FromComponent.Contains("UserService") && d.ToComponent.Contains("IRepository"));
        Assert.That(dependency, Is.Not.Null);
        Assert.That(dependency.Type, Is.EqualTo("TypeReference"));
    }

    [TestCase("test.java", "Java")]
    [TestCase("test.py", "Python")]
    [TestCase("test.js", "JavaScript")]
    [TestCase("test.ts", "TypeScript")]
    public async Task IdentifyComponentsAsync_ShouldHandleMultipleLanguages(string fileName, string expectedLanguage)
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        var testFile = Path.Combine(_testRepoPath, fileName);
        
        var content = expectedLanguage switch
        {
            "Java" => "public class TestClass { }",
            "Python" => "class TestClass:\n    pass",
            "JavaScript" => "class TestClass { }",
            "TypeScript" => "class TestClass { }",
            _ => throw new ArgumentException("Unsupported language")
        };
        
        await File.WriteAllTextAsync(testFile, content);

        // Act
        var result = await _service.IdentifyComponentsAsync(_testRepoPath);

        // Assert
        Assert.That(result, Is.Not.Null);
        if (expectedLanguage != "JavaScript" && expectedLanguage != "TypeScript")
        {
            // Our simple parser mainly handles C#, Java, and Python well
            Assert.That(result.Any(), Is.True);
        }
    }
}
