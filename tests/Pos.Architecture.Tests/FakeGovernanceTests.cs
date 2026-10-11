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

    [Fact]
    public void EveryInterface_ImplementedBySupportFakes_HasAtMostOneFakeImplementation()
    {
        var testsDirectory = SolutionDirectory.PosApplicationTests;
        var fakesDirectory = Path.Combine(testsDirectory, "Support", "Fakes");
        Assert.True(Directory.Exists(fakesDirectory), $"El directorio '{fakesDirectory}' no existe.");

        var files = Directory.GetFiles(fakesDirectory, "*.cs", SearchOption.TopDirectoryOnly);
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var interfaceToFakes = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(text, parseOptions, path: file);
            var root = tree.GetCompilationUnitRoot();

            var classDeclarations = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>();

            foreach (var classDecl in classDeclarations)
            {
                if (classDecl.BaseList == null) continue;

                foreach (var baseType in classDecl.BaseList.Types)
                {
                    var interfaceName = baseType.Type.ToString();
                    if (!interfaceToFakes.TryGetValue(interfaceName, out var fakesList))
                    {
                        fakesList = new List<string>();
                        interfaceToFakes[interfaceName] = fakesList;
                    }
                    fakesList.Add(classDecl.Identifier.Text);
                }
            }
        }

        var duplicates = interfaceToFakes
            .Where(kvp => kvp.Value.Count > 1)
            .Select(kvp => $"Interfaz '{kvp.Key}' está implementada por múltiples fakes: {string.Join(", ", kvp.Value)}")
            .ToList();

        Assert.True(
            duplicates.Count == 0,
            $"Se encontraron interfaces implementadas por más de un fake en Support/Fakes:\n{string.Join("\n", duplicates)}"
        );
    }
}
