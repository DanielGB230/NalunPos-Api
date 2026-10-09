using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pos.Architecture.Tests.Support;

namespace Pos.Architecture.Tests;

public class SourceScannerTests
{
    private static CompilationUnitSyntax ParseSnippet(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest));
        var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, $"Snippet de prueba tiene errores de sintaxis:\n{string.Join("\n", errors.Select(e => e.GetMessage(System.Globalization.CultureInfo.InvariantCulture)))}");
        return tree.GetCompilationUnitRoot();
    }

    [Theory]
    [InlineData("public class V {}", 1)]
    [InlineData("public class V {} internal static class H {}", 2)]
    [InlineData("public class V {} internal delegate void D();", 2)]
    [InlineData("public class V {} internal delegate int D();", 2)]
    [InlineData("public class V { private class N {} }", 2)]
    [InlineData("public class V {} public record struct R(int A); public enum E { A } public interface I {}", 4)]
    [InlineData("public class V<T> : B<T>\n  where T : class\n  where T : new() {}", 1)]
    [InlineData("// class Fake {}\n/* class Fake2 {} */\npublic class V { string s = \"class Fake3 {}\"; }", 1)]
    [InlineData("public class V { string s = @\"class Z {}\"; string s2 = \"\"\"class Z2 {}\"\"\"; }", 1)]
    [InlineData("public class V {} file class Hidden {}", 2)]
    public void TypeDeclarationScanner_CountsAllDeclaredTypes(string code, int expectedCount)
    {
        var root = ParseSnippet(code);
        var types = TypeDeclarationScanner.ScanDeclaredTypes(root);
        Assert.Equal(expectedCount, types.Count);
    }

    [Theory]
    [InlineData("Math.Min(request.PageSize, 100);", true)]
    [InlineData("Math.Min(Math.Abs(request.PageSize), 100);", true)]
    [InlineData("System.Math.Clamp(x.PageNumber, 1, 5);", true)]
    [InlineData("using static System.Math;\nclass C { void M() { Min(request.PageSize, 100); } }", true)]
    [InlineData("Math.Max(request.PageSize, 1);", true)]
    [InlineData("Math.Min(a, b);", false)]
    [InlineData("// Math.Min(PageSize, 1)", false)]
    [InlineData("string s = \"Math.Min(PageSize,1)\";", false)]
    public void PaginationSyntaxScanner_DetectsMathPaginationInvocations(string code, bool expectedDetected)
    {
        var root = ParseSnippet(code);
        var detected = PaginationSyntaxScanner.HasDisallowedMathPaginationInvocation(root);
        Assert.Equal(expectedDetected, detected);
    }

    [Theory]
    [InlineData("var x = PaginationRules.MaxPageSize;", true)]
    [InlineData("string s = \"MaxPageSize\";", false)]
    [InlineData("// MaxPageSize", false)]
    public void PaginationSyntaxScanner_DetectsMaxPageSizeIdentifierToken(string code, bool expectedDetected)
    {
        var root = ParseSnippet(code);
        var detected = PaginationSyntaxScanner.HasMaxPageSizeIdentifierToken(root);
        Assert.Equal(expectedDetected, detected);
    }

    [Fact]
    public void Roslyn_SupportsLatestCSharp14Syntax()
    {
        var code = """
            extension(string s)
            {
                public string Name { get; set => field = value.Trim(); }
            }

            class C
            {
                void M(C? a)
                {
                    a?.B = 1;
                }
                public int B { get; set; }
            }
            """;

        ParseSnippet(code);
    }
}
