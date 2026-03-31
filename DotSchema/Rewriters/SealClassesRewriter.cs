using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotSchema.Rewriters;

/// <summary>
///     Rewriter that converts partial classes to sealed classes.
///     Always removes the <c>partial</c> modifier from all classes.
///     Does not add <c>sealed</c> to: base classes (inherited from), abstract classes, or static classes.
/// </summary>
internal sealed class SealClassesRewriter(IReadOnlySet<string> baseClasses) : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var visited = (ClassDeclarationSyntax) base.VisitClassDeclaration(node)!;

        // Always remove the partial modifier (generated code doesn't need it)
        var newModifiers = visited.Modifiers
                                  .Where(m => !m.IsKind(SyntaxKind.PartialKeyword))
                                  .ToList();

        // Determine if we should add sealed
        var shouldSeal = !baseClasses.Contains(visited.Identifier.Text)        // Don't seal base classes
                         && !visited.Modifiers.Any(SyntaxKind.AbstractKeyword) // sealed + abstract is invalid
                         && !visited.Modifiers.Any(SyntaxKind.StaticKeyword)   // sealed + static is invalid
                         && !visited.Modifiers.Any(SyntaxKind.SealedKeyword);  // Already sealed

        if (shouldSeal)
        {
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
        }

        return visited.WithModifiers(SyntaxFactory.TokenList(newModifiers));
    }
}
