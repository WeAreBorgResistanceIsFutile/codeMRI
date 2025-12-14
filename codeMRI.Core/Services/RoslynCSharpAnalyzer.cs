using System.Text.RegularExpressions;
using codeMRI.Core.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services;

public class RoslynCSharpAnalyzer
{
    private readonly ILogger<RoslynCSharpAnalyzer> _logger;

    public RoslynCSharpAnalyzer(ILogger<RoslynCSharpAnalyzer> logger)
    {
        _logger = logger;
    }

    public async Task<List<CodeComponent>> AnalyzeCSharpFileAsync(string filePath)
    {
        var components = new List<CodeComponent>();

        try
        {
            var sourceCode = await File.ReadAllTextAsync(filePath);
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = await tree.GetRootAsync();
            var compilation = CSharpCompilation.Create("Temp")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddSyntaxTrees(tree);

            var semanticModel = compilation.GetSemanticModel(tree);

            // Find all class declarations
            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            foreach (var classDecl in classDeclarations)
            {
                var component = await AnalyzeClassOrInterfaceAsync(classDecl, semanticModel, filePath, "Class");
                if (component != null)
                    components.Add(component);
            }

            // Find all interface declarations
            var interfaceDeclarations = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
            foreach (var interfaceDecl in interfaceDeclarations)
            {
                var component = await AnalyzeClassOrInterfaceAsync(interfaceDecl, semanticModel, filePath, "Interface");
                if (component != null)
                    components.Add(component);
            }

            // Find all record declarations
            var recordDeclarations = root.DescendantNodes().OfType<RecordDeclarationSyntax>();
            foreach (var recordDecl in recordDeclarations)
            {
                var component = await AnalyzeRecordAsync(recordDecl, semanticModel, filePath);
                if (component != null)
                    components.Add(component);
            }
        }
        catch (Exception ex)
        {
            // Log error but continue processing other files
            _logger.LogError(ex, "Error analyzing file {FilePath}: {Message}", filePath, ex.Message);
        }

        return components;
    }

    private Task<CodeComponent?> AnalyzeClassOrInterfaceAsync(
        BaseTypeDeclarationSyntax declaration,
        SemanticModel semanticModel,
        string filePath,
        string componentType)
    {
        var symbol = semanticModel.GetDeclaredSymbol(declaration);
        if (symbol == null) return Task.FromResult<CodeComponent?>(null);

        var component = new CodeComponent
        {
            Id = symbol.ToDisplayString(),
            Name = symbol.Name,
            Type = DetermineComponentType(symbol, componentType),
            FilePath = filePath,
            Language = "C#",
            LineCount = declaration.GetLocation().GetLineSpan().EndLinePosition.Line -
                declaration.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            ComplexityScore = CalculateComplexity(declaration, semanticModel)
        };

        // Extract methods
        var methods = declaration.DescendantNodes().OfType<MethodDeclarationSyntax>();
        component.Methods = methods.Select(m =>
        {
            var methodSymbol = semanticModel.GetDeclaredSymbol(m);
            return methodSymbol != null
                ? $"{methodSymbol.ReturnType.Name} {methodSymbol.Name}({string.Join(", ", methodSymbol.Parameters.Select(p => p.Type.Name))})"
                : m.Identifier.Text;
        }).ToList();

        // Extract properties
        var properties = declaration.DescendantNodes().OfType<PropertyDeclarationSyntax>();
        component.Properties = properties.Select(p =>
        {
            var propertySymbol = semanticModel.GetDeclaredSymbol(p);
            return propertySymbol != null
                ? $"{propertySymbol.Type.Name} {propertySymbol.Name}"
                : $"{p.Type} {p.Identifier.Text}";
        }).ToList();

        // Extract dependencies
        component.Dependencies = ExtractDependencies(declaration, semanticModel);

        // Generate description
        component.Description = GenerateComponentDescription(symbol, semanticModel);

        return Task.FromResult<CodeComponent?>(component);
    }

