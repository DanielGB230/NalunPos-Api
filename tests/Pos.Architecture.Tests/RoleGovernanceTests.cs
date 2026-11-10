using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pos.Api.Contracts.Requests;
using Pos.Application.Users.Commands;
using Pos.Architecture.Tests.Support;
using Xunit;

namespace Pos.Architecture.Tests;

public class RoleGovernanceTests
{
    private static CompilationUnitSyntax ParseSnippet(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest));
        var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, $"Snippet de prueba tiene errores de sintaxis:\n{string.Join("\n", errors.Select(e => e.GetMessage(System.Globalization.CultureInfo.InvariantCulture)))}");
        return tree.GetCompilationUnitRoot();
    }

    [Fact]
    public void NoProductionCode_ContainsReservedRoleLiterals_OutsideCanonicalConstants()
    {
        SourceTreeIndex.EnsureNoSyntaxErrors(SourceTreeIndex.Src);
        var failures = new List<string>();

        foreach (var file in SourceTreeIndex.Src.Files)
        {
            var findings = ReservedRoleLiteralScanner.Scan(file.Root, file.FilePath);
            foreach (var finding in findings)
            {
                var relPath = Path.GetRelativePath(SolutionDirectory.Root, file.FilePath);
                failures.Add($"- {relPath}:{finding.LineNumber} | Literal reservado: '{finding.LiteralValue}' | Expresión: {finding.ExpressionText}");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"Se detectaron literales de nombres reservados de rol en src/ fuera de PlatformRoles.cs y TenantRoles.cs (use las constantes canónicas):\n{string.Join("\n", failures)}");
    }

    [Fact]
    public void ReservedRoleLiteralScanner_Ratchet_Must_Remain_Sealed_At_Zero()
    {
        Assert.Empty(ReservedRoleLiteralScanner.AllowedFilesRatchet);
    }

    [Theory]
    [InlineData("class C { string r = \"SuperAdmin\"; }", "SuperAdmin")]
    [InlineData("class C { string r = \" superadmin \"; }", " superadmin ")]
    [InlineData("class C { string r = \"TENANTADMIN\"; }", "TENANTADMIN")]
    [InlineData("class C { string r = @\"Supervisor\"; }", "Supervisor")]
    [InlineData("class C { string r = \"\"\"InventoryManager\"\"\"; }", "InventoryManager")]
    [InlineData("class C { string r = $\"Cajero\"; }", "Cajero")]
    [InlineData("[Authorize(Roles = \"SuperAdmin\")] public class Ctrl {}", "SuperAdmin")]
    public void ReservedRoleLiteralScanner_DetectsReservedLiterals(string code, string expectedLiteral)
    {
        var root = ParseSnippet(code);
        var findings = ReservedRoleLiteralScanner.Scan(root, "src/Pos.Api/Sample.cs");

        var finding = Assert.Single(findings);
        Assert.Equal(expectedLiteral, finding.LiteralValue);
    }

    [Theory]
    [InlineData("src/Pos.Domain/Common/PlatformRoles.cs", "public static class PlatformRoles { public const string SuperAdmin = \"SuperAdmin\"; }")]
    [InlineData("src/Pos.Domain/Common/TenantRoles.cs", "public static class TenantRoles { public const string Cajero = \"Cajero\"; }")]
    public void ReservedRoleLiteralScanner_AllowsCanonicalDefinitionFiles(string filePath, string code)
    {
        var root = ParseSnippet(code);
        var findings = ReservedRoleLiteralScanner.Scan(root, filePath);

        Assert.Empty(findings);
    }

    [Theory]
    [InlineData("// \"SuperAdmin\"\nclass C {}")]
    [InlineData("/* \"TenantAdmin\" */ class C {}")]
    [InlineData("/// <summary>Rol \"Cajero\"</summary>\nclass C {}")]
    [InlineData("class C { string s = \"Cajero del Punto de Venta\"; }")]
    [InlineData("class C { string s = \"SuperAdminSettings\"; }")]
    [InlineData("class C { string r = PlatformRoles.SuperAdmin; string t = TenantRoles.Cajero; }")]
    public void ReservedRoleLiteralScanner_IgnoresCommentsNonMatchingStringsAndConstants(string code)
    {
        var root = ParseSnippet(code);
        var findings = ReservedRoleLiteralScanner.Scan(root, "src/Pos.Application/Sample.cs");

        Assert.Empty(findings);
    }

    [Fact]
    public void UpdateUserRole_Contracts_MustNotExposeTenantId()
    {
        Assert.Null(typeof(UpdateUserRoleRequest).GetProperty("TenantId"));
        Assert.Null(typeof(UpdateUserRoleCommand).GetProperty("TenantId"));

        Assert.DoesNotContain(
            typeof(UpdateUserRoleRequest).GetConstructors().Single().GetParameters(),
            p => string.Equals(p.Name, "tenantId", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            typeof(UpdateUserRoleCommand).GetConstructors().Single().GetParameters(),
            p => string.Equals(p.Name, "tenantId", StringComparison.OrdinalIgnoreCase));
    }
}

