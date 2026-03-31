using System.Text;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotSchema.Generators;

/// <summary>
///     Generates fluent builder extension methods for Config classes.
///     For each nullable property on a *Config class, generates an extension method
///     that constructs the metadata type and assigns it, returning the config for chaining.
/// </summary>
public static class FluentBuilderGenerator
{
    /// <summary>
    ///     Given the source code of a variant config file (e.g., WindowsConfig.cs) and
    ///     any companion source files (e.g., SharedConfig.cs), generates a companion
    ///     extensions file with fluent builder methods.
    /// </summary>
    /// <param name="variantCode">The variant config source code containing the *Config class.</param>
    /// <param name="companionSources">Additional source files that may define metadata types (e.g., SharedConfig.cs).</param>
    /// <param name="targetNamespace">The target namespace for the generated extensions.</param>
    public static string? Generate(string variantCode, IEnumerable<string> companionSources, string targetNamespace)
    {
        var tree = CSharpSyntaxTree.ParseText(variantCode);
        var root = tree.GetCompilationUnitRoot();

        // Parse companion sources to find metadata types defined elsewhere
        var allRoots = new List<CompilationUnitSyntax> { root };

        foreach (var source in companionSources)
        {
            var companionTree = CSharpSyntaxTree.ParseText(source);
            allRoots.Add(companionTree.GetCompilationUnitRoot());
        }

        // Find all classes ending with "Config" in the variant file
        var configClasses = root.DescendantNodes()
                                .OfType<ClassDeclarationSyntax>()
                                .Where(c => c.Identifier.Text.EndsWith("Config"))
                                .ToList();

        if (configClasses.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"namespace {targetNamespace};");
        sb.AppendLine();

        foreach (var configClass in configClasses)
        {
            var className = configClass.Identifier.Text;

            sb.AppendLine("/// <summary>");
            sb.AppendLine($"///     Fluent builder extension methods for <see cref=\"{className}\"/>.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine($"public static class {className}Extensions");
            sb.AppendLine("{");

            // Find all nullable properties with setters (the ones we converted from constructor params)
            var properties = configClass.Members
                                        .OfType<PropertyDeclarationSyntax>()
                                        .Where(p => p.Type is NullableTypeSyntax)
                                        .ToList();

            for (var i = 0; i < properties.Count; i++)
            {
                var prop = properties[i];
                var propName = prop.Identifier.Text;
                var nullableType = (NullableTypeSyntax) prop.Type;
                var metadataTypeName = nullableType.ElementType.ToString();

                // Find the metadata class across all source files
                var metadataClass = allRoots
                                    .SelectMany(r => r.DescendantNodes().OfType<ClassDeclarationSyntax>())
                                    .FirstOrDefault(c => c.Identifier.Text == metadataTypeName);

                if (metadataClass == null)
                {
                    GenerateSimpleMethod(sb, className, propName, metadataTypeName);
                }
                else
                {
                    var constructor = metadataClass.Members
                                                   .OfType<ConstructorDeclarationSyntax>()
                                                   .FirstOrDefault();

                    if (constructor == null || constructor.ParameterList.Parameters.Count == 0)
                    {
                        GenerateSimpleMethod(sb, className, propName, metadataTypeName);
                    }
                    else
                    {
                        GenerateMethodFromConstructor(sb, className, propName, metadataTypeName, constructor);
                    }
                }

                // Add blank line between methods, but not after the last one
                if (i < properties.Count - 1)
                {
                    sb.AppendLine();
                }
            }

            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private static void GenerateSimpleMethod(
        StringBuilder sb,
        string className,
        string propName,
        string metadataTypeName)
    {
        sb.AppendLine($"    /// <summary>Configures the {propName} collector.</summary>");
        sb.AppendLine($"    public static {className} {propName}(this {className} config, bool enabled = true)");
        sb.AppendLine("    {");
        sb.AppendLine($"        config.{propName} = new {metadataTypeName}(enabled);");
        sb.AppendLine("        return config;");
        sb.AppendLine("    }");
    }

    private static void GenerateMethodFromConstructor(
        StringBuilder sb,
        string className,
        string propName,
        string metadataTypeName,
        ConstructorDeclarationSyntax constructor)
    {
        var parameters = constructor.ParameterList.Parameters;

        // Build parameter list: put 'enabled' last with default true in the method signature,
        // but keep original order when calling the constructor.
        var enabledParam = parameters.FirstOrDefault(p => p.Identifier.ValueText == "enabled");
        var otherParams = parameters.Where(p => p.Identifier.ValueText != "enabled").ToList();

        // Method signature: other params first, then enabled with default
        var methodParams = new List<string>();

        foreach (var param in otherParams)
        {
            var paramType = param.Type?.ToString() ?? "object";
            var paramName = param.Identifier.ValueText;
            methodParams.Add($"{paramType} {paramName}");
        }

        if (enabledParam != null)
        {
            methodParams.Add("bool enabled = true");
        }

        // Constructor args: preserve original parameter order
        var ctorArgs = new List<string>();

        foreach (var param in parameters)
        {
            ctorArgs.Add(param.Identifier.ValueText);
        }

        var methodParamStr = string.Join(", ", methodParams);
        var ctorArgStr = string.Join(", ", ctorArgs);

        sb.AppendLine($"    /// <summary>Configures the {propName} collector.</summary>");
        sb.AppendLine($"    public static {className} {propName}(this {className} config, {methodParamStr})");
        sb.AppendLine("    {");
        sb.AppendLine($"        config.{propName} = new {metadataTypeName}({ctorArgStr});");
        sb.AppendLine("        return config;");
        sb.AppendLine("    }");
    }
}
