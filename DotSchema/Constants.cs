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
}
