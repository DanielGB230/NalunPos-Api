using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pos.Domain.Common;

namespace Pos.Architecture.Tests.Support;

public sealed record ReservedRoleLiteralFinding(
    string FilePath,
    string LiteralValue,
    int LineNumber,
    string ExpressionText);

/// <summary>
/// Escáner sintáctico Roslyn (ADR 0016) que prohíbe cualquier literal de cadena igual a un nombre
/// de rol reservado en src/ fuera de sus archivos fuente canónicos (PlatformRoles.cs y TenantRoles.cs).
/// </summary>
public static class ReservedRoleLiteralScanner
{
    /// <summary>
    /// Lista ratchet sellada permanentemente en cero (0) desde su introducción.
    /// </summary>
    public static readonly HashSet<string> AllowedFilesRatchet = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] AuthorizedCanonicalFiles =
    [
        "src/Pos.Domain/Common/PlatformRoles.cs",
        "src/Pos.Domain/Common/TenantRoles.cs"
    ];

    public static IReadOnlyList<ReservedRoleLiteralFinding> Scan(SyntaxNode root, string filePath = "")
    {
        ArgumentNullException.ThrowIfNull(root);

        var normalizedPath = filePath.Replace('\\', '/');
        if (AuthorizedCanonicalFiles.Any(canonical =>
                normalizedPath.EndsWith(canonical, StringComparison.OrdinalIgnoreCase)))
        {
            return [];
        }

        var findings = new List<ReservedRoleLiteralFinding>();

        // 1. Literales de cadena (normales, verbatim @"...", raw """...""", UTF-8 "..."u8)
        foreach (var literal in root.DescendantNodes().OfType<LiteralExpressionSyntax>())
        {
            if (!literal.IsKind(SyntaxKind.StringLiteralExpression) &&
                !literal.IsKind(SyntaxKind.Utf8StringLiteralExpression))
            {
                continue;
            }

            string value = literal.Token.ValueText;
            if (RoleNames.IsReserved(value))
            {
                var lineSpan = literal.GetLocation().GetLineSpan();
                int lineNumber = lineSpan.StartLinePosition.Line + 1;
                findings.Add(new ReservedRoleLiteralFinding(filePath, value, lineNumber, literal.ToString()));
            }
        }

        // 2. Cadenas interpoladas constantes sin expresiones interpoladas (ej. $"SuperAdmin")
        foreach (var interpolated in root.DescendantNodes().OfType<InterpolatedStringExpressionSyntax>())
        {
            if (interpolated.Contents.Count == 1 &&
                interpolated.Contents[0] is InterpolatedStringTextSyntax textSyntax)
            {
                string value = textSyntax.TextToken.ValueText;
                if (RoleNames.IsReserved(value))
                {
                    var lineSpan = interpolated.GetLocation().GetLineSpan();
                    int lineNumber = lineSpan.StartLinePosition.Line + 1;
                    findings.Add(new ReservedRoleLiteralFinding(filePath, value, lineNumber, interpolated.ToString()));
                }
            }
        }

        return findings;
    }
}

