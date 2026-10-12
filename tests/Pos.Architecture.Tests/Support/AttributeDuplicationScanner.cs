using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pos.Architecture.Tests.Support;

public sealed record DuplicateAttributeFinding(
    string TypeName,
    string AttributeName,
    string NormalizedAttribute,
    int Count,
    BaseTypeDeclarationSyntax TypeDeclaration);

public static class AttributeDuplicationScanner
{
    public static IReadOnlyList<DuplicateAttributeFinding> Scan(SyntaxNode root)
    {
        var findings = new List<DuplicateAttributeFinding>();

        foreach (var typeDecl in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            var attributes = typeDecl.AttributeLists
                .SelectMany(al => al.Attributes)
                .ToList();

            if (attributes.Count < 2)
            {
                continue;
            }

            var duplicateGroups = attributes
                .GroupBy(NormalizeAttribute)
                .Where(g => g.Count() > 1);

            foreach (var group in duplicateGroups)
            {
                var sample = group.First();
                findings.Add(new DuplicateAttributeFinding(
                    typeDecl.Identifier.ValueText,
                    sample.Name.ToString().Trim(),
                    group.Key,
                    group.Count(),
                    typeDecl));
            }
        }

        return findings;
    }

    public static string NormalizeAttribute(AttributeSyntax attr)
    {
        var name = attr.Name.ToString().Trim();
        if (name.EndsWith("Attribute", StringComparison.Ordinal))
        {
            name = name[..^"Attribute".Length];
        }

        if (attr.ArgumentList is null || attr.ArgumentList.Arguments.Count == 0)
        {
            return name;
        }

        var args = attr.ArgumentList.Arguments
            .Select(a => string.Concat(a.ToString().Where(c => !char.IsWhiteSpace(c))));

        return $"{name}({string.Join(",", args)})";
    }
}
