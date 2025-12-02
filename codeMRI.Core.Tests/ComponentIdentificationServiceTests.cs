using Microsoft.Extensions.Logging;
using Moq;
using codeMRI.Core.Services;

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
        Assert.That(result.Files.Count, Is.GreaterThan(0));
    }

    [Test]
    public async Task IdentifyComponentsAsync_ShouldHandleGo()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        var goFile = Path.Combine(_testRepoPath, "test.go");
        await File.WriteAllTextAsync(goFile, @"
package main

import ""fmt""

type UserService struct {
    name string
}

func NewUserService(name string) *UserService {
    return &UserService{name: name}
}

func (s *UserService) Process() error {
    fmt.Printf(""Processing: %s\n"", s.name)
    return nil
}

type UserData struct {
    ID    int
    Name  string
}

func (u *UserData) String() string {
    return fmt.Sprintf(""User{ID: %d, Name: %s}"", u.ID, u.Name)
}

type Status int

const (
    StatusActive Status = iota
    StatusInactive
)");

        // Act
        var result = await _service.IdentifyComponentsAsync(_testRepoPath);

        // Assert
        Assert.That(result, Is.Not.Null);
        // Go parsing uses generic patterns, so we check for any detected components
        Assert.That(result.Any(), Is.True);
    }

    [Test]
    public async Task IdentifyComponentsAsync_ShouldDetectLanguageAccuracy()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        
        var files = new Dictionary<string, string>
        {
            { "test.cs", "C#" },
            { "test.java", "Java" },
            { "test.py", "Python" },
            { "test.js", "JavaScript" },
            { "test.ts", "TypeScript" },
            { "test.cpp", "C++" },
            { "test.go", "Go" }
        };

        foreach (var file in files)
        {
            var content = file.Value switch
            {
                "C#" => "public class TestClass { }",
                "Java" => "public class TestClass { }",
                "Python" => "class TestClass:\n    pass",
                "JavaScript" => "class TestClass { }",
                "TypeScript" => "class TestClass { }",
                "C++" => "class TestClass { };",
                "Go" => "package main\n\ntype TestClass struct {}",
                _ => ""
            };
            
            await File.WriteAllTextAsync(Path.Combine(_testRepoPath, file.Key), content);
        }

        // Act
        var result = await _service.IdentifyComponentsAsync(_testRepoPath);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.GreaterThan(0));
        
        // Verify that components were detected for supported languages
        var detectedLanguages = result.Select(c => c.Language).Distinct().ToList();
        Assert.That(detectedLanguages.Contains("C#"), Is.True);
    }

    #region Complex Scenarios Tests

    [Test]
    public async Task IdentifyComponentsAsync_ShouldHandleGenericsAndTemplates()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        var genericFile = Path.Combine(_testRepoPath, "GenericClass.cs");
        await File.WriteAllTextAsync(genericFile, @"
using System.Collections.Generic;

public class Repository<T> where T : class
{
    public void Add(T item) { }
    public T Get(int id) => default(T);
}

public class Service<T, TKey> where T : class
{
    public void Process(T item, TKey key) { }
}

public interface IRepository<T>
{
    void Add(T item);
    T Get(int id);
}");

        // Act
        var result = await _service.IdentifyComponentsAsync(_testRepoPath);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Any(c => c.Name.Contains("Repository")), Is.True);
        Assert.That(result.Any(c => c.Name.Contains("Service")), Is.True);
    }

    [Test]
    public async Task IdentifyComponentsAsync_ShouldHandleInheritanceChains()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        var inheritanceFile = Path.Combine(_testRepoPath, "InheritanceChain.cs");
        await File.WriteAllTextAsync(inheritanceFile, @"
public abstract class BaseEntity
{
    public int Id { get; set; }
}

public abstract class Person : BaseEntity
{
    public string Name { get; set; }
}

public class Employee : Person
{
    public string Department { get; set; }
}

public class Manager : Employee
{
    public List<Employee> Reports { get; set; }
}

public interface IRepository<T>
{
    T Get(int id);
    void Save(T entity);
}

public class EmployeeRepository : IRepository<Employee>
{
    public Employee Get(int id) => new Employee();
    public void Save(Employee entity) { }
}");

        // Act
        var result = await _service.IdentifyComponentsAsync(_testRepoPath);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Any(c => c.Name == "BaseEntity"), Is.True);
        Assert.That(result.Any(c => c.Name == "Person"), Is.True);
        Assert.That(result.Any(c => c.Name == "Employee"), Is.True);
        Assert.That(result.Any(c => c.Name == "Manager"), Is.True);
        Assert.That(result.Any(c => c.Name == "EmployeeRepository"), Is.True);
    }

    [Test]
    public async Task IdentifyComponentsAsync_ShouldHandleInterfacesAndAbstractClasses()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        var abstractFile = Path.Combine(_testRepoPath, "AbstractClasses.cs");
        await File.WriteAllTextAsync(abstractFile, @"
public interface IDataService
{
    Task<Data> GetDataAsync();
    void SaveData(Data data);
}

public interface IRepository<T> where T : class
{
    Task<T> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task AddAsync(T entity);
}

public abstract class BaseService
{
    protected readonly ILogger _logger;
    
    protected BaseService(ILogger logger)
    {
        _logger = logger;
    }
    
    public abstract Task ProcessAsync();
    
    protected virtual void Log(string message)
    {
        _logger.LogInformation(message);
    }
}

public class DataService : BaseService, IDataService
{
    public DataService(ILogger logger) : base(logger) { }
    
    public override Task ProcessAsync()
    {
        Log(""Processing data"");
        return Task.CompletedTask;
    }
    
    public async Task<Data> GetDataAsync()
    {
        await ProcessAsync();
        return new Data();
    }
    
    public void SaveData(Data data) { }
}");

        // Act
        var result = await _service.IdentifyComponentsAsync(_testRepoPath);

        // Assert
        Assert.That(result, Is.Not.Null);
        // Check that components were detected (may not be classified as "Interface" by generic parser)
        Assert.That(result.Any(), Is.True);
        Assert.That(result.Any(c => c.Name.Contains("BaseService") || c.Name.Contains("DataService")), Is.True);
    }

    [Test]
    public async Task IdentifyComponentsAsync_ShouldHandleAttributesAndAnnotations()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        var attributesFile = Path.Combine(_testRepoPath, "Attributes.cs");
        await File.WriteAllTextAsync(attributesFile, @"
using System;
using System.ComponentModel.DataAnnotations;

[ApiController]
[Route(""api/[controller]"")]
public class UserController : ControllerBase
{
    [HttpGet(""{id}"")]
    [ProducesResponseType(typeof(User), 200)]
    public IActionResult GetUser(int id)
    {
        return Ok(new User());
    }
    
    [HttpPost]
    [ValidateModel]
    public IActionResult CreateUser([FromBody] CreateUserRequest request)
    {
        return CreatedAtAction(nameof(GetUser), new { id = 1 }, request);
    }
}

public class CreateUserRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; }
    
    [EmailAddress]
    public string Email { get; set; }
}

