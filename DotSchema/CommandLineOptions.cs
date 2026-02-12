using CommandLine;

namespace DotSchema;

/// <summary>
///     Command line options for the schema generator.
/// </summary>
public sealed record GenerateOptions
{
    [Option(
        'm',
        "mode",
        Default = GenerationMode.All,
        HelpText = "Generation mode: All (all types), Shared (types in all schemas), Variant (variant-specific).")]
    public GenerationMode Mode { get; init; } = GenerationMode.All;

    [Option(
        'v',
        "variant",
        Default = "",
        HelpText = "Variant name for single-variant generation (derived from schema filename if not specified).")]
    public string Variant { get; init; } = string.Empty;

    [Option(
        's',
        "schemas",
        Required = true,
        Separator = ' ',
        HelpText = "One or more JSON schema files to process.")]
    public required IEnumerable<string> Schemas { get; init; }

    [Option(
        'o',
        "output",
        Required = true,
        HelpText = "Output C# file path (for Shared/Variant modes) or directory (for All mode).")]
    public required string Output { get; init; }

    [Option(
        'n',
        "namespace",
        Required = true,
        HelpText = "Namespace for generated types (e.g., MyCompany.Config).")]
    public required string Namespace { get; init; }

    [Option(
        "no-interface",
        Default = false,
        HelpText = "Skip generating the marker interface (e.g., IConfig) that all variant types implement.")]
    public bool NoInterface { get; init; } = false;

    [Option(
        "no-cleanup",
        Default = false,
        HelpText = "Skip running JetBrains code cleanup on generated files.")]
    public bool NoCleanup { get; init; } = false;

    /// <summary>
    ///     Gets whether to generate the marker interface.
    /// </summary>
    public bool GenerateInterface => !NoInterface;

    /// <summary>
    ///     Gets whether to run JetBrains cleanup.
    /// </summary>
    public bool RunCleanup => !NoCleanup;

    /// <summary>
    ///     Gets the schema file paths as a list.
    /// </summary>
    public List<string> SchemaPaths => Schemas.ToList();

    /// <summary>
    ///     Gets the output file path or directory.
    /// </summary>
    public string OutputPath => Output;
}

/// <summary>
///     Code generation mode.
/// </summary>
public enum GenerationMode
{
    /// <summary>Generate Shared{RootType}.cs + {Variant}{RootType}.cs for all variants.</summary>
    All,

    /// <summary>Generate only types that exist in ALL provided schemas.</summary>
    Shared,

    /// <summary>Generate only types unique to the specified variant.</summary>
    Variant
}
