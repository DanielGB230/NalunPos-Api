using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pos.Architecture.Tests.Support;

public sealed record HandlerRequiresTenantAttributeFinding(
    string FilePath,
    string HandlerTypeName,
    string AttributeName);

public static class HandlerRequiresTenantAttributeScanner
{
    public static IReadOnlyList<HandlerRequiresTenantAttributeFinding> Scan(SyntaxNode root, string filePath = "")
    {
        var findings = new List<HandlerRequiresTenantAttributeFinding>();

        foreach (var typeDecl in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            var typeName = typeDecl.Identifier.ValueText;
            if (!typeName.EndsWith("Handler", StringComparison.Ordinal))
            {
                continue;
            }

            var attributes = typeDecl.AttributeLists
                .SelectMany(al => al.Attributes)
                .ToList();

            foreach (var attr in attributes)
            {
                var attrName = attr.Name.ToString().Trim();
                if (attrName.EndsWith("Attribute", StringComparison.Ordinal))
                {
                    attrName = attrName[..^"Attribute".Length];
                }

                if (attrName.Equals("RequiresTenant", StringComparison.Ordinal) ||
                    attrName.EndsWith(".RequiresTenant", StringComparison.Ordinal) ||
                    attrName.Equals("AllowsNoTenant", StringComparison.Ordinal) ||
                    attrName.EndsWith(".AllowsNoTenant", StringComparison.Ordinal))
                {
                    findings.Add(new HandlerRequiresTenantAttributeFinding(
                        filePath,
                        typeName,
                        attr.ToString()));
                }
            }
        }

        return findings;
    }
}