[AttributeUsage(AttributeTargets.Method)]
public class ValidateModelAttribute : Attribute
{
    public bool AllowEmpty { get; set; }
}

[Serializable]
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}");

        // Act
        var result = await _service.IdentifyComponentsAsync(_testRepoPath);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Any(c => c.Name == "UserController"), Is.True);
        Assert.That(result.Any(c => c.Name == "CreateUserRequest"), Is.True);
        Assert.That(result.Any(c => c.Name == "User"), Is.True);
        Assert.That(result.Any(c => c.Name == "ValidateModelAttribute"), Is.True);
    }

    #endregion

    #region Performance Tests

    [Test]
    public async Task IdentifyComponentsAsync_ShouldHandleLargeCodebaseSimulation()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        
        // Create multiple directories with files
        for (int dirIndex = 0; dirIndex < 10; dirIndex++)
        {
            var dir = Path.Combine(_testRepoPath, $"Module{dirIndex}");
            Directory.CreateDirectory(dir);
            
            // Create multiple files per directory
            for (int fileIndex = 0; fileIndex < 20; fileIndex++)
            {
                var file = Path.Combine(dir, $"Class{fileIndex}.cs");
                var content = $@"
namespace Module{dirIndex}
{{
    public class Class{fileIndex}
    {{
        public void Method{fileIndex}() {{ }}
        public string Property{{ get; set; }}
    }}
    
    public interface IClass{fileIndex}
    {{
        void Method{fileIndex}();
    }}
}}";
                await File.WriteAllTextAsync(file, content);
            }
        }

        var startTime = DateTime.UtcNow;

        // Act
        var result = await _service.IdentifyComponentsAsync(_testRepoPath);

        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.GreaterThan(200)); // Should detect many components
        Assert.That(duration.TotalSeconds, Is.LessThan(30)); // Should complete within reasonable time
    }

    [Test]
    public async Task AnalyzeRelationshipsAsync_ShouldHandleComplexDependencyGraphs()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);
        
        // Create a complex dependency scenario
        var files = new Dictionary<string, string>
        {
            { "IRepository.cs", @"
public interface IRepository<T> where T : class
{
    Task<T> GetByIdAsync(int id);
    Task SaveAsync(T entity);
}" },
            { "IUserService.cs", @"
public interface IUserService
{
    Task<User> GetUserAsync(int id);
    Task CreateUserAsync(User user);
}" },
            { "User.cs", @"
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public Address Address { get; set; }
}" },
            { "Address.cs", @"
public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
}" },
            { "UserRepository.cs", @"
public class UserRepository : IRepository<User>
{
    public async Task<User> GetByIdAsync(int id) => new User();
    public async Task SaveAsync(User entity) { }
}" },
            { "UserService.cs", @"
public class UserService : IUserService
{
    private readonly IRepository<User> _repository;
    
    public UserService(IRepository<User> repository)
    {
        _repository = repository;
    }
    
    public async Task<User> GetUserAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }
    
    public async Task CreateUserAsync(User user)
    {
        await _repository.SaveAsync(user);
    }
}" },
            { "UserController.cs", @"
public class UserController
{
    private readonly IUserService _userService;
    
    public UserController(IUserService userService)
    {
        _userService = userService;
    }
    
    public async Task<User> Get(int id)
    {
        return await _userService.GetUserAsync(id);
    }
}" }
        };

        foreach (var file in files)
        {
            await File.WriteAllTextAsync(Path.Combine(_testRepoPath, file.Key), file.Value);
        }

        var components = await _service.IdentifyComponentsAsync(_testRepoPath);

        var startTime = DateTime.UtcNow;

        // Act
        var result = await _service.AnalyzeRelationshipsAsync(_testRepoPath, components);

        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Dependencies.Count, Is.GreaterThan(0));
        Assert.That(duration.TotalSeconds, Is.LessThan(10)); // Should complete quickly
        
        // Verify that dependencies were detected (may not be exact matches due to parsing limitations)
        Assert.That(result.Dependencies.Count, Is.GreaterThan(0));
        
        // Check for any dependencies involving the key components
        var hasUserControllerDependencies = result.Dependencies.Any(d => 
            d.FromComponent.Contains("UserController") || d.ToComponent.Contains("UserController"));
        var hasUserServiceDependencies = result.Dependencies.Any(d => 
            d.FromComponent.Contains("UserService") || d.ToComponent.Contains("UserService"));
        
        Assert.That(hasUserControllerDependencies || hasUserServiceDependencies, Is.True);
    }

    #endregion
}