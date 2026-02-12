using DotSchema.Analyzers;

using Microsoft.Extensions.Logging.Abstractions;

namespace DotSchema.Tests.Analyzers;

public class SchemaAnalyzerTests
{
    private readonly SchemaAnalyzer _analyzer = new(NullLogger.Instance);

    private static string GetTestDataPath(string filename)
    {
        // Navigate from bin/Debug/net8.0 to TestData
        var baseDir = AppContext.BaseDirectory;

        return Path.Combine(baseDir, "..", "..", "..", "TestData", filename);
    }

    [Fact]
    public async Task AnalyzeAsync_DetectsSharedTypes()
    {
        var schemas = new List<string> { GetTestDataPath("windows.schema.json"), GetTestDataPath("linux.schema.json") };

        var result = await _analyzer.AnalyzeAsync(schemas, "Windows");

        // SharedType has identical content in both schemas
        Assert.Contains("SharedType", result.SharedTypes);
    }

    [Fact]
    public async Task AnalyzeAsync_DetectsConflictingTypes()
    {
        var schemas = new List<string> { GetTestDataPath("windows.schema.json"), GetTestDataPath("linux.schema.json") };

        var result = await _analyzer.AnalyzeAsync(schemas, "Windows");

        // ProcessConfig has different properties in each schema
        Assert.Contains("ProcessConfig", result.ConflictingTypes);
    }

    [Fact]
    public async Task AnalyzeAsync_DetectsVariantSpecificTypes()
    {
        var schemas = new List<string> { GetTestDataPath("windows.schema.json"), GetTestDataPath("linux.schema.json") };

        var result = await _analyzer.AnalyzeAsync(schemas, "Windows");

        // WindowsOnlyType only exists in windows schema
        Assert.Contains("WindowsOnlyType", result.VariantTypes);
    }

    [Fact]
    public async Task AnalyzeAsync_ExtractsRootTypeName()
    {
        var schemas = new List<string> { GetTestDataPath("windows.schema.json"), GetTestDataPath("linux.schema.json") };

        var result = await _analyzer.AnalyzeAsync(schemas, "Windows");

        Assert.Equal("Config", result.RootTypeName);
    }

    [Fact]
    public async Task AnalyzeAsync_DeterminesPrimarySchemaPath()
    {
        var schemas = new List<string> { GetTestDataPath("windows.schema.json"), GetTestDataPath("linux.schema.json") };

        var result = await _analyzer.AnalyzeAsync(schemas, "Linux");

        Assert.Contains("linux.schema.json", result.PrimarySchemaPath);
    }

    [Fact]
    public async Task AnalyzeAsync_SingleSchema_ReturnsEmptySets()
    {
        var schemas = new List<string> { GetTestDataPath("windows.schema.json") };

        var result = await _analyzer.AnalyzeAsync(schemas, "Windows");

        Assert.Empty(result.SharedTypes);
        Assert.Empty(result.ConflictingTypes);
        Assert.Empty(result.VariantTypes);
    }

    [Fact]
    public async Task AnalyzeAsync_ExcludesRootTypeFromSharedAndConflicting()
    {
        var schemas = new List<string> { GetTestDataPath("windows.schema.json"), GetTestDataPath("linux.schema.json") };

        var result = await _analyzer.AnalyzeAsync(schemas, "Windows");

        // Root type "Config" should not be in shared or conflicting
        Assert.DoesNotContain("Config", result.SharedTypes);
        Assert.DoesNotContain("Config", result.ConflictingTypes);
    }
}