    private Task<CodeComponent?> AnalyzeRecordAsync(
        RecordDeclarationSyntax declaration,
        SemanticModel semanticModel,
        string filePath)
    {
        var symbol = semanticModel.GetDeclaredSymbol(declaration);
        if (symbol == null) return Task.FromResult<CodeComponent?>(null);

        var component = new CodeComponent
        {
            Id = symbol.ToDisplayString(),
            Name = symbol.Name,
            Type = "Record",
            FilePath = filePath,
            Language = "C#",
            LineCount = declaration.GetLocation().GetLineSpan().EndLinePosition.Line -
                declaration.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            ComplexityScore = CalculateComplexity(declaration, semanticModel)
        };

        // Extract methods
        var methods = declaration.DescendantNodes().OfType<MethodDeclarationSyntax>();
        component.Methods = methods.Select(m =>
        {
            var methodSymbol = semanticModel.GetDeclaredSymbol(m);
            return methodSymbol != null
                ? $"{methodSymbol.ReturnType.Name} {methodSymbol.Name}({string.Join(", ", methodSymbol.Parameters.Select(p => p.Type.Name))})"
                : m.Identifier.Text;
        }).ToList();

        // Extract properties (including record parameters)
        var properties = declaration.DescendantNodes().OfType<PropertyDeclarationSyntax>().ToList();
        var parameterList = declaration.ParameterList?.Parameters.Select(p => $"{p.Type} {p.Identifier.Text}") ??
                            new List<string>();

        component.Properties = properties.Select(p =>
        {
            var propertySymbol = semanticModel.GetDeclaredSymbol(p);
            return propertySymbol != null
                ? $"{propertySymbol.Type.Name} {propertySymbol.Name}"
                : $"{p.Type} {p.Identifier.Text}";
        }).Concat(parameterList).ToList();

        // Extract dependencies
        component.Dependencies = ExtractDependencies(declaration, semanticModel);

        // Generate description
        component.Description = GenerateComponentDescription(symbol, semanticModel);

        return Task.FromResult<CodeComponent?>(component);
    }

    private string DetermineComponentType(INamedTypeSymbol symbol, string baseType)
    {
        // Check for common patterns and attributes
        if (symbol.GetAttributes().Any(attr => attr.AttributeClass?.Name.Contains("Controller") == true))
            return "Controller";

        if (symbol.GetAttributes().Any(attr => attr.AttributeClass?.Name.Contains("Service") == true))
            return "Service";

        if (symbol.GetAttributes().Any(attr => attr.AttributeClass?.Name.Contains("Repository") == true))
            return "Repository";

        if (symbol.BaseType?.Name.Contains("Controller") == true)
            return "Controller";

        if (symbol.Name.EndsWith("Service"))
            return "Service";

        if (symbol.Name.EndsWith("Repository"))
            return "Repository";

        if (symbol.Name.EndsWith("Controller"))
            return "Controller";

        return baseType;
    }

    private int CalculateComplexity(BaseTypeDeclarationSyntax declaration, SemanticModel semanticModel)
    {
        var complexity = 1; // Base complexity

        // Count decision points
        var descendantNodes = declaration.DescendantNodes();

        // If statements
        complexity += descendantNodes.OfType<IfStatementSyntax>().Count();

        // Switch statements
        complexity += descendantNodes.OfType<SwitchStatementSyntax>().Count() * 2;

        // While loops
        complexity += descendantNodes.OfType<WhileStatementSyntax>().Count();

        // For loops
        complexity += descendantNodes.OfType<ForStatementSyntax>().Count();

        // Foreach loops
        complexity += descendantNodes.OfType<ForEachStatementSyntax>().Count();

        // Conditional operator (?:)
        complexity += descendantNodes.OfType<ConditionalExpressionSyntax>().Count();

        // Logical AND/OR operators
        complexity += descendantNodes.OfType<BinaryExpressionSyntax>()
            .Count(be => be.Kind() == SyntaxKind.LogicalAndExpression || be.Kind() == SyntaxKind.LogicalOrExpression);

        // Count methods and properties
        complexity += declaration.DescendantNodes().OfType<MethodDeclarationSyntax>().Count();
        complexity += declaration.DescendantNodes().OfType<PropertyDeclarationSyntax>().Count();

        return complexity;
    }

