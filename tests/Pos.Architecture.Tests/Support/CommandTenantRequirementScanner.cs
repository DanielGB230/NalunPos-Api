using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pos.Architecture.Tests.Support;

public sealed record CommandTenantRequirementFinding(
    string FilePath,
    string CommandTypeName,
    string Reason);

public static class CommandTenantRequirementScanner
{
    public static IReadOnlyList<CommandTenantRequirementFinding> Scan(SyntaxNode root, string filePath = "")
    {
        var findings = new List<CommandTenantRequirementFinding>();

        var typeDeclarations = root.DescendantNodes()
            .OfType<BaseTypeDeclarationSyntax>()
            .Where(t => t is ClassDeclarationSyntax || t is RecordDeclarationSyntax)
            .ToList();

        foreach (var typeDecl in typeDeclarations)
        {
            var typeName = typeDecl.Identifier.ValueText;

            // Check if implements ICommand or ICommand<T>
            bool isCommand = false;
            if (typeDecl.BaseList != null)
            {
                foreach (var baseType in typeDecl.BaseList.Types)
                {
                    var baseTypeText = baseType.Type.ToString();
                    if (baseTypeText == "ICommand" ||
                        baseTypeText.StartsWith("ICommand<", StringComparison.Ordinal) ||
                        baseTypeText.EndsWith(".ICommand", StringComparison.Ordinal) ||
                        baseTypeText.Contains(".ICommand<", StringComparison.Ordinal))
                    {
                        isCommand = true;
                        break;
                    }
                }
            }

            if (!isCommand)
            {
                continue;
            }

            var attributes = typeDecl.AttributeLists
                .SelectMany(al => al.Attributes)
                .Select(attr =>
                {
                    var name = attr.Name.ToString().Trim();
                    if (name.EndsWith("Attribute", StringComparison.Ordinal))
                    {
                        name = name[..^"Attribute".Length];
                    }
                    if (name.Contains('.'))
                    {
                        name = name[(name.LastIndexOf('.') + 1)..];
                    }
                    return name;
                })
                .ToList();

            bool hasRequiresTenant = attributes.Contains("RequiresTenant");
            bool hasAllowsNoTenant = attributes.Contains("AllowsNoTenant");

            if (!hasRequiresTenant && !hasAllowsNoTenant)
            {
                findings.Add(new CommandTenantRequirementFinding(
                    filePath,
                    typeName,
                    $"El comando '{typeName}' no declara ni [RequiresTenant] ni [AllowsNoTenant] (debe declarar exactamente uno)."));
            }
            else if (hasRequiresTenant && hasAllowsNoTenant)
            {
                findings.Add(new CommandTenantRequirementFinding(
                    filePath,
                    typeName,
                    $"El comando '{typeName}' declara tanto [RequiresTenant] como [AllowsNoTenant] (ambos a la vez está prohibido)."));
            }
        }

        return findings;
    }
}
