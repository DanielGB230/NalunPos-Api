using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pos.Architecture.Tests.Support;

public sealed record PlatformScopedEntityFinding(
    string FilePath,
    string TypeName,
    string Reason);

public static class PlatformScopedEntityScanner
{
    private static readonly HashSet<string> AllowedPlatformEntities = new(StringComparer.Ordinal)
    {
        "Role",
        "AuditLog"
    };

    public static IReadOnlyList<PlatformScopedEntityFinding> Scan(SyntaxNode root, string filePath = "")
    {
        var findings = new List<PlatformScopedEntityFinding>();

        var typeDeclarations = root.DescendantNodes()
            .OfType<BaseTypeDeclarationSyntax>()
            .Where(t => t is ClassDeclarationSyntax || t is RecordDeclarationSyntax)
            .ToList();

        foreach (var typeDecl in typeDeclarations)
        {
            var typeName = typeDecl.Identifier.ValueText;

            if (typeDecl.BaseList == null)
            {
                continue;
            }

            bool implementsPlatformScoped = false;
            foreach (var baseType in typeDecl.BaseList.Types)
            {
                var name = baseType.Type.ToString();
                if (name == "IPlatformScopedEntity" || name.EndsWith(".IPlatformScopedEntity", StringComparison.Ordinal))
                {
                    implementsPlatformScoped = true;
                    break;
                }
            }

            if (implementsPlatformScoped && !AllowedPlatformEntities.Contains(typeName))
            {
                findings.Add(new PlatformScopedEntityFinding(
                    filePath,
                    typeName,
                    $"El tipo '{typeName}' implementa 'IPlatformScopedEntity', pero solo 'Role' y 'AuditLog' están autorizados en la lista cerrada (ver ADR 0008 y ADR 0017)."));
            }
        }

        return findings;
    }
}
