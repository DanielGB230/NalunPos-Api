using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pos.Architecture.Tests.Support;
using Xunit;

namespace Pos.Architecture.Tests;

public class AuthenticationGovernanceTests
{
    private static CompilationUnitSyntax ParseSnippet(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest));
        var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, $"Snippet de prueba tiene errores de sintaxis:\n{string.Join("\n", errors.Select(e => e.GetMessage(System.Globalization.CultureInfo.InvariantCulture)))}");
        return tree.GetCompilationUnitRoot();
    }

    [Fact]
    public void AuthenticationCode_MustNot_CatchInvalidOperationException()
    {
        SourceTreeIndex.EnsureNoSyntaxErrors(SourceTreeIndex.Src);
        var failures = new List<string>();

        foreach (var file in SourceTreeIndex.Src.Files)
        {
            var normalized = file.FilePath.Replace('\\', '/');
            if (!normalized.Contains("/src/Pos.Infrastructure/Authentication/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var findings = AuthenticationCatchGuardScanner.Scan(file.Root, file.FilePath);
            foreach (var finding in findings)
            {
                var relPath = Path.GetRelativePath(SolutionDirectory.Root, file.FilePath);
                failures.Add($"- {relPath}:{finding.LineNumber} | Prohibido catch ({finding.ExceptionType}) en Authentication: {finding.Snippet}");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"No se permite capturar InvalidOperationException dentro de src/Pos.Infrastructure/Authentication/:\n{string.Join("\n", failures)}");
    }

    [Fact]
    public void AuthenticationCatchGuardScanner_Ratchet_Must_Remain_Sealed_At_Zero()
    {
        Assert.Empty(AuthenticationCatchGuardScanner.AllowedFilesRatchet);
    }

    [Theory]
    [InlineData("class C { void M() { try {} catch (InvalidOperationException) {} } }")]
    [InlineData("class C { void M() { try {} catch (InvalidOperationException ex) {} } }")]
    [InlineData("class C { void M() { try {} catch (System.InvalidOperationException ex) {} } }")]
    [InlineData("class C { void M() { try {} catch (global::System.InvalidOperationException) {} } }")]
    [InlineData("class C { void M() { try {} catch (InvalidOperationException ex) when (ex.Message != null) {} } }")]
    public void AuthenticationCatchGuardScanner_WhenInvalidOperationExceptionCaught_DetectsViolation(string code)
    {
        var root = ParseSnippet(code);
        var findings = AuthenticationCatchGuardScanner.Scan(root, "src/Pos.Infrastructure/Authentication/Sample.cs");

        Assert.Single(findings);
    }

    [Theory]
    [InlineData("class C { void M() { try {} catch (FormatException) {} } }")]
    [InlineData("class C { void M() { try {} catch (ArgumentException) {} } }")]
    [InlineData("class C { void M() { try {} finally {} } }")]
    [InlineData("// catch (InvalidOperationException)\nclass C { string s = \"catch (InvalidOperationException)\"; }")]
    public void AuthenticationCatchGuardScanner_IgnoresOtherExceptionsFinallyAndComments(string code)
    {
        var root = ParseSnippet(code);
        var findings = AuthenticationCatchGuardScanner.Scan(root, "src/Pos.Infrastructure/Authentication/Sample.cs");

        Assert.Empty(findings);
    }
}

