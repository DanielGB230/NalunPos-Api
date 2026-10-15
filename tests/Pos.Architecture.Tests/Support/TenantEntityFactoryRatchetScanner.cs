using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pos.Architecture.Tests.Support;

public sealed record TenantEntityFactoryFinding(
    string FilePath,
    string EntityName,
    string MethodSignature,
    int LineNumber,
    string Description);

public static class TenantEntityFactoryRatchetScanner
{
    public static readonly HashSet<string> PendingEntities = new(StringComparer.Ordinal)
    {
        "Branch",
        "CashRegister",
        "Category",
        "Customer",
        "Invoice",
        "Payment",
        "PosDevice",
        "Product",
        "Sale",
        "SaleLineItem",
        "Supplier",
        "SystemNotification"
    };

    public static IReadOnlyList<TenantEntityFactoryFinding> Scan(SyntaxNode root, string filePath = "")
    {
        var findings = new List<TenantEntityFactoryFinding>();

        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDecls)
        {
            if (classDecl.BaseList == null)
            {
                continue;
            }

            var baseTypes = classDecl.BaseList.Types
                .Select(t => t.Type.ToString())
                .ToList();

            bool isTenantOwned = baseTypes.Any(t => t.Equals("ITenantOwnedEntity", StringComparison.Ordinal) ||
                                                    t.EndsWith(".ITenantOwnedEntity", StringComparison.Ordinal));
            bool isPlatformScoped = baseTypes.Any(t => t.Equals("IPlatformScopedEntity", StringComparison.Ordinal) ||
                                                       t.EndsWith(".IPlatformScopedEntity", StringComparison.Ordinal));

            if (!isTenantOwned || isPlatformScoped)
            {
                continue;
            }

            var entityName = classDecl.Identifier.ValueText;

            var methods = classDecl.Members.OfType<MethodDeclarationSyntax>()
                .Where(m => m.Modifiers.Any(SyntaxKind.PublicKeyword) &&
                            m.Modifiers.Any(SyntaxKind.StaticKeyword) &&
                            m.Identifier.ValueText.StartsWith("Create", StringComparison.Ordinal))
                .ToList();

            foreach (var method in methods)
            {
                bool hasTenantIdParam = method.ParameterList.Parameters.Any(p =>
                {
                    var pType = p.Type?.ToString();
                    var pName = p.Identifier.ValueText;
                    return (pType == "Guid" || pType == "System.Guid") &&
                           pName.Equals("tenantId", StringComparison.OrdinalIgnoreCase);
                });

                if (!hasTenantIdParam)
                {
                    var lineSpan = method.GetLocation().GetLineSpan();
                    int line = lineSpan.StartLinePosition.Line + 1;
                    findings.Add(new TenantEntityFactoryFinding(
                        filePath,
                        entityName,
                        $"{method.Identifier.ValueText}({string.Join(", ", method.ParameterList.Parameters.Select(p => $"{p.Type} {p.Identifier.ValueText}"))})",
                        line,
                        $"Factoría estática '{method.Identifier.ValueText}' en entidad de tenant '{entityName}' no recibe parámetro 'Guid tenantId'."));
                }
            }
        }

        return findings;
    }
}
