using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pos.Architecture.Tests.Support;
using Xunit;

namespace Pos.Architecture.Tests;

public class AttributeGovernanceTests
{
    private static CompilationUnitSyntax ParseSnippet(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest));
        var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, $"Snippet de prueba tiene errores de sintaxis:\n{string.Join("\n", errors.Select(e => e.GetMessage(System.Globalization.CultureInfo.InvariantCulture)))}");
        return tree.GetCompilationUnitRoot();
    }

    [Fact]
    public void NoTypeDeclaration_HasDuplicateAttributes()
    {
        var failures = new List<string>();

        foreach (var file in SourceTreeIndex.ApplicationAndApi.Files)
        {
            var duplicates = AttributeDuplicationScanner.Scan(file.Root);
            foreach (var dup in duplicates)
            {
                var relPath = Path.GetRelativePath(SolutionDirectory.Root, file.FilePath);
                failures.Add($"- {relPath} | Tipo: {dup.TypeName} | Atributo: [{dup.NormalizedAttribute}] (aparece {dup.Count} veces)");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"Se encontraron declaraciones de tipo con atributos duplicados:\n{string.Join("\n", failures)}");
    }

    [Fact]
    public void AttributeDuplicationScanner_IdenticalAttributesOnClass_DetectsDuplicate()
    {
        var code = """
            [HasPermission("A")]
            [HasPermission("A")]
            public class TestClass {}
            """;
        var root = ParseSnippet(code);

        var findings = AttributeDuplicationScanner.Scan(root);

        Assert.Single(findings);
        Assert.Equal("TestClass", findings[0].TypeName);
        Assert.Equal("HasPermission(\"A\")", findings[0].NormalizedAttribute);
        Assert.Equal(2, findings[0].Count);
    }

    [Fact]
    public void AttributeDuplicationScanner_DifferentAttributesOnClass_DoesNotDetect()
    {
        var code = """
            [HasPermission("A")]
            [HasPermission("B")]
            public class TestClass {}
            """;
        var root = ParseSnippet(code);

        var findings = AttributeDuplicationScanner.Scan(root);

        Assert.Empty(findings);
    }

    [Fact]
    public void AttributeDuplicationScanner_SingleAttributeOnClass_DoesNotDetect()
    {
        var code = """
            [HasPermission("A")]
            public class TestClass {}
            """;
        var root = ParseSnippet(code);

        var findings = AttributeDuplicationScanner.Scan(root);

        Assert.Empty(findings);
    }
}
