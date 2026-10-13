using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pos.Architecture.Tests.Support;

public sealed record HandlerRequiresTenantFinding(
    string FilePath,
    string HandlerTypeName,
    string CommandTypeName,
    string Reason);

public static class HandlerTenantRequirementScanner
{
    public static IReadOnlyList<HandlerRequiresTenantFinding> Scan(SyntaxNode root, string filePath = "")
    {
        var findings = new List<HandlerRequiresTenantFinding>();

        // Encontrar todos los tipos de clases que implementan ICommandHandler<TCommand, TResponse>
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();

        foreach (var classDecl in classDeclarations)
        {
            var handlerName = classDecl.Identifier.ValueText;

            // Verificar si el handler invoca RequireTenantId()
            bool callsRequireTenantId = classDecl.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(inv => inv.ToString().Contains("RequireTenantId", StringComparison.Ordinal));

            if (!callsRequireTenantId)
            {
                continue;
            }

            // Identificar el tipo del comando que maneja:
            // normalmente de la interfaz ICommandHandler<TCommand, TResponse> o ICommandHandler<TCommand>
            string? commandTypeName = null;
            if (classDecl.BaseList != null)
            {
                foreach (var baseType in classDecl.BaseList.Types)
                {
                    var baseTypeText = baseType.Type.ToString();
                    if (baseTypeText.StartsWith("ICommandHandler<", StringComparison.Ordinal))
                    {
                        if (baseType.Type is GenericNameSyntax genericName &&
                            genericName.TypeArgumentList.Arguments.Count > 0)
                        {
                            commandTypeName = genericName.TypeArgumentList.Arguments[0].ToString();
                            break;
                        }
                    }
                }
            }

            if (commandTypeName == null)
            {
                // Fallback heurístico: HandlerName sin "Handler"
                if (handlerName.EndsWith("Handler", StringComparison.Ordinal))
                {
                    commandTypeName = handlerName[..^"Handler".Length];
                }
            }

            if (commandTypeName == null)
            {
                continue;
            }

            // Buscar en el mismo archivo (o sintaxis provista) la declaración de commandTypeName
            var typeDeclarations = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
                .Where(t => t.Identifier.ValueText == commandTypeName)
                .ToList();

            foreach (var commandTypeDecl in typeDeclarations)
            {
                bool hasRequiresTenant = commandTypeDecl.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .Any(attr =>
                    {
                        var name = attr.Name.ToString().Trim();
                        if (name.EndsWith("Attribute", StringComparison.Ordinal))
                        {
                            name = name[..^"Attribute".Length];
                        }
                        return name.Equals("RequiresTenant", StringComparison.Ordinal) ||
                               name.EndsWith(".RequiresTenant", StringComparison.Ordinal);
                    });

                if (!hasRequiresTenant)
                {
                    findings.Add(new HandlerRequiresTenantFinding(
                        filePath,
                        handlerName,
                        commandTypeName,
                        $"El handler '{handlerName}' invoca RequireTenantId(), pero su comando '{commandTypeName}' no está anotado con [RequiresTenant]."));
                }
            }
        }

        return findings;
    }
}
