using NJsonSchema;
using NJsonSchema.CodeGeneration;

namespace DotSchema.Generators;

/// <summary>
///     Converts snake_case property names to PascalCase.
/// </summary>
public sealed class PascalCasePropertyNameGenerator : IPropertyNameGenerator
{
    public string Generate(JsonSchemaProperty property)
    {
        return ToPascalCase(property.Name);
    }

    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var parts = name.Split('_');

        return string.Concat(
            parts.Select(p => string.IsNullOrEmpty(p)
                             ? ""
                             : char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p[1..] : "")));
    }
}
