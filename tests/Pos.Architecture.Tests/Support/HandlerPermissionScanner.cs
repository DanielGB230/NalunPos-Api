using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pos.Architecture.Tests.Support;

public sealed record HandlerWithPermissionFinding(
    string TypeName,
    string AttributeName,
    BaseTypeDeclarationSyntax TypeDeclaration);

public static class HandlerPermissionScanner
{
    public static IReadOnlyList<HandlerWithPermissionFinding> Scan(SyntaxNode root)
    {
        var findings = new List<HandlerWithPermissionFinding>();

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
                var normalized = AttributeDuplicationScanner.NormalizeAttribute(attr);
                var attrName = attr.Name.ToString().Trim();
                if (attrName.EndsWith("Attribute", StringComparison.Ordinal))
                {
                    attrName = attrName[..^"Attribute".Length];
                }

                if (attrName.Equals("HasPermission", StringComparison.Ordinal) ||
                    attrName.EndsWith(".HasPermission", StringComparison.Ordinal))
                {
                    findings.Add(new HandlerWithPermissionFinding(typeName, normalized, typeDecl));
                }
            }
        }

        return findings;
    }
}
