using System.Reflection;

using DotSchema.Analyzers;

using Microsoft.Extensions.Logging.Abstractions;

namespace DotSchema.Tests.Analyzers;

public class SchemaAnalyzerTests
{
    private readonly SchemaAnalyzer _analyzer = new(NullLogger.Instance);

    private static SchemaInput LoadEmbeddedSchema(string filename)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"DotSchema.Tests.TestData.{filename}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var reader = new StreamReader(stream);

        return SchemaInput.FromContent(filename, reader.ReadToEnd());
    }

    private static List<SchemaInput> GetTestSchemas() =>
    [
        LoadEmbeddedSchema("windows.schema.json"),
        LoadEmbeddedSchema("linux.schema.json")
    ];

    [Fact]
    public async Task AnalyzeAsync_DetectsSharedTypes()
    {
        var schemas = GetTestSchemas();

        var result = await _analyzer.AnalyzeAsync(schemas, "Windows");

        // SharedType has identical content in both schemas
        Assert.Contains("SharedType", result.SharedTypes);
    }

    [Fact]
    public async Task AnalyzeAsync_DetectsConflictingTypes()
    {
        var schemas = GetTestSchemas();

        var result = await _analyzer.AnalyzeAsync(schemas, "Windows");

        // ProcessConfig has different properties in each schema
        Assert.Contains("ProcessConfig", result.ConflictingTypes);
    }

    [Fact]
    public async Task AnalyzeAsync_DetectsVariantSpecificTypes()
    {
        var schemas = GetTestSchemas();

        var result = await _analyzer.AnalyzeAsync(schemas, "Windows");

        // WindowsOnlyType only exists in windows schema
        Assert.Contains("WindowsOnlyType", result.VariantTypes);
    }

    [Fact]
    public async Task AnalyzeAsync_ExtractsRootTypeName()
    {
        var schemas = GetTestSchemas();

        var result = await _analyzer.AnalyzeAsync(schemas, "Windows");

        Assert.Equal("Config", result.RootTypeName);
    }

    [Fact]
    public async Task AnalyzeAsync_DeterminesPrimarySchemaPath()
    {
        var schemas = GetTestSchemas();

        var result = await _analyzer.AnalyzeAsync(schemas, "Linux");

        Assert.Contains("linux.schema.json", result.PrimarySchemaPath);
    }

    [Fact]
    public async Task AnalyzeAsync_SingleSchema_ReturnsEmptySets()
    {
        var schemas = new List<SchemaInput> { LoadEmbeddedSchema("windows.schema.json") };

        var result = await _analyzer.AnalyzeAsync(schemas, "Windows");

        Assert.Empty(result.SharedTypes);
        Assert.Empty(result.ConflictingTypes);
        Assert.Empty(result.VariantTypes);
    }

    [Fact]
    public async Task AnalyzeAsync_ExcludesRootTypeFromSharedAndConflicting()
    {
        var schemas = GetTestSchemas();

        var result = await _analyzer.AnalyzeAsync(schemas, "Windows");

        // Root type "Config" should not be in shared or conflicting
        Assert.DoesNotContain("Config", result.SharedTypes);
        Assert.DoesNotContain("Config", result.ConflictingTypes);
    }
}
