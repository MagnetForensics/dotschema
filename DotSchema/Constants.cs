namespace DotSchema;

/// <summary>
///     Constants used throughout the schema generator.
/// </summary>
public static class Constants
{
    /// <summary>
    ///     Default root type name when schema title is not available.
    /// </summary>
    public const string DefaultRootTypeName = "Config";

    /// <summary>
    ///     Fallback type name when no name hint or title is available.
    /// </summary>
    public const string AnonymousTypeName = "Anonymous";

    /// <summary>
    ///     Prefix for generated interface names (e.g., "I" produces "IConfig" from "Config").
    /// </summary>
    public const string InterfacePrefix = "I";

    /// <summary>
    ///     Type names to exclude from generation.
    /// </summary>
    public static readonly string[] ExcludedTypeNames = ["AdditionalProperties"];

    /// <summary>
    ///     Conjunction keywords used in generated type names to combine multiple types.
    ///     These are detected and used to split compound type names.
    /// </summary>
    public static readonly string[] Conjunctions = ["_and_", "_or_", "_with_", "_of_"];

    /// <summary>
    ///     Generates the interface name from the root type name.
    ///     E.g., "Config" -> "IConfig", "Settings" -> "ISettings"
    /// </summary>
    public static string GetInterfaceName(string rootTypeName)
    {
        return $"{InterfacePrefix}{rootTypeName}";
    }

    /// <summary>
    ///     Generates the interface filename from the root type name.
    ///     E.g., "Config" -> "IConfig.cs", "Settings" -> "ISettings.cs"
    /// </summary>
    public static string GetInterfaceFileName(string rootTypeName)
    {
        return $"{InterfacePrefix}{rootTypeName}.cs";
    }

    /// <summary>
    ///     Generates the shared types filename from the root type name.
    ///     E.g., "Config" -> "SharedConfig.cs", "Settings" -> "SharedSettings.cs"
    /// </summary>
    public static string GetSharedFileName(string rootTypeName)
    {
        return $"Shared{rootTypeName}.cs";
    }

    /// <summary>
    ///     Generates the variant-specific filename.
    ///     E.g., ("Windows", "Config") -> "WindowsConfig.cs"
    /// </summary>
    public static string GetVariantFileName(string variant, string rootTypeName)
    {
        return $"{variant}{rootTypeName}.cs";
    }

    /// <summary>
    ///     Constants for JetBrains cleanup tool.
    /// </summary>
    public static class JetBrains
    {
        public const string DotnetExecutable = "dotnet";
        public const string CleanupProfile = "Built-in: Full Cleanup";
    }

    /// <summary>
    ///     Constants for file patterns.
    /// </summary>
    public static class FilePatterns
    {
        public const string SolutionPattern = "*.sln";
    }

    /// <summary>
    ///     Extracts a variant name from a schema filename.
    ///     Handles various patterns:
    ///     - "windows.schema.json" -> "Windows"
    ///     - "windows.config.schema.json" -> "Windows"
    ///     - "windows-config.json" -> "Windows"
    ///     - "WindowsConfig.json" -> "Windows"
    ///     - "my_config_windows.json" -> "Windows" (last segment before extension)
    /// </summary>
    public static string ExtractVariantName(string schemaPath)
    {
        var filename = Path.GetFileNameWithoutExtension(schemaPath);

        // Remove common suffixes like ".schema", ".config", etc.
        var suffixesToRemove = new[] { ".schema", ".config", "-schema", "-config", "_schema", "_config" };

        foreach (var suffix in suffixesToRemove)
        {
            if (filename.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                filename = filename[..^suffix.Length];
            }
        }

        // Split on common delimiters and take the first meaningful segment
        var delimiters = new[] { '.', '-', '_' };
        var parts = filename.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return filename;
        }

        // Take the first part as the variant name
        var variantName = parts[0];

        // Handle PascalCase filenames (e.g., "WindowsConfig" -> "Windows")
        // Only split on uppercase if the string is mixed case (not all uppercase)
        var isAllUpperOrLower = variantName.All(c => !char.IsLetter(c) || char.IsUpper(c))
                                || variantName.All(c => !char.IsLetter(c) || char.IsLower(c));

        if (!isAllUpperOrLower && variantName.Length > 1)
        {
            // Look for a capital letter after the first character (indicates PascalCase boundary)
            for (var i = 1; i < variantName.Length; i++)
            {
                if (char.IsUpper(variantName[i]))
                {
                    variantName = variantName[..i];

                    break;
                }
            }
        }

        // Normalize to PascalCase
        return ToPascalCase(variantName);
    }

    /// <summary>
    ///     Converts a string to PascalCase (first letter uppercase, rest lowercase).
    /// </summary>
    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        if (name.Length == 1)
        {
            return char.ToUpperInvariant(name[0]).ToString();
        }

        return char.ToUpperInvariant(name[0]) + name[1..].ToLowerInvariant();
    }
}
