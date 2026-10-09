using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pos.Architecture.Tests.Support;

public sealed record MathPaginationViolation(string FilePath, int LineNumber, string StatementText);

public static class PaginationSyntaxScanner
{
    public static bool HasDisallowedMathPaginationInvocation(SyntaxNode root)
    {
        return FindMathPaginationInvocations(root).Count > 0;
    }

    public static IReadOnlyList<MathPaginationViolation> FindMathPaginationInvocations(SyntaxNode root, string filePath = "")
    {
        var violations = new List<MathPaginationViolation>();
        var hasStaticUsingMath = HasStaticUsingMath(root);

        foreach (var node in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (!IsMathMinMaxClamp(node, hasStaticUsingMath))
                continue;

            if (HasPageSizeOrNumberArgument(node.ArgumentList))
            {
                var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                violations.Add(new MathPaginationViolation(filePath, line, node.ToString()));
            }
        }

        return violations;
    }

    public static bool HasMaxPageSizeIdentifierToken(SyntaxNode root)
    {
        return root.DescendantTokens()
            .Any(t => t.IsKind(SyntaxKind.IdentifierToken) && t.ValueText == "MaxPageSize");
    }

    private static bool HasStaticUsingMath(SyntaxNode root)
    {
        return root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Any(u => u.StaticKeyword.IsKind(SyntaxKind.StaticKeyword) &&
                      (u.Name?.ToString() == "System.Math" || u.Name?.ToString() == "Math"));
    }

    private static bool IsMathMinMaxClamp(InvocationExpressionSyntax node, bool hasStaticUsingMath)
    {
        var methodName = "";
        var isMathTarget = false;

        if (node.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            methodName = memberAccess.Name.Identifier.ValueText;
            var targetText = memberAccess.Expression.ToString().Trim();
            isMathTarget = targetText == "Math" || targetText == "System.Math";
        }
        else if (node.Expression is IdentifierNameSyntax identifier)
        {
            methodName = identifier.Identifier.ValueText;
            isMathTarget = hasStaticUsingMath;
        }

        if (!isMathTarget)
            return false;

        return methodName is "Min" or "Max" or "Clamp";
    }

    private static bool HasPageSizeOrNumberArgument(ArgumentListSyntax argumentList)
    {
        if (argumentList == null)
            return false;

        return argumentList.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Any(id => id.Identifier.ValueText is "PageSize" or "PageNumber");
    }
}