    private List<string> ExtractDependencies(BaseTypeDeclarationSyntax declaration, SemanticModel semanticModel)
    {
        var dependencies = new HashSet<string>();

        // Base types and interfaces
        if (declaration is ClassDeclarationSyntax classDecl)
        {
            if (classDecl.BaseList != null)
                foreach (var baseType in classDecl.BaseList.Types)
                {
                    var typeInfo = semanticModel.GetTypeInfo(baseType.Type);
                    if (typeInfo.Type != null) dependencies.Add(typeInfo.Type.ToDisplayString());
                }
        }
        else if (declaration is InterfaceDeclarationSyntax interfaceDecl)
        {
            if (interfaceDecl.BaseList != null)
                foreach (var baseType in interfaceDecl.BaseList.Types)
                {
                    var typeInfo = semanticModel.GetTypeInfo(baseType.Type);
                    if (typeInfo.Type != null) dependencies.Add(typeInfo.Type.ToDisplayString());
                }
        }

        // Method return types and parameter types
        var methods = declaration.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            // Return type
            var returnTypeInfo = semanticModel.GetTypeInfo(method.ReturnType);
            if (returnTypeInfo.Type != null && !IsSystemType(returnTypeInfo.Type))
                dependencies.Add(returnTypeInfo.Type.ToDisplayString());

            // Parameter types
            foreach (var param in method.ParameterList.Parameters)
                if (param.Type != null)
                {
                    var paramTypeInfo = semanticModel.GetTypeInfo(param.Type);
                    if (paramTypeInfo.Type != null && !IsSystemType(paramTypeInfo.Type))
                        dependencies.Add(paramTypeInfo.Type.ToDisplayString());
                }
        }

        // Property types
        var properties = declaration.DescendantNodes().OfType<PropertyDeclarationSyntax>();
        foreach (var property in properties)
            if (property.Type != null)
            {
                var propertyTypeInfo = semanticModel.GetTypeInfo(property.Type);
                if (propertyTypeInfo.Type != null && !IsSystemType(propertyTypeInfo.Type))
                    dependencies.Add(propertyTypeInfo.Type.ToDisplayString());
            }

        // Field types
        var fields = declaration.DescendantNodes().OfType<FieldDeclarationSyntax>();
        foreach (var field in fields)
            if (field.Declaration?.Type != null)
            {
                var fieldTypeInfo = semanticModel.GetTypeInfo(field.Declaration.Type);
                if (fieldTypeInfo.Type != null && !IsSystemType(fieldTypeInfo.Type))
                    dependencies.Add(fieldTypeInfo.Type.ToDisplayString());
            }

        return dependencies.ToList();
    }

    private bool IsSystemType(ITypeSymbol type)
    {
        return type.ContainingNamespace?.Name == "System" &&
               (type.ContainingNamespace.ContainingNamespace?.IsGlobalNamespace == true ||
                type.ContainingNamespace.Name == "Collections" ||
                type.ContainingNamespace.Name == "Generic" ||
                type.ContainingNamespace.Name == "Threading" ||
                type.ContainingNamespace.Name == "Linq");
    }

    private string GenerateComponentDescription(INamedTypeSymbol symbol, SemanticModel semanticModel)
    {
        var description = $"{symbol.TypeKind} {symbol.Name}";

        // Add inheritance information
        if (symbol.BaseType != null && symbol.BaseType.SpecialType != SpecialType.System_Object)
            description += $" inheriting from {symbol.BaseType.Name}";

        if (symbol.Interfaces.Length > 0)
            description += $" implementing {string.Join(", ", symbol.Interfaces.Select(i => i.Name))}";

        // Add member count summary
        var publicMethods = symbol.GetMembers().OfType<IMethodSymbol>()
            .Count(m => m.DeclaredAccessibility == Accessibility.Public);
        var publicProperties = symbol.GetMembers().OfType<IPropertySymbol>()
            .Count(p => p.DeclaredAccessibility == Accessibility.Public);

        description += $". Contains {publicMethods} public methods and {publicProperties} public properties.";

        // Add XML documentation if available
        var xmlComment = symbol.GetDocumentationCommentXml();
        if (!string.IsNullOrEmpty(xmlComment))
        {
            // Simple extraction of summary from XML docs
            var summaryMatch = Regex.Match(xmlComment, @"<summary>(.*?)</summary>", RegexOptions.Singleline);
            if (summaryMatch.Success)
            {
                var summary = summaryMatch.Groups[1].Value.Trim();
                summary = Regex.Replace(summary, @"<[^>]*>", ""); // Remove HTML tags
                description += $" Documentation: {summary}";
            }
        }

        return description;
    }
}