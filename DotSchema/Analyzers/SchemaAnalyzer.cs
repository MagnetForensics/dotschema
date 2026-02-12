using System.Security.Cryptography;
using System.Text;

using DotSchema.Generators;

using Microsoft.Extensions.Logging;

using NJsonSchema;

namespace DotSchema.Analyzers;

/// <summary>
///     Result of schema analysis containing shared and variant-specific types.
/// </summary>
/// <param name="SharedTypes">Types that exist in all schemas with identical content.</param>
/// <param name="VariantTypes">Types unique to the current variant.</param>
/// <param name="ConflictingTypes">Types that exist in multiple schemas but with different content (need variant prefix).</param>
/// <param name="PrimarySchemaPath">Path to the primary schema for the current variant.</param>
/// <param name="PrimarySchema">The parsed primary schema (avoids re-parsing).</param>
/// <param name="RootTypeName">The root type name extracted from the schema's title field.</param>
public sealed record SchemaAnalysisResult(
    HashSet<string> SharedTypes,
    HashSet<string> VariantTypes,
    HashSet<string> ConflictingTypes,
    string PrimarySchemaPath,
    JsonSchema PrimarySchema,
    string RootTypeName);

/// <summary>
///     Analyzes JSON schemas to determine shared vs variant-specific types.
///     Types are considered shared only if they have the same name AND same content hash.
/// </summary>
public sealed class SchemaAnalyzer
{
    private readonly ILogger _logger;
    private readonly CleanTypeNameGenerator _typeNameGenerator = new();

