namespace Pos.Architecture.Tests;

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pos.Architecture.Tests.Support;
using Xunit;

public class FakeGovernanceTests
{
    [Fact]
    public void NoFakeTypes_DeclaredOutsideOfSupportFakes_InApplicationTests()
    {
        var testsDirectory = SolutionDirectory.PosApplicationTests;
        Assert.True(Directory.Exists(testsDirectory), $"El directorio '{testsDirectory}' no existe.");

        var testIndex = SourceTreeIndex.ForDirectory(testsDirectory);
        testIndex.EnsureNoSyntaxErrors();

        var failures = new List<string>();

        foreach (var file in testIndex.Files)
        {
            var relativePath = Path.GetRelativePath(testsDirectory, file.FilePath);
            var normalizedRelative = relativePath.Replace('\\', '/');

            // Ignorar archivos dentro de Support/Fakes
            if (normalizedRelative.StartsWith("Support/Fakes/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var type in file.DeclaredTypes)
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

        var fakesIndex = SourceTreeIndex.ForDirectory(fakesDirectory);
        fakesIndex.EnsureNoSyntaxErrors();

        var srcIndex = SourceTreeIndex.Src;
        srcIndex.EnsureNoSyntaxErrors();

        var srcInterfaces = srcIndex.Files
            .SelectMany(f => f.DeclaredTypes)
            .Where(t => t.DeclarationKind == "interface")
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        var interfaceToFakes = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var file in fakesIndex.Files)
        {
            var classDeclarations = file.Root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var classDecl in classDeclarations)
            {
                if (classDecl.BaseList == null) continue;

                foreach (var baseType in classDecl.BaseList.Types)
                {
                    var interfaceName = baseType.Type.ToString();
                    if (!srcInterfaces.Contains(interfaceName))
                    {
                        continue;
                    }

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
