using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotSchema.Rewriters;

/// <summary>
///     Rewriter that removes AdditionalProperties field and property from classes.
///     NJsonSchema generates these by default, but they add unnecessary complexity to DTOs.
///     Note: Associated attributes (e.g., [JsonExtensionData]) are implicitly removed
///     when the property is removed, as they are part of the property's syntax node.
/// </summary>
internal sealed class RemoveAdditionalPropertiesRewriter : CSharpSyntaxRewriter
{
    private const string AdditionalPropertiesFieldName = "_additionalProperties";
    private const string AdditionalPropertiesPropertyName = "AdditionalProperties";

    public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        // Remove _additionalProperties field
        if (node.Declaration.Variables.Any(v => v.Identifier.Text == AdditionalPropertiesFieldName))
        {
            return null;
        }

        return base.VisitFieldDeclaration(node);
    }

    public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        // Remove AdditionalProperties property
        if (node.Identifier.Text == AdditionalPropertiesPropertyName)
        {
            return null;
        }

        return base.VisitPropertyDeclaration(node);
    }
}

