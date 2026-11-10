using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pos.Architecture.Tests.Support;

public sealed record CatchGuardFinding(
    string FilePath,
    string ExceptionType,
    int LineNumber,
    string Snippet);

/// <summary>
/// Escáner sintáctico Roslyn (ADR 0016) que prohíbe capturar <see cref="InvalidOperationException"/>
/// dentro de <c>src/Pos.Infrastructure/Authentication/</c> para evitar tolerancias ocultas a proveedores InMemory.
/// </summary>
public static class AuthenticationCatchGuardScanner
{
    /// <summary>
    /// Lista ratchet sellada permanentemente en cero (0).
    /// </summary>
    public static readonly HashSet<string> AllowedFilesRatchet = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<CatchGuardFinding> Scan(SyntaxNode root, string filePath = "")
    {
        ArgumentNullException.ThrowIfNull(root);

        var findings = new List<CatchGuardFinding>();

        foreach (var catchClause in root.DescendantNodes().OfType<CatchClauseSyntax>())
        {
            if (catchClause.Declaration == null)
            {
                continue;
            }

            string simpleName = ExtractRightmostSimpleName(catchClause.Declaration.Type);
            if (string.Equals(simpleName, nameof(InvalidOperationException), StringComparison.Ordinal))
            {
                var lineSpan = catchClause.Declaration.GetLocation().GetLineSpan();
                int lineNumber = lineSpan.StartLinePosition.Line + 1;
                findings.Add(new CatchGuardFinding(
                    filePath,
                    catchClause.Declaration.Type.ToString(),
                    lineNumber,
                    catchClause.Declaration.ToString()));
            }
        }

        return findings;
    }

    private static string ExtractRightmostSimpleName(TypeSyntax typeSyntax) => typeSyntax switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.ValueText,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        _ => typeSyntax.ToString()
    };
}

