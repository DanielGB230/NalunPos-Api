using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pos.Architecture.Tests.Support;

public sealed record TokenExpirationFinding(
    string FilePath,
    string Expression,
    int LineNumber,
    string Snippet);

/// <summary>
/// Escáner sintáctico Roslyn (ADR 0016) que prohíbe calcular la expiración del access token
/// en <c>src/Pos.Application</c> y <c>src/Pos.Api</c>. La única fuente de verdad es el
/// generador de tokens en Infrastructure, que devuelve el token junto con su expiración
/// y del que depende el claim <c>exp</c> y el <c>ExpiresAtUtc</c> de la respuesta de login.
/// </summary>
public static class TokenExpirationGuardScanner
{
    /// <summary>
    /// Lista ratchet sellada permanentemente en cero (0).
    /// </summary>
    public static readonly HashSet<string> AllowedFilesRatchet = new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> TemporalArithmeticMethods = new(StringComparer.Ordinal)
    {
        "AddHours",
        "AddMinutes",
        "AddSeconds",
        "AddDays"
    };

    public static IReadOnlyList<TokenExpirationFinding> Scan(SyntaxNode root, string filePath = "")
    {
        ArgumentNullException.ThrowIfNull(root);

        var findings = new List<TokenExpirationFinding>();

        foreach (var declarator in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (!ContainsExpirationIdentifier(declarator.Identifier.ValueText))
            {
                continue;
            }

            if (declarator.Initializer?.Value is not { } initializer)
            {
                continue;
            }

            if (IsTemporalArithmetic(initializer))
            {
                AddFinding(findings, filePath, initializer, declarator.Identifier.ValueText);
            }
        }

        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is MemberAccessExpressionSyntax member &&
                string.Equals(member.Name.Identifier.ValueText, "Expires", StringComparison.Ordinal))
            {
                AddFinding(findings, filePath, assignment, member.ToString());
                continue;
            }

            if (assignment.Left is IdentifierNameSyntax identifier &&
                ContainsExpirationIdentifier(identifier.Identifier.ValueText) &&
                IsTemporalArithmetic(assignment.Right))
            {
                AddFinding(findings, filePath, assignment, identifier.Identifier.ValueText);
            }
        }

        return findings;
    }

    private static bool ContainsExpirationIdentifier(string name) =>
        name.Contains("expir", StringComparison.OrdinalIgnoreCase);

    private static bool IsTemporalArithmetic(ExpressionSyntax expression)
    {
        foreach (var invocation in expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is MemberAccessExpressionSyntax member &&
                TemporalArithmeticMethods.Contains(member.Name.Identifier.ValueText))
            {
                return true;
            }
        }

        return expression is BinaryExpressionSyntax binary &&
               (binary.IsKind(SyntaxKind.AddExpression) || binary.IsKind(SyntaxKind.SubtractExpression)) &&
               binary.Right.ToString().Contains("TimeSpan", StringComparison.Ordinal);
    }

    private static void AddFinding(
        List<TokenExpirationFinding> findings,
        string filePath,
        SyntaxNode node,
        string expression)
    {
        int lineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        findings.Add(new TokenExpirationFinding(filePath, expression, lineNumber, node.ToString()));
    }
}