    public SchemaAnalyzer(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Analyzes multiple schemas to determine which types are shared (exist in all with identical content)
    ///     vs variant-specific (exist in only some schemas or have different content).
    /// </summary>
    public async Task<SchemaAnalysisResult> AnalyzeAsync(
        List<string> schemaPaths,
        string currentVariant,
        CancellationToken cancellationToken = default)
    {
        // Determine primary schema path
        var primarySchemaPath = DeterminePrimarySchemaPath(schemaPaths, currentVariant);

        // Parse all schemas and extract type hashes
        var (parsedSchemas, allSchemaTypes) = await ParseSchemasAsync(schemaPaths, cancellationToken);

        // Extract root type name from the first schema's title
        var rootTypeName = parsedSchemas.Values.FirstOrDefault()?.Title ?? Constants.DefaultRootTypeName;

        // Get the primary schema (already parsed)
        var primarySchema = parsedSchemas[primarySchemaPath];

        // With only one schema, we can't determine what's shared
        if (schemaPaths.Count < 2)
        {
            return new SchemaAnalysisResult([], [], [], primarySchemaPath, primarySchema, rootTypeName);
        }

        // Categorize types as shared or conflicting
        var (sharedTypes, conflictingTypes) = CategorizeTypes(allSchemaTypes, rootTypeName);

        // Variant-specific types = types in current variant schema that aren't shared or conflicting
        var variantTypes = DetermineVariantTypes(
            schemaPaths,
            currentVariant,
            parsedSchemas,
            sharedTypes,
            conflictingTypes,
            rootTypeName);

        return new SchemaAnalysisResult(sharedTypes, variantTypes, conflictingTypes, primarySchemaPath, primarySchema, rootTypeName);
    }

    /// <summary>
    ///     Determines the primary schema path based on the current variant.
    /// </summary>
    private static string DeterminePrimarySchemaPath(List<string> schemaPaths, string currentVariant)
    {
        if (string.IsNullOrEmpty(currentVariant))
        {
            return schemaPaths[0];
        }

        var variantSchema = schemaPaths.FirstOrDefault(p => Path.GetFileName(p)
                                                                .Contains(
                                                                    currentVariant,
                                                                    StringComparison.OrdinalIgnoreCase));

        return variantSchema ?? schemaPaths[0];
    }

    /// <summary>
    ///     Parses all schemas and extracts type hashes.
    /// </summary>
    private async Task<(Dictionary<string, JsonSchema> parsedSchemas, List<Dictionary<string, string>> allSchemaTypes)>
        ParseSchemasAsync(List<string> schemaPaths, CancellationToken cancellationToken)
    {
        var parsedSchemas = new Dictionary<string, JsonSchema>();
        var allSchemaTypes = new List<Dictionary<string, string>>();

        foreach (var schemaPath in schemaPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var schemaJson = await File.ReadAllTextAsync(schemaPath, cancellationToken);
            var schema = await JsonSchema.FromJsonAsync(schemaJson, cancellationToken);
            parsedSchemas[schemaPath] = schema;

            var types = ExtractTypeHashes(schema);
            allSchemaTypes.Add(types);

            _logger.LogDebug("  {SchemaFile}: {TypeCount} types", Path.GetFileName(schemaPath), types.Count);
        }

        return (parsedSchemas, allSchemaTypes);
    }

    /// <summary>
    ///     Categorizes types as shared (identical in 2+ schemas) or conflicting (different content in 2+ schemas).
    /// </summary>
    private static (HashSet<string> sharedTypes, HashSet<string> conflictingTypes) CategorizeTypes(
        List<Dictionary<string, string>> allSchemaTypes,
        string rootTypeName)
    {
        var allTypeNames = allSchemaTypes.SelectMany(s => s.Keys).ToHashSet();
        var sharedTypes = new HashSet<string>();
        var conflictingTypes = new HashSet<string>();

        foreach (var typeName in allTypeNames)
        {
            var hashesForType = allSchemaTypes
                                .Where(s => s.ContainsKey(typeName))
                                .Select(s => s[typeName])
                                .ToList();

            var distinctHashCount = hashesForType.Distinct().Count();

            switch (hashesForType.Count)
            {
                // Type exists in 2+ schemas with identical content = shared
                case > 1 when distinctHashCount == 1:
                    sharedTypes.Add(typeName);

                    break;

                // Type exists in 2+ schemas with different content = conflicting (needs variant prefix)
                case > 1 when distinctHashCount > 1:
                    conflictingTypes.Add(typeName);

                    break;

                // Type exists in only 1 schema = truly variant-specific (no action needed here)
            }
        }

        // Always exclude the root type from shared (it gets renamed per-variant)
        sharedTypes.Remove(rootTypeName);
        conflictingTypes.Remove(rootTypeName);

        return (sharedTypes, conflictingTypes);
    }

    /// <summary>
    ///     Determines variant-specific types (types not shared or conflicting).
    /// </summary>
    private HashSet<string> DetermineVariantTypes(
        List<string> schemaPaths,
        string currentVariant,
        Dictionary<string, JsonSchema> parsedSchemas,
        HashSet<string> sharedTypes,
        HashSet<string> conflictingTypes,
        string rootTypeName)
    {
        var currentVariantSchemaPath = schemaPaths.FirstOrDefault(p => Path.GetFileName(p)
                                                                           .Contains(
                                                                               currentVariant,
                                                                               StringComparison.OrdinalIgnoreCase))
                                       ?? schemaPaths[0];

        var currentTypes = ExtractTypeHashes(parsedSchemas[currentVariantSchemaPath]);

        var variantTypes = new HashSet<string>(currentTypes.Keys);
        variantTypes.ExceptWith(sharedTypes);
        variantTypes.ExceptWith(conflictingTypes);
        variantTypes.Remove(rootTypeName); // Root type is handled separately

        return variantTypes;
    }

    /// <summary>
    ///     Extracts all type names and their content hashes from a JSON schema.
    /// </summary>
    private Dictionary<string, string> ExtractTypeHashes(JsonSchema schema)
    {
        var types = new Dictionary<string, string>();

        // Add root type
        var rootName = _typeNameGenerator.Generate(schema, schema.Title, []);
        types[rootName] = ComputeSchemaHash(schema);

        // Add all definition types
        foreach (var (key, definition) in schema.Definitions)
        {
            var typeName = _typeNameGenerator.Generate(definition, key, []);
            types[typeName] = ComputeSchemaHash(definition);
        }

        // Also scan for inline types that NJsonSchema generates from property names
        ExtractInlineTypeHashes(schema, types);

        return types;
    }

    /// <summary>
    ///     Computes a hash of the schema's content for comparison.
    ///     Uses a custom serialization approach since ToJson() fails on sub-schemas with unresolved references.
    /// </summary>
    private static string ComputeSchemaHash(JsonSchema schema)
    {
        // Build a normalized representation of the schema's structure
        var sb = new StringBuilder();
        AppendSchemaFingerprint(schema, sb);

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }

    /// <summary>
    ///     Builds a normalized string representation of a schema for hashing.
    ///     This captures the structural aspects that matter for type equivalence.
    /// </summary>
    private static void AppendSchemaFingerprint(JsonSchema schema, StringBuilder sb)
    {
        sb.Append($"type:{schema.Type};");

        // Properties (sorted by name for consistency)
        if (schema.Properties.Count > 0)
        {
            sb.Append("props:[");

            foreach (var (name, prop) in schema.Properties.OrderBy(p => p.Key))
            {
                sb.Append($"{name}:");
                AppendPropertyFingerprint(prop, sb);
                sb.Append(',');
            }

            sb.Append(']');
        }

        // Required properties
        if (schema.RequiredProperties.Count > 0)
        {
            sb.Append($"req:[{string.Join(",", schema.RequiredProperties.OrderBy(r => r))}];");
        }

        // Enum values
        if (schema.Enumeration.Count > 0)
        {
            sb.Append(
                $"enum:[{string.Join(",", schema.Enumeration.Select(e => e?.ToString() ?? "null").OrderBy(e => e))}];");
        }

        // Default value
        if (schema.Default != null)
        {
            sb.Append($"default:{schema.Default};");
        }

        // Array items
        if (schema.Item != null)
        {
            sb.Append("items:{");
            AppendSchemaFingerprint(schema.Item, sb);
            sb.Append("};");
        }

        // OneOf/AnyOf/AllOf - hash contents, not just count
        AppendSchemaCollection(schema.OneOf, "oneOf", sb);
        AppendSchemaCollection(schema.AnyOf, "anyOf", sb);
        AppendSchemaCollection(schema.AllOf, "allOf", sb);

        // AdditionalProperties
        if (schema.AdditionalPropertiesSchema != null)
        {
            sb.Append("addProps:{");
            AppendSchemaFingerprint(schema.AdditionalPropertiesSchema, sb);
            sb.Append("};");
        }
    }

    /// <summary>
    ///     Appends property-specific fingerprint data.
    /// </summary>
    private static void AppendPropertyFingerprint(JsonSchemaProperty prop, StringBuilder sb)
    {
        sb.Append($"t:{prop.Type};");

        if (prop.Reference != null)
        {
            sb.Append($"ref:{prop.Reference.Title ?? prop.Reference.Id ?? "unknown"};");
        }

        if (prop.Format != null)
        {
            sb.Append($"fmt:{prop.Format};");
        }

        if (prop.IsNullableRaw.HasValue)
        {
            sb.Append($"null:{prop.IsNullableRaw};");
        }

        // Constraints
        if (prop.Minimum.HasValue)
        {
            sb.Append($"min:{prop.Minimum};");
        }

        if (prop.Maximum.HasValue)
        {
            sb.Append($"max:{prop.Maximum};");
        }

        if (prop.MinLength.HasValue)
        {
            sb.Append($"minLen:{prop.MinLength};");
        }

        if (prop.MaxLength.HasValue)
        {
            sb.Append($"maxLen:{prop.MaxLength};");
        }

        if (!string.IsNullOrEmpty(prop.Pattern))
        {
            sb.Append($"pattern:{prop.Pattern};");
        }

        if (prop.Default != null)
        {
            sb.Append($"default:{prop.Default};");
        }
    }

    /// <summary>
    ///     Appends a collection of schemas (oneOf, anyOf, allOf) with their contents.
    /// </summary>
    private static void AppendSchemaCollection(
        ICollection<JsonSchema> schemas,
        string name,
        StringBuilder sb)
    {
        if (schemas.Count == 0)
        {
            return;
        }

        sb.Append($"{name}:[");

        foreach (var schema in schemas)
        {
            sb.Append('{');
            AppendSchemaFingerprint(schema, sb);
            sb.Append("},");
        }

        sb.Append("];");
    }

    /// <summary>
    ///     Recursively scans schema for inline types that might be generated.
    /// </summary>
    private void ExtractInlineTypeHashes(JsonSchema schema, Dictionary<string, string> types)
    {
        // Check properties for anyOf/oneOf patterns that generate inline types
        foreach (var (propName, propSchema) in schema.Properties)
        {
            if (propSchema.AnyOf.Count > 0 || propSchema.OneOf.Count > 0)
            {
                var pascalCaseName = char.ToUpperInvariant(propName[0]) + propName[1..];
                var typeName = _typeNameGenerator.Generate(propSchema, pascalCaseName, []);
                types[typeName] = ComputeSchemaHash(propSchema);
            }
        }

        // Recurse into definitions
        foreach (var (_, definition) in schema.Definitions)
        {
            ExtractInlineTypeHashes(definition, types);
        }
    }
}
