using DotSchema.Analyzers;

using Microsoft.Extensions.Logging;

using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;

namespace DotSchema.Generators;

/// <summary>
///     Orchestrates the schema-to-C# code generation process.
/// </summary>
public static class SchemaGenerator
{
    /// <summary>
    ///     Runs the schema generation process with the given options.
    /// </summary>
    public static async Task<int> RunAsync(
        GenerateOptions options,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var generatedFiles = new List<string>();

        int result;

        if (options.Mode == GenerationMode.All)
        {
            result = await GenerateAllAsync(options, generatedFiles, logger, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            result = await GenerateAsync(options, generatedFiles, logger, cancellationToken).ConfigureAwait(false);
        }

        if (result != 0)
        {
            return result;
        }

        // Run JetBrains cleanup on all generated files at the end (if enabled)
        if (options.RunCleanup)
        {
            await JetBrainsCleanupRunner.RunAsync(generatedFiles, logger, cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Done!");

        return 0;
    }

    /// <summary>
    ///     Generates Shared{RootType}.cs + {Variant}{RootType}.cs for all variants.
    /// </summary>
    private static async Task<int> GenerateAllAsync(
        GenerateOptions options,
        List<string> generatedFiles,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var schemas = options.SchemaPaths;

        if (schemas.Count < 2)
        {
            logger.LogError(
                "All mode requires at least 2 schemas to detect shared vs variant-specific types, but {SchemaCount} provided",
                schemas.Count);

            return 1;
        }

        // Validate schema files exist
        foreach (var schemaPath in schemas)
        {
            if (!File.Exists(schemaPath))
            {
                logger.LogError("Schema file not found: {SchemaPath}", schemaPath);

                return 1;
            }
        }

        // Ensure output directory exists
        var outputDir = options.OutputPath;

        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // Extract root type name from the first schema's title
        var rootTypeName = await ExtractRootTypeNameAsync(schemas[0], cancellationToken).ConfigureAwait(false);

        // Extract variant names from schema filenames (e.g., "windows.config.schema.json" -> "Windows")
        var variants = schemas
                       .Select(Constants.ExtractVariantName)
                       .ToList();

        logger.LogInformation("Generating for variants: {Variants}", string.Join(", ", variants));

        // Generate I{RootType}.cs interface file (e.g., IConfig.cs) if enabled
        if (options.GenerateInterface)
        {
            var interfacePath = Path.Combine(outputDir, Constants.GetInterfaceFileName(rootTypeName));
            await GenerateInterfaceAsync(
                    interfacePath,
                    rootTypeName,
                    variants,
                    options.Namespace,
                    options.DryRun,
                    logger,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!options.DryRun)
            {
                generatedFiles.Add(interfacePath);
            }
        }

        // Generate shared types first (no variant - shared types shouldn't be variant-prefixed)
        var sharedOptions = options with
        {
            Mode = GenerationMode.Shared,
            Variant = string.Empty,
            Output = Path.Combine(outputDir, Constants.GetSharedFileName(rootTypeName))
        };

        var result = await GenerateAsync(sharedOptions, generatedFiles, logger, cancellationToken)
            .ConfigureAwait(false);

        if (result != 0)
        {
            return result;
        }

        // Generate variant-specific types for each variant
        foreach (var variant in variants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var variantOptions = options with
            {
                Mode = GenerationMode.Variant,
                Variant = variant,
                Output = Path.Combine(outputDir, Constants.GetVariantFileName(variant, rootTypeName))
            };

            result = await GenerateAsync(variantOptions, generatedFiles, logger, cancellationToken)
                .ConfigureAwait(false);

            if (result != 0)
            {
                return result;
            }
        }

        return 0;
    }

    /// <summary>
    ///     Extracts the root type name from a schema file's title field.
    /// </summary>
    private static async Task<string> ExtractRootTypeNameAsync(
        string schemaPath,
        CancellationToken cancellationToken)
    {
        var schemaJson = await File.ReadAllTextAsync(schemaPath, cancellationToken).ConfigureAwait(false);
        var schema = await JsonSchema.FromJsonAsync(schemaJson, cancellationToken).ConfigureAwait(false);

        return schema.Title ?? Constants.DefaultRootTypeName;
    }

    /// <summary>
    ///     Generates the I{RootType}.cs interface file that all variant types implement.
    /// </summary>
    private static async Task GenerateInterfaceAsync(
        string outputPath,
        string rootTypeName,
        List<string> variants,
        string targetNamespace,
        bool dryRun,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var interfaceName = Constants.GetInterfaceName(rootTypeName);
        var implementers = string.Join(", ", variants.Select(v => $"{v}{rootTypeName}"));

        var interfaceCode = $$"""
            namespace {{targetNamespace}};

            /// <summary>
            ///     Marker interface for all variant-specific {{rootTypeName.ToLowerInvariant()}} types.
            ///     Implemented by {{implementers}}.
            /// </summary>
            public interface {{interfaceName}};

            """;

        if (dryRun)
        {
            logger.LogInformation(
                "[DRY RUN] Would write {InterfaceName} interface to: {OutputPath}",
                interfaceName,
                outputPath);
        }
        else
        {
            logger.LogInformation("Writing {InterfaceName} interface to: {OutputPath}", interfaceName, outputPath);
            await File.WriteAllTextAsync(outputPath, interfaceCode, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Generates code without running cleanup. Adds output path to generatedFiles list.
    /// </summary>
    public static async Task<int> GenerateAsync(
        GenerateOptions options,
        List<string> generatedFiles,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        // Validate schema count for shared/variant modes
        var schemas = options.SchemaPaths;

        if (options.Mode is GenerationMode.Shared or GenerationMode.Variant && schemas.Count < 2)
        {
            logger.LogError(
                "Mode '{Mode}' requires at least 2 schemas to detect shared vs variant-specific types, but {SchemaCount} provided",
                options.Mode.ToString().ToLowerInvariant(),
                schemas.Count);

            return 1;
        }

        // Validate schema files exist
        foreach (var schemaPath in schemas)
        {
            if (!File.Exists(schemaPath))
            {
                logger.LogError("Schema file not found: {SchemaPath}", schemaPath);

                return 1;
            }
        }

        // Analyze schemas to detect shared vs variant-specific types
        var analyzer = new SchemaAnalyzer(logger);
        var analysisResult = await analyzer.AnalyzeAsync(schemas, options.Variant, cancellationToken)
                                           .ConfigureAwait(false);

        logger.LogInformation(
            "Detected {SharedCount} shared types, {ConflictingCount} conflicting types, {VariantCount} variant-specific types",
            analysisResult.SharedTypes.Count,
            analysisResult.ConflictingTypes.Count,
            analysisResult.VariantTypes.Count);

        // Generate code from the primary schema (already parsed by analyzer)
        logger.LogInformation(
            "Generating {Mode} types for {Variant} from: {SchemaPath}",
            options.Mode,
            options.Variant,
            analysisResult.PrimarySchemaPath);

        var settings = new CSharpGeneratorSettings
        {
            Namespace = options.Namespace,
            ClassStyle = CSharpClassStyle.Record,
            GenerateOptionalPropertiesAsNullable = true,
            GenerateNullableReferenceTypes = true,
            GenerateDataAnnotations = false,
            JsonLibrary = CSharpJsonLibrary.SystemTextJson,
            InlineNamedAny = true,
            ExcludedTypeNames = Constants.ExcludedTypeNames,
            PropertyNameGenerator = new PascalCasePropertyNameGenerator(),
            TypeNameGenerator = new CleanTypeNameGenerator(
                options.Variant,
                analysisResult.RootTypeName,
                analysisResult.ConflictingTypes)
        };

        var generator = new CSharpGenerator(analysisResult.PrimarySchema, settings);
        var code = generator.GenerateFile();

        // Post-process the generated code (type renaming is already done by CleanTypeNameGenerator)
        code = CodePostProcessor.Process(
            code,
            options.Mode,
            options.Variant,
            analysisResult.SharedTypes,
            analysisResult.VariantTypes,
            analysisResult.ConflictingTypes,
            analysisResult.RootTypeName,
            options.GenerateInterface);

        if (options.DryRun)
        {
            logger.LogInformation("[DRY RUN] Would write generated code to: {OutputPath}", options.OutputPath);
            logger.LogDebug(
                "Generated code preview:\n{Code}",
                code[..Math.Min(code.Length, 500)] + (code.Length > 500 ? "\n..." : ""));
        }
        else
        {
            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(options.OutputPath);

            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            logger.LogInformation("Writing generated code to: {OutputPath}", options.OutputPath);
            await File.WriteAllTextAsync(options.OutputPath, code, cancellationToken).ConfigureAwait(false);

            generatedFiles.Add(options.OutputPath);
        }

        return 0;
    }
}
