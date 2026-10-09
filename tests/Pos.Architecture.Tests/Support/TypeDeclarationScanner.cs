using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pos.Architecture.Tests.Support;

public static class TypeDeclarationScanner
{
    public static IReadOnlyList<DeclaredTypeInfo> ScanDeclaredTypes(SyntaxNode root)
    {
        var types = new List<DeclaredTypeInfo>();

        foreach (var node in root.DescendantNodes())
        {
            if (node is BaseTypeDeclarationSyntax typeDecl)
            {
                var kind = typeDecl switch
                {
                    ClassDeclarationSyntax => "class",
                    StructDeclarationSyntax => "struct",
                    InterfaceDeclarationSyntax => "interface",
                    EnumDeclarationSyntax => "enum",
                    RecordDeclarationSyntax recordDecl when recordDecl.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) => "record struct",
                    RecordDeclarationSyntax => "record",
                    _ => "type"
                };
                types.Add(new DeclaredTypeInfo(typeDecl.Identifier.ValueText, kind, typeDecl));
            }
            else if (node is DelegateDeclarationSyntax delegateDecl)
            {
                types.Add(new DeclaredTypeInfo(delegateDecl.Identifier.ValueText, "delegate", delegateDecl));
            }
        }

        return types;
    }
}
