using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotSchema.Rewriters;

/// <summary>
///     Rewriter that converts constructor parameters into nullable init-only properties
///     for classes whose names end with "Config".
///     This allows consumers to use object initializer syntax instead of massive positional constructors,
///     and means new collectors can be added without breaking existing consumer code.
/// </summary>
internal sealed class OptionalPropertiesToInitRewriter : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var visited = (ClassDeclarationSyntax) base.VisitClassDeclaration(node)!;

        // Only apply to classes whose names end with "Config"
        if (!visited.Identifier.Text.EndsWith("Config"))
        {
            return visited;
        }

        // Find the constructor
        var constructor = visited.Members
                                 .OfType<ConstructorDeclarationSyntax>()
                                 .FirstOrDefault();

        if (constructor?.ParameterList.Parameters.Count is null or 0)
        {
            return visited;
        }

        // Collect all parameter names — they all become optional
        var optionalParams = new HashSet<string>();

        foreach (var param in constructor.ParameterList.Parameters)
        {
            optionalParams.Add(param.Identifier.Text.TrimStart('@'));
        }

        // Remove the constructor entirely — all properties become init-only
        var newMembers = new SyntaxList<MemberDeclarationSyntax>();

        foreach (var member in visited.Members)
        {
            if (member is ConstructorDeclarationSyntax)
            {
                // Skip the constructor
                continue;
            }

            if (member is PropertyDeclarationSyntax property && property.AccessorList != null)
            {
                // Check if this property corresponds to a constructor parameter
                var propName = property.Identifier.Text;
                var matchesParam = optionalParams.Any(p => string.Equals(
                                                          p,
                                                          propName,
                                                          StringComparison.OrdinalIgnoreCase));

                if (matchesParam)
                {
                    // Make the type nullable if it isn't already
                    var propertyType = property.Type;

                    if (propertyType is not NullableTypeSyntax)
                    {
                        propertyType = SyntaxFactory.NullableType(propertyType);
                    }

                    // Build get; set; accessor list
                    var getAccessor = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                                   .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

                    var initAccessor = SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                                                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

                    var newAccessorList = SyntaxFactory.AccessorList(
                        SyntaxFactory.List(new[] { getAccessor, initAccessor }));

                    var newProperty = property
                                      .WithType(propertyType)
                                      .WithAccessorList(newAccessorList)
                                      .WithInitializer(
                                          SyntaxFactory.EqualsValueClause(
                                              SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)))
                                      .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

                    newMembers = newMembers.Add(newProperty);

                    continue;
                }
            }

            newMembers = newMembers.Add(member);
        }

        return visited.WithMembers(newMembers);
    }
}
