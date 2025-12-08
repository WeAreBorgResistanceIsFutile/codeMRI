using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

public interface IComponentIdentificationService
{
    Task<RepositoryStructure> AnalyzeRepositoryAsync(string repositoryPath);
    Task<List<CodeComponent>> IdentifyComponentsAsync(string repositoryPath);
    Task<ComponentRelationships> AnalyzeRelationshipsAsync(string repositoryPath, List<CodeComponent> components);
}

public class RepositoryStructure
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public List<string> Directories { get; set; } = new();
    public List<string> Files { get; set; } = new();
    public Dictionary<string, int> FileExtensions { get; set; } = new();
}

public class CodeComponent
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Class, Interface, Service, Controller, etc.
    public string FilePath { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public List<string> Dependencies { get; set; } = new();
    public List<string> Methods { get; set; } = new();
    public List<string> Properties { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public int ComplexityScore { get; set; }
    public ComponentMetadata Metadata { get; set; } = new();
}

public class ComponentMetadata
{
    public int Loc { get; set; }
    public int CyclomaticComplexity { get; set; }
    public int NestingDepth { get; set; }
    public int FanIn { get; set; }
    public int FanOut { get; set; }
    public bool IsPublic { get; set; }
    public bool DocstringPresent { get; set; }
    public double EstimatedTokens { get; set; }
}

public class ComponentRelationships
{
    public List<Dependency> Dependencies { get; set; } = new();
    public List<Inheritance> InheritanceHierarchy { get; set; } = new();
    public List<Composition> Compositions { get; set; } = new();
    public List<string> EntryPoints { get; set; } = new();
}

public class Dependency
{
    public string FromComponent { get; set; } = string.Empty;
    public string ToComponent { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // MethodCall, PropertyAccess, TypeReference
    public int Strength { get; set; } // 1-10 scale
}

public class Inheritance
{
    public string BaseComponent { get; set; } = string.Empty;
    public string DerivedComponent { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // ClassInheritance, InterfaceImplementation
}

public class Composition
{
    public string Container { get; set; } = string.Empty;
    public string Contained { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = string.Empty; // Has-a, Uses-a, Contains-a
}