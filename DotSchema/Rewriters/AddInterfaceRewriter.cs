using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotSchema.Rewriters;

/// <summary>
///     Rewriter that adds an interface to a specific class by name.
///     Used to add marker interfaces (e.g., IConfig) to variant root types.
/// </summary>
internal sealed class AddInterfaceRewriter(string className, string interfaceName) : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var visited = (ClassDeclarationSyntax) base.VisitClassDeclaration(node)!;

        if (visited.Identifier.Text != className)
        {
            return visited;
        }

        // Create the interface type
        var interfaceType = SyntaxFactory.SimpleBaseType(SyntaxFactory.IdentifierName(interfaceName));

        // Add to base list
        if (visited.BaseList == null)
        {
            // No existing base list - create one with proper spacing
            var baseList = SyntaxFactory.BaseList(
                    SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(interfaceType))
                .WithColonToken(
                    SyntaxFactory.Token(SyntaxKind.ColonToken)
                                 .WithLeadingTrivia(SyntaxFactory.Space)
                                 .WithTrailingTrivia(SyntaxFactory.Space));

            return visited.WithBaseList(baseList);
        }

        // Existing base list - prepend interface
        var newTypes = visited.BaseList.Types.Insert(0, interfaceType);

        return visited.WithBaseList(visited.BaseList.WithTypes(newTypes));
    }
}

