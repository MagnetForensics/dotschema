using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotSchema.Rewriters;

/// <summary>
///     Rewriter that converts partial classes to sealed classes.
///     Base classes (those inherited from) are preserved as non-sealed to allow inheritance.
/// </summary>
internal sealed class SealClassesRewriter(IReadOnlySet<string> baseClasses) : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var visited = (ClassDeclarationSyntax) base.VisitClassDeclaration(node)!;

        // Don't seal base classes
        if (baseClasses.Contains(visited.Identifier.Text))
        {
            return visited;
        }

        // Check if already sealed
        if (visited.Modifiers.Any(SyntaxKind.SealedKeyword))
        {
            return visited;
        }

        // Remove partial modifier and add sealed
        var newModifiers = visited.Modifiers
                                  .Where(m => !m.IsKind(SyntaxKind.PartialKeyword))
                                  .ToList();

        // Find position to insert sealed (after public/internal/etc.)
        var insertIndex = 0;

        for (var i = 0; i < newModifiers.Count; i++)
        {
            if (newModifiers[i].IsKind(SyntaxKind.PublicKeyword)
                || newModifiers[i].IsKind(SyntaxKind.InternalKeyword)
                || newModifiers[i].IsKind(SyntaxKind.PrivateKeyword)
                || newModifiers[i].IsKind(SyntaxKind.ProtectedKeyword))
            {
                insertIndex = i + 1;
            }
        }

        // Create sealed keyword with proper trivia (space before the class keyword)
        var sealedKeyword = SyntaxFactory.Token(SyntaxKind.SealedKeyword)
                                         .WithTrailingTrivia(SyntaxFactory.Space);

        newModifiers.Insert(insertIndex, sealedKeyword);

        return visited.WithModifiers(SyntaxFactory.TokenList(newModifiers));
    }
}

