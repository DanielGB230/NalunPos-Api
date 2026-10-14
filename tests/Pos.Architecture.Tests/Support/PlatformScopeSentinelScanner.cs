using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pos.Architecture.Tests.Support;

public sealed record PlatformScopeSentinelFinding(
    string FilePath,
    int LineNumber,
    string PatternDescription,
    string ExpressionText);

/// <summary>
/// Escáner Roslyn para verificar que en src/Pos.Infrastructure y src/Pos.Application esté prohibido
/// el uso de Guid.Empty directo como centinela de tenant (ej. TenantId ?? Guid.Empty, comparaciones e.TenantId == Guid.Empty),
/// obligando al uso de la constante fuertemente tipada PlatformScope.TenantId (ADR 0008, ADR 0017).
/// </summary>
public static class PlatformScopeSentinelScanner
{
    public static IReadOnlyList<PlatformScopeSentinelFinding> Scan(SyntaxNode root, string filePath = "")
    {
        var findings = new List<PlatformScopeSentinelFinding>();

        // 1. Prohibido: Coalesce con Guid.Empty sobre variables o propiedades de Tenant (ej. _tenantContext.TenantId ?? Guid.Empty)
        foreach (var coalesce in root.DescendantNodes().OfType<BinaryExpressionSyntax>())
        {
            if (coalesce.IsKind(SyntaxKind.CoalesceExpression))
            {
                var leftText = coalesce.Left.ToString();
                var rightText = coalesce.Right.ToString();

                bool isTenantLeft = leftText.Contains("TenantId", StringComparison.OrdinalIgnoreCase) ||
                                    leftText.Contains("Tenant", StringComparison.OrdinalIgnoreCase);
                bool isGuidEmptyRight = rightText.Contains("Guid.Empty", StringComparison.Ordinal);

                if (isTenantLeft && isGuidEmptyRight)
                {
                    var lineSpan = coalesce.GetLocation().GetLineSpan();
                    int line = lineSpan.StartLinePosition.Line + 1;
                    findings.Add(new PlatformScopeSentinelFinding(
                        filePath,
                        line,
                        "Uso de Guid.Empty en coalesce de Tenant. Debe usar PlatformScope.TenantId.",
                        coalesce.ToString()));
                }
            }
        }

        // 2. Prohibido: Comparaciones directas de TenantId con Guid.Empty (ej. TenantId == Guid.Empty o TenantId != Guid.Empty)
        foreach (var binary in root.DescendantNodes().OfType<BinaryExpressionSyntax>())
        {
            if (binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression))
            {
                var leftText = binary.Left.ToString();
                var rightText = binary.Right.ToString();

                bool leftIsTenant = leftText.Contains("TenantId", StringComparison.OrdinalIgnoreCase);
                bool rightIsTenant = rightText.Contains("TenantId", StringComparison.OrdinalIgnoreCase);

                bool leftIsGuidEmpty = leftText.Contains("Guid.Empty", StringComparison.Ordinal);
                bool rightIsGuidEmpty = rightText.Contains("Guid.Empty", StringComparison.Ordinal);

                if ((leftIsTenant && rightIsGuidEmpty) || (rightIsTenant && leftIsGuidEmpty))
                {
                    var lineSpan = binary.GetLocation().GetLineSpan();
                    int line = lineSpan.StartLinePosition.Line + 1;
                    findings.Add(new PlatformScopeSentinelFinding(
                        filePath,
                        line,
                        "Comparación de TenantId con Guid.Empty. Debe usar PlatformScope.TenantId según ADR 0008 y ADR 0017.",
                        binary.ToString()));
                }
            }
        }

        return findings;
    }
}
