using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pos.Architecture.Tests.Support;

public sealed record TenantFallbackFinding(
    string FilePath,
    int LineNumber,
    string PatternDescription,
    string ExpressionText);

public static class TenantFallbackScanner
{
    public static IReadOnlyList<TenantFallbackFinding> Scan(SyntaxNode root, string filePath = "")
    {
        var findings = new List<TenantFallbackFinding>();

        // 1. Prohibido: `TenantId ?? Guid.Empty`
        foreach (var coalesce in root.DescendantNodes().OfType<BinaryExpressionSyntax>())
        {
            if (coalesce.IsKind(SyntaxKind.CoalesceExpression))
            {
                var leftText = coalesce.Left.ToString();
                var rightText = coalesce.Right.ToString();

                bool isTenantLeft = leftText.Contains("TenantId", StringComparison.Ordinal);
                bool isGuidEmptyRight = rightText.Contains("Guid.Empty", StringComparison.Ordinal);

                if (isTenantLeft && isGuidEmptyRight)
                {
                    var lineSpan = coalesce.GetLocation().GetLineSpan();
                    int line = lineSpan.StartLinePosition.Line + 1;
                    findings.Add(new TenantFallbackFinding(
                        filePath,
                        line,
                        "TenantId ?? Guid.Empty",
                        coalesce.ToString()));
                }
            }
        }

        // 2. Prohibido: Guid.NewGuid() para fabricar/resolver tenant
        // Buscamos asignaciones o expresiones donde una variable/campo llamado tenant o tenantId reciba Guid.NewGuid()
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var invText = invocation.ToString();
            if (invText.Contains("Guid.NewGuid()", StringComparison.Ordinal))
            {
                // Verificar si se asigna a tenantId o tenant
                if (invocation.Parent is AssignmentExpressionSyntax assignment)
                {
                    var leftVar = assignment.Left.ToString();
                    if (leftVar.Contains("tenant", StringComparison.OrdinalIgnoreCase))
                    {
                        var lineSpan = invocation.GetLocation().GetLineSpan();
                        int line = lineSpan.StartLinePosition.Line + 1;
                        findings.Add(new TenantFallbackFinding(
                            filePath,
                            line,
                            "Asignación de Guid.NewGuid() a variable de tenant",
                            assignment.ToString()));
                    }
                }
                else if (invocation.Parent is EqualsValueClauseSyntax equalsClause &&
                         equalsClause.Parent is VariableDeclaratorSyntax varDecl)
                {
                    if (varDecl.Identifier.ValueText.Contains("tenant", StringComparison.OrdinalIgnoreCase))
                    {
                        var lineSpan = invocation.GetLocation().GetLineSpan();
                        int line = lineSpan.StartLinePosition.Line + 1;
                        findings.Add(new TenantFallbackFinding(
                            filePath,
                            line,
                            "Inicialización con Guid.NewGuid() para variable de tenant",
                            varDecl.ToString()));
                    }
                }
            }
        }

        return findings;
    }
}
