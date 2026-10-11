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

public sealed class SourceTreeIndex
{
    private static readonly Lazy<SourceTreeIndex> LazyAppAndApi = new(() =>
        new SourceTreeIndex(SolutionDirectory.Root, [
            SolutionDirectory.PosApplication,
            SolutionDirectory.PosApi
        ]));

    private static readonly Lazy<SourceTreeIndex> LazySrc = new(() =>
        new SourceTreeIndex(Path.Combine(SolutionDirectory.Root, "src"), [
            Path.Combine(SolutionDirectory.Root, "src")
        ]));

    public static SourceTreeIndex ApplicationAndApi => LazyAppAndApi.Value;
    public static SourceTreeIndex Src => LazySrc.Value;

    public static IReadOnlyList<FileSyntaxInfo> ApplicationAndApiFiles => ApplicationAndApi.Files;

    public static SourceTreeIndex ForDirectory(string directoryPath)
    {
        return new SourceTreeIndex(directoryPath, [directoryPath]);
    }

    public static void EnsureNoSyntaxErrors(SourceTreeIndex? index = null)
    {
        (index ?? ApplicationAndApi).EnsureNoSyntaxErrors();
    }

    public static IReadOnlyList<FileSyntaxInfo> GetDeclaringFilesForType(string typeName)
    {
        EnsureNoSyntaxErrors();
        return ApplicationAndApi.Files
            .Where(f => f.DeclaredTypes.Any(t => t.Name == typeName))
            .ToList();
    }

    private readonly string _rootPath;
    private readonly IReadOnlyList<FileSyntaxInfo> _files;

    public IReadOnlyList<FileSyntaxInfo> Files => _files;

    public SourceTreeIndex(string rootPath, IEnumerable<string> searchDirectories)
    {
        _rootPath = rootPath;
        _files = LoadFiles(searchDirectories);
    }

    public void EnsureNoSyntaxErrors()
    {
        var filesWithErrors = _files.Where(f => f.SyntaxErrors.Count > 0).ToList();
        if (filesWithErrors.Count > 0)
        {
            var details = string.Join("\n", filesWithErrors.Select(f =>
                $"- {Path.GetRelativePath(_rootPath, f.FilePath)}: {string.Join("; ", f.SyntaxErrors.Select(e => e.GetMessage(CultureInfo.InvariantCulture)))}"));
            throw new InvalidOperationException($"Se encontraron errores de sintaxis al parsear archivos C#:\n{details}");
        }
    }

    private static List<FileSyntaxInfo> LoadFiles(IEnumerable<string> directories)
    {
        var paths = new List<string>();
        foreach (var dir in directories)
        {
            if (Directory.Exists(dir))
            {
                paths.AddRange(Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
                    .Where(p => !p.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                  .Any(part => part.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                                               part.Equals("obj", StringComparison.OrdinalIgnoreCase))));
            }
        }

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
