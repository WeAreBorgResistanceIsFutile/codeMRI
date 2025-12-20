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

        var walker = new StructureWalker();
        walker.Visit(root);

        return new ASTParseResult
        {
            Language = "C#",
            FilePath = filePath,
            Tree = root.ToString(), // Or some other representation
            Timestamp = DateTime.UtcNow.ToString("o"),
            Metrics = new { Lines = code.Split('\n').Length, Complexity = walker.TotalComplexity },
            DependencyGraph = new DependencyGraphData { Dependencies = walker.Dependencies },
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
        public List<object> Classes { get; } = new();
        public List<object> Functions { get; } = new();
        public List<string?> Dependencies { get; } = new();
        public List<string> EntryPoints { get; } = new();
        public int TotalComplexity { get; private set; }

        public override void VisitUsingDirective(UsingDirectiveSyntax node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            Dependencies.Add(node.Name?.ToString());
            base.VisitUsingDirective(node);
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            ExtractTypeDeclaration(node, "Class");
            base.VisitClassDeclaration(node);
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            ExtractTypeDeclaration(node, "Interface");
            base.VisitInterfaceDeclaration(node);
        }

        public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
        {
            ExtractTypeDeclaration(node, "Record");
            base.VisitRecordDeclaration(node);
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            ExtractTypeDeclaration(node, "Struct");
            base.VisitStructDeclaration(node);
        }

        private void ExtractTypeDeclaration(TypeDeclarationSyntax node, string type)
        {
            var methods = node.Members.OfType<MethodDeclarationSyntax>()
                .Select(m => m.Identifier.Text).ToList();

            var properties = node.Members.OfType<PropertyDeclarationSyntax>()
                .Select(p => p.Identifier.Text).ToList();

            // Calculate complexity (naive)
            var complexity = node.DescendantNodes().OfType<IfStatementSyntax>().Count()
                             + node.DescendantNodes().OfType<ForStatementSyntax>().Count()
                             + node.DescendantNodes().OfType<ForEachStatementSyntax>().Count()
                             + node.DescendantNodes().OfType<WhileStatementSyntax>().Count()
                             + node.DescendantNodes().OfType<CaseSwitchLabelSyntax>().Count()
                             + node.DescendantNodes().OfType<CatchClauseSyntax>().Count()
                             + 1;

            TotalComplexity += complexity;

            // Check for Main method
            if (methods.Contains("Main")) EntryPoints.Add(node.Identifier.Text);

            var lineCount = node.GetText().Lines.Count;

            Classes.Add(new
            {
                Name = node.Identifier.Text,
                Type = type,
                Metrics = new { Lines = lineCount, Complexity = complexity },
                Methods = methods,
                Properties = properties
            });
        }

        // Handle File-Scoped Namespaces or Top Level statements? 
        // For now this covers standard class structures.
    }
}