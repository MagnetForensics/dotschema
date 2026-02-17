using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotSchema.Rewriters;

/// <summary>
///     Helper methods for working with Roslyn syntax nodes.
/// </summary>
internal static class SyntaxHelpers
{
    /// <summary>
    ///     Extracts the type name from a base type syntax node.
    ///     Handles simple names (BaseClass), generic names (BaseClass&lt;T&gt;),
    ///     and qualified names (Namespace.BaseClass).
    /// </summary>
    public static string? GetBaseTypeName(TypeSyntax type)
    {
        return type switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            GenericNameSyntax gen => gen.Identifier.Text,
            QualifiedNameSyntax qualified => GetBaseTypeName(qualified.Right),
            AliasQualifiedNameSyntax alias => GetBaseTypeName(alias.Name),
            _ => null
        };
    }

    /// <summary>
    ///     Gets all base class names from a compilation unit.
    ///     These are classes that other classes inherit from.
    /// </summary>
    public static HashSet<string> GetBaseClassNames(CompilationUnitSyntax root)
    {
        return root.DescendantNodes()
                   .OfType<ClassDeclarationSyntax>()
                   .Where(c => c.BaseList != null)
                   .SelectMany(c => c.BaseList!.Types)
                   .Select(t => GetBaseTypeName(t.Type))
                   .OfType<string>()
                   .ToHashSet();
    }
}

