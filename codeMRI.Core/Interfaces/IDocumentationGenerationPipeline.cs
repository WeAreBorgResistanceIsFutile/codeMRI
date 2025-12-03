using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

public interface IDocumentationGenerationPipeline
{
    Task<WikiStructure> GenerateDocumentationAsync(string repositoryPath, DocumentationOptions options);
    Task<WikiPage> GenerateComponentDocumentationAsync(CodeComponent component, RepositoryStructure context);
    Task<List<WikiPage>> GenerateOverviewPagesAsync(RepositoryStructure structure, List<CodeComponent> components);
}

public class DocumentationOptions
{
    public bool IncludeArchitecture { get; set; } = true;
    public bool IncludeApiDocumentation { get; set; } = true;
    public bool IncludeCodeExamples { get; set; } = true;
    public bool IncludeDependencies { get; set; } = true;
    public bool IncludeTestCoverage { get; set; } = false;
    public string TargetAudience { get; set; } = "Developers"; // Developers, Architects, Users
    public int MaxDepth { get; set; } = 3;
    public List<string> ExcludePatterns { get; set; } = new();
}