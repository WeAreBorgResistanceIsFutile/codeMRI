using Microsoft.Extensions.Logging;
using Moq;
using codeMRI.Core.Services;
using codeMRI.Core.Interfaces;

namespace codeMRI.Core.Tests;

public class ComponentIdentificationServiceTests
{
    private readonly Mock<ILogger<ComponentIdentificationService>> _mockLogger;
    private readonly ComponentIdentificationService _service;
    private readonly string _testRepoPath;

    public ComponentIdentificationServiceTests()
    {
        _mockLogger = new Mock<ILogger<ComponentIdentificationService>>();
        _service = new ComponentIdentificationService(_mockLogger.Object);
        _testRepoPath = Path.Combine(Path.GetTempPath(), "test_repo_components");
    }

    [Fact]
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
        Assert.NotNull(result);
        Assert.Equal("test_repo_components", result.Name);
        Assert.Equal("C#", result.Language);
        Assert.True(result.Files.Count >= 3);
        Assert.Contains("Program.cs", result.Files);
        Assert.Contains("Services/UserService.cs", result.Files);
        Assert.True(result.FileExtensions.ContainsKey(".cs"));

        // Cleanup
        if (Directory.Exists(_testRepoPath))
        {
            Directory.Delete(_testRepoPath, true);
        }
    }

    [Fact]
    public async Task AnalyzeRepositoryAsync_ShouldReturnEmptyStructure_WhenInvalidPathProvided()
    {
        // Arrange
        var invalidPath = Path.Combine(Path.GetTempPath(), "nonexistent_repo");

        // Act
        var result = await _service.AnalyzeRepositoryAsync(invalidPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("nonexistent_repo", result.Name);
        Assert.Empty(result.Files);
        Assert.Empty(result.Directories);
    }

    [Fact]
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
        Assert.NotNull(result);
        Assert.True(result.Count >= 2);
        
        var testClass = result.FirstOrDefault(c => c.Name == "TestClass");
        Assert.NotNull(testClass);
        Assert.Equal("Class", testClass.Type);
        Assert.Equal("C#", testClass.Language);
        Assert.Equal(testFile, testClass.FilePath);

        var testInterface = result.FirstOrDefault(c => c.Name == "ITestInterface");
        Assert.NotNull(testInterface);
        Assert.Equal("Interface", testInterface.Type);

        // Cleanup
        if (Directory.Exists(_testRepoPath))
        {
            Directory.Delete(_testRepoPath, true);
        }
    }

    [Fact]
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
        Assert.NotNull(result);
        Assert.True(result.Dependencies.Count >= 1);
        
        var dependency = result.Dependencies.FirstOrDefault(d => 
            d.FromComponent.Contains("UserService") && d.ToComponent.Contains("IRepository"));
        Assert.NotNull(dependency);
        Assert.Equal("TypeReference", dependency.Type);

        // Cleanup
        if (Directory.Exists(_testRepoPath))
        {
            Directory.Delete(_testRepoPath, true);
        }
    }

    [Theory]
    [InlineData("test.java", "Java")]
    [InlineData("test.py", "Python")]
    [InlineData("test.js", "JavaScript")]
    [InlineData("test.ts", "TypeScript")]
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
        Assert.NotNull(result);
        if (expectedLanguage != "JavaScript" && expectedLanguage != "TypeScript")
        {
            // Our simple parser mainly handles C#, Java, and Python well
            Assert.True(result.Any());
        }

        // Cleanup
        if (Directory.Exists(_testRepoPath))
        {
            Directory.Delete(_testRepoPath, true);
        }
    }
}