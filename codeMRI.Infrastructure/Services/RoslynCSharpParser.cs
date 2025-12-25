using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace codeMRI.Infrastructure.Services;

public interface ICSharpParser
{
    ASTParseResult Parse(string code, string filePath);
}

public class RoslynCSharpParser : ICSharpParser
{
    public ASTParseResult Parse(string code, string filePath)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();

        var walker = new StructureWalker(filePath);
        walker.Visit(root);

        return new ASTParseResult
        {
            Language = "C#",
            FilePath = filePath,
            Tree = root.ToString(),
            Timestamp = DateTime.UtcNow.ToString("o"),
            Metrics = new { Lines = code.Split('\n').Length, Complexity = walker.TotalComplexity },
            DependencyGraph = new DependencyGraphData 
            { 
                Dependencies = walker.Dependencies,
                Nodes = walker.GraphNodes,
                Edges = walker.GraphEdges
            },
            EntryPoints = walker.EntryPoints.Cast<object>().ToList(),
            HierarchicalStructure = new
            {
                walker.Classes, walker.Functions
            },
            CrossModuleReferences = new List<object>()
        };
    }

    private class StructureWalker : CSharpSyntaxWalker
    {
        private readonly string _filePath;
        
        public List<object> Classes { get; } = new();
        public List<object> Functions { get; } = new();
        public List<string?> Dependencies { get; } = new();
        public List<string> EntryPoints { get; } = new();
        public int TotalComplexity { get; private set; }
        
        // New properties for dependency graph
        public List<ASTGraphNode> GraphNodes { get; } = new();
        public List<ASTGraphEdge> GraphEdges { get; } = new();
        
        private string? _currentClassName;

        public StructureWalker(string filePath)
        {
            _filePath = filePath;
        }

        private string CreateNodeId(string identifier)
        {
            return $"{_filePath}::{identifier}";
        }

        public override void VisitUsingDirective(UsingDirectiveSyntax node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            Dependencies.Add(node.Name?.ToString());
            base.VisitUsingDirective(node);
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            ExtractTypeDeclaration(node, "Class", "classes");
            base.VisitClassDeclaration(node);
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            ExtractTypeDeclaration(node, "Interface", "interfaces");
            base.VisitInterfaceDeclaration(node);
        }

        public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
        {
            ExtractTypeDeclaration(node, "Record", "records");
            base.VisitRecordDeclaration(node);
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            ExtractTypeDeclaration(node, "Struct", "structs");
            base.VisitStructDeclaration(node);
        }

        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            var enumName = node.Identifier.Text;
            var previousClassName = _currentClassName;
            _currentClassName = enumName;

            // Create node for the enum
            var typeNodeId = CreateNodeId(enumName);
            GraphNodes.Add(new ASTGraphNode
            {
                Id = typeNodeId,
                Type = "enums",
                Language = "C#",
                FilePath = _filePath,
                Properties = new ASTNodeProperties()
            });

            var members = node.Members.ToList();
            var memberNames = new List<string>();

            foreach (var member in members)
            {
                var memberName = member.Identifier.Text;
                memberNames.Add(memberName);
                
                // Create node for each enum member
                var memberNodeId = CreateNodeId($"{enumName}.{memberName}");
                GraphNodes.Add(new ASTGraphNode
                {
                    Id = memberNodeId,
                    Type = "enum_member",
                    Language = "C#",
                    FilePath = _filePath,
                    Properties = new ASTNodeProperties()
                });

                // Create edge from member to enum
                GraphEdges.Add(new ASTGraphEdge
                {
                    Source = memberNodeId,
                    Target = typeNodeId,
                    Type = "contains",
                    Subtype = "member_to_enum",
                    TargetLanguage = "C#"
                });
            }

            Classes.Add(new
            {
                Name = enumName,
                Type = "Enum",
                Metrics = new { Lines = node.GetText().Lines.Count, Complexity = 1 },
                Methods = new List<string>(),
                Properties = memberNames
            });

            _currentClassName = previousClassName;
            base.VisitEnumDeclaration(node);
        }

        private void ExtractTypeDeclaration(TypeDeclarationSyntax node, string typeDisplayName, string graphNodeType)
        {
            var className = node.Identifier.Text;
            var previousClassName = _currentClassName;
            _currentClassName = className;

            // Create node for the type itself
            var typeNodeId = CreateNodeId(className);
            GraphNodes.Add(new ASTGraphNode
            {
                Id = typeNodeId,
                Type = graphNodeType,
                Language = "C#",
                FilePath = _filePath,
                Properties = new ASTNodeProperties()
            });

            var methods = node.Members.OfType<MethodDeclarationSyntax>().ToList();
            var methodNames = new List<string>();

            foreach (var method in methods)
            {
                var methodName = method.Identifier.Text;
                methodNames.Add(methodName);
                
                // Create node for each method
                var methodNodeId = CreateNodeId($"{className}.{methodName}");
                GraphNodes.Add(new ASTGraphNode
                {
                    Id = methodNodeId,
                    Type = "functions",
                    Language = "C#",
                    FilePath = _filePath,
                    Properties = new ASTNodeProperties()
                });

                // Create edge from method to its containing class
                GraphEdges.Add(new ASTGraphEdge
                {
                    Source = methodNodeId,
                    Target = typeNodeId,
                    Type = "contains",
                    Subtype = "method_to_class",
                    TargetLanguage = "C#"
                });
            }

            var properties = node.Members.OfType<PropertyDeclarationSyntax>().ToList();
            var propertyNames = new List<string>();

            foreach (var property in properties)
            {
                var propertyName = property.Identifier.Text;
                propertyNames.Add(propertyName);
                
                // Create node for each property
                var propertyNodeId = CreateNodeId($"{className}.{propertyName}");
                GraphNodes.Add(new ASTGraphNode
                {
                    Id = propertyNodeId,
                    Type = "properties",
                    Language = "C#",
                    FilePath = _filePath,
                    Properties = new ASTNodeProperties()
                });

                // Create edge from property to its containing class
                GraphEdges.Add(new ASTGraphEdge
                {
                    Source = propertyNodeId,
                    Target = typeNodeId,
                    Type = "contains",
                    Subtype = "property_to_class",
                    TargetLanguage = "C#"
                });
            }

            // Handle inheritance and interface implementation
            if (node.BaseList != null)
            {
                foreach (var baseType in node.BaseList.Types)
                {
                    var baseTypeName = baseType.Type.ToString();
                    var baseTypeNodeId = CreateNodeId(baseTypeName);
                    
                    // Determine if it's inheritance or implementation
                    var edgeType = typeDisplayName == "Interface" ? "extends" : "inherits";
                    
                    GraphEdges.Add(new ASTGraphEdge
                    {
                        Source = typeNodeId,
                        Target = baseTypeNodeId,
                        Type = edgeType,
                        Subtype = "",
                        TargetLanguage = "C#"
                    });
                }
            }

            // Calculate complexity
            var complexity = node.DescendantNodes().OfType<IfStatementSyntax>().Count()
                             + node.DescendantNodes().OfType<ForStatementSyntax>().Count()
                             + node.DescendantNodes().OfType<ForEachStatementSyntax>().Count()
                             + node.DescendantNodes().OfType<WhileStatementSyntax>().Count()
                             + node.DescendantNodes().OfType<CaseSwitchLabelSyntax>().Count()
                             + node.DescendantNodes().OfType<CatchClauseSyntax>().Count()
                             + 1;

            TotalComplexity += complexity;

            // Check for Main method
            if (methodNames.Contains("Main")) EntryPoints.Add(className);

            var lineCount = node.GetText().Lines.Count;

            Classes.Add(new
            {
                Name = className,
                Type = typeDisplayName,
                Metrics = new { Lines = lineCount, Complexity = complexity },
                Methods = methodNames,
                Properties = propertyNames
            });

            _currentClassName = previousClassName;
        }
    }
}