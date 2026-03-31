using System.Text.Json;
using System.Text.Json.Serialization;

using DotSchema.Analyzers;

using Microsoft.Extensions.Logging;

using NJsonSchema;

namespace DotSchema.Generators;

/// <summary>
///     Generates a JSON manifest mapping schema fields to their resolved C# type names.
///     This is consumed by documentation generators to show accurate C# types without
///     parsing generated .cs files.
/// </summary>
public static class ManifestGenerator
{
    /// <summary>
    ///     Generates a manifest JSON file mapping each variant's root-level schema fields
    ///     to their resolved C# type names.
    /// </summary>
    public static async Task<int> RunAsync(
        GenerateOptions options,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var schemas = options.SchemaPaths;

        if (schemas.Count == 0)
        {
            logger.LogError("Manifest mode requires at least one schema");

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

        // Analyze schemas to get shared/conflicting type info
        var analyzer = new SchemaAnalyzer(logger);
        var inputs = schemas.Select(SchemaInput.FromFile).ToList();

        // Build the manifest: { variant -> { field -> csharp_type } }
        var manifest = new Dictionary<string, Dictionary<string, ManifestEntry>>();

        foreach (var schemaPath in schemas)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var variant = Constants.ExtractVariantName(schemaPath);
            var schemaJson = await File.ReadAllTextAsync(schemaPath, cancellationToken).ConfigureAwait(false);
            var schema = await JsonSchema.FromJsonAsync(schemaJson, cancellationToken).ConfigureAwait(false);
            var rootTypeName = schema.Title ?? Constants.DefaultRootTypeName;

            // Run analysis to get conflicting types (needed for correct name generation)
            var analysisResult = await analyzer.AnalyzeAsync(inputs, variant, cancellationToken)
                                               .ConfigureAwait(false);

            var typeNameGenerator = new CleanTypeNameGenerator(
                variant,
                rootTypeName,
                analysisResult.ConflictingTypes);

            var variantMapping = new Dictionary<string, ManifestEntry>();

            foreach (var (fieldName, fieldSchema) in schema.Properties)
            {
                var resolvedType = ResolveFieldType(fieldSchema, schema, typeNameGenerator);
                variantMapping[fieldName] = new ManifestEntry
                {
                    CSharpType = resolvedType,
                    Description = fieldSchema.Description
                                  ?? fieldSchema.ActualSchema?.Description
                                  ?? string.Empty
                };
            }

            manifest[variant.ToLowerInvariant()] = variantMapping;

            logger.LogInformation(
                "Manifest: {Variant} — {FieldCount} fields mapped",
                variant,
                variantMapping.Count);
        }

        // Write manifest
        var outputPath = options.OutputPath;

        // If output is a directory, write manifest.json inside it
        if (Directory.Exists(outputPath) || outputPath.EndsWith(Path.DirectorySeparatorChar))
        {
            Directory.CreateDirectory(outputPath);
            outputPath = Path.Combine(outputPath, "manifest.json");
        }

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        if (options.DryRun)
        {
            logger.LogInformation("[DRY RUN] Would write manifest to: {OutputPath}", outputPath);
            var preview = JsonSerializer.Serialize(manifest, jsonOptions);
            logger.LogDebug("Manifest preview:\n{Preview}", preview);
        }
        else
        {
            var dir = Path.GetDirectoryName(outputPath);

            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(manifest, jsonOptions);
            await File.WriteAllTextAsync(outputPath, json, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Wrote manifest to: {OutputPath}", outputPath);
        }

        return 0;
    }

    /// <summary>
    ///     Resolves a schema field's $ref chain to the final C# type name.
    /// </summary>
    private static string ResolveFieldType(
        JsonSchemaProperty fieldSchema,
        JsonSchema rootSchema,
        CleanTypeNameGenerator typeNameGenerator)
    {
        // Follow $ref to the actual definition
        var actual = fieldSchema.ActualSchema;

        // Find the definition name by matching the reference target
        if (fieldSchema.Reference != null)
        {
            var defName = FindDefinitionName(fieldSchema.Reference, rootSchema);

            if (defName != null)
            {
                return typeNameGenerator.Generate(fieldSchema.Reference, defName, []);
            }
        }

        // For inline schemas, use the field name as hint
        return typeNameGenerator.Generate(actual, actual.Title ?? fieldSchema.Name, []);
    }

    /// <summary>
    ///     Finds the definition key name for a referenced schema.
    /// </summary>
    private static string? FindDefinitionName(JsonSchema reference, JsonSchema rootSchema)
    {
        foreach (var (key, definition) in rootSchema.Definitions)
        {
            if (ReferenceEquals(definition, reference))
            {
                return key;
            }
        }

        return null;
    }
}

/// <summary>
///     A single entry in the manifest mapping.
/// </summary>
public sealed record ManifestEntry
{
    [JsonPropertyName("csharp_type")] public required string CSharpType { get; init; }

    [JsonPropertyName("description")] public string? Description { get; init; }
}
