namespace Pos.Architecture.Tests;

using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Pos.Architecture.Tests.Support;
using Xunit;

public class FakeGovernanceTests
{
    [Fact]
    public void NoFakeTypes_DeclaredOutsideOfSupportFakes_InApplicationTests()
    {
        var testsDirectory = SolutionDirectory.PosApplicationTests;
        Assert.True(Directory.Exists(testsDirectory), $"El directorio '{testsDirectory}' no existe.");

        var files = Directory.GetFiles(testsDirectory, "*.cs", SearchOption.AllDirectories);
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var failures = new List<string>();

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(testsDirectory, file);
            var normalizedRelative = relativePath.Replace('\\', '/');

            // Ignorar archivos dentro de Support/Fakes
            if (normalizedRelative.StartsWith("Support/Fakes/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Ignorar carpetas obj/ o bin/ si existieran
            if (normalizedRelative.StartsWith("obj/", StringComparison.OrdinalIgnoreCase) ||
                normalizedRelative.StartsWith("bin/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(text, parseOptions, path: file);
            var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (errors.Count > 0)
            {
                var errorDetails = string.Join("; ", errors.Select(e => e.GetMessage(CultureInfo.InvariantCulture)));
                failures.Add($"Error de sintaxis en '{normalizedRelative}': {errorDetails}");
                continue;
            }

            var root = tree.GetCompilationUnitRoot();
            var declaredTypes = TypeDeclarationScanner.ScanDeclaredTypes(root);

            foreach (var type in declaredTypes)
            {
                if (type.Name.StartsWith("Fake", StringComparison.Ordinal))
                {
                    failures.Add($"Tipo '{type.Name}' ({type.DeclarationKind}) declarado fuera de Support/Fakes en '{normalizedRelative}'.");
                }
            }
        }

        Assert.True(
            failures.Count == 0,
            $"Se encontraron tipos Fake declarados fuera de Support/Fakes en Pos.Application.Tests:\n{string.Join("\n", failures)}"
        );
    }
}
