using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pos.Architecture.Tests.Support;

public sealed record SystemClockFinding(
    string FilePath,
    string MemberAccess,
    int LineNumber,
    string LineSnippet);

public static class SystemClockRatchetScanner
{
    /// <summary>
    /// Lista ratchet explícita sellada en 0 (ADR 0018).
    /// </summary>
    public static readonly HashSet<string> AllowedDomainEntitiesRatchet = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<SystemClockFinding> Scan(SyntaxNode root, string filePath = "")
    {
        var findings = new List<SystemClockFinding>();

        // 1. Prohibir DateTime.UtcNow/Now, DateTimeOffset.UtcNow/Now y TimeProvider.System en MemberAccess
        var memberAccessExpressions = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>();

        foreach (var memberAccess in memberAccessExpressions)
        {
            var expressionText = memberAccess.Expression.ToString();
            var nameText = memberAccess.Name.Identifier.ValueText;

            bool isProhibitedAccess = false;

            if ((expressionText == "DateTime" || expressionText == "System.DateTime") &&
                (nameText == "UtcNow" || nameText == "Now"))
            {
                isProhibitedAccess = true;
            }
            else if ((expressionText == "DateTimeOffset" || expressionText == "System.DateTimeOffset") &&
                     (nameText == "UtcNow" || nameText == "Now"))
            {
                isProhibitedAccess = true;
            }
            else if ((expressionText == "TimeProvider" || expressionText == "System.TimeProvider") &&
                     nameText == "System")
            {
                // Permitido exclusivamente en el registro de DI en DependencyInjection.cs de Pos.Application y Pos.Infrastructure
                var normalizedPath = filePath.Replace('\\', '/');
                bool isAuthorizedDiRegistration =
                    normalizedPath.EndsWith("src/Pos.Application/DependencyInjection.cs", StringComparison.OrdinalIgnoreCase) ||
                    normalizedPath.EndsWith("src/Pos.Infrastructure/DependencyInjection.cs", StringComparison.OrdinalIgnoreCase);

                if (!isAuthorizedDiRegistration)
                {
                    isProhibitedAccess = true;
                }
            }

            if (isProhibitedAccess)
            {
                var lineSpan = memberAccess.GetLocation().GetLineSpan();
                int lineNumber = lineSpan.StartLinePosition.Line + 1;
                string snippet = memberAccess.ToString();

                findings.Add(new SystemClockFinding(filePath, $"{expressionText}.{nameText}", lineNumber, snippet));
            }
        }

        // 2. Prohibir parámetros TimeProvider? con valor por defecto (= null o = default)
        var parameters = root.DescendantNodes().OfType<ParameterSyntax>();
        foreach (var parameter in parameters)
        {
            var typeText = parameter.Type?.ToString();
            if (typeText != null &&
                (typeText == "TimeProvider?" ||
                 typeText == "System.TimeProvider?" ||
                 typeText == "Nullable<TimeProvider>" ||
                 typeText == "Nullable<System.TimeProvider>"))
            {
                if (parameter.Default != null)
                {
                    var lineSpan = parameter.GetLocation().GetLineSpan();
                    int lineNumber = lineSpan.StartLinePosition.Line + 1;
                    findings.Add(new SystemClockFinding(filePath, "TimeProvider? with default value", lineNumber, parameter.ToString()));
                }
            }
        }

        // 3. Prohibir fallbacks silenciosos con operador de coalescencia nula: ?? TimeProvider.System
        var binaryExpressions = root.DescendantNodes().OfType<BinaryExpressionSyntax>()
            .Where(b => b.IsKind(SyntaxKind.CoalesceExpression));
        foreach (var binary in binaryExpressions)
        {
            var rightText = binary.Right.ToString();
            if (rightText == "TimeProvider.System" || rightText == "System.TimeProvider.System")
            {
                var lineSpan = binary.GetLocation().GetLineSpan();
                int lineNumber = lineSpan.StartLinePosition.Line + 1;
                findings.Add(new SystemClockFinding(filePath, "?? TimeProvider.System", lineNumber, binary.ToString()));
            }
        }

        return findings;
    }
}
