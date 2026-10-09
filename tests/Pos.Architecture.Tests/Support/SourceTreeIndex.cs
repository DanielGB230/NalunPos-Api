using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pos.Architecture.Tests.Support;

public sealed record DeclaredTypeInfo(string Name, string DeclarationKind, MemberDeclarationSyntax SyntaxNode);

public sealed record FileSyntaxInfo(
    string FilePath,
    SyntaxTree SyntaxTree,
    CompilationUnitSyntax Root,
    IReadOnlyList<DeclaredTypeInfo> DeclaredTypes,
    IReadOnlyList<Diagnostic> SyntaxErrors);

public static class SourceTreeIndex
{
    private static readonly Lazy<IReadOnlyList<FileSyntaxInfo>> LazyFiles = new(LoadAllFiles);

    public static IReadOnlyList<FileSyntaxInfo> ApplicationAndApiFiles => LazyFiles.Value;

    public static void EnsureNoSyntaxErrors()
    {
        var filesWithErrors = ApplicationAndApiFiles.Where(f => f.SyntaxErrors.Count > 0).ToList();
        if (filesWithErrors.Count > 0)
        {
            var details = string.Join("\n", filesWithErrors.Select(f =>
                $"- {Path.GetRelativePath(SolutionDirectory.Root, f.FilePath)}: {string.Join("; ", f.SyntaxErrors.Select(e => e.GetMessage(CultureInfo.InvariantCulture)))}"));
            throw new InvalidOperationException($"Se encontraron errores de sintaxis al parsear archivos C#:\n{details}");
        }
    }

    public static IReadOnlyList<FileSyntaxInfo> GetDeclaringFilesForType(string typeName)
    {
        EnsureNoSyntaxErrors();
        return ApplicationAndApiFiles
            .Where(f => f.DeclaredTypes.Any(t => t.Name == typeName))
            .ToList();
    }

    private static List<FileSyntaxInfo> LoadAllFiles()
    {
        var paths = new List<string>();
        if (Directory.Exists(SolutionDirectory.PosApplication))
            paths.AddRange(Directory.GetFiles(SolutionDirectory.PosApplication, "*.cs", SearchOption.AllDirectories));
        if (Directory.Exists(SolutionDirectory.PosApi))
            paths.AddRange(Directory.GetFiles(SolutionDirectory.PosApi, "*.cs", SearchOption.AllDirectories));

        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var result = new List<FileSyntaxInfo>();

        foreach (var file in paths)
        {
            var text = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(text, parseOptions, path: file);
            var root = tree.GetCompilationUnitRoot();
            var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

            var declaredTypes = TypeDeclarationScanner.ScanDeclaredTypes(root);
            var fileInfo = new FileSyntaxInfo(file, tree, root, declaredTypes, errors);

            result.Add(fileInfo);
        }

        return result;
    }
}
