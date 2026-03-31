using System.Reflection;
using System.Text.Json;

using DotSchema.Generators;

using Microsoft.Extensions.Logging.Abstractions;

namespace DotSchema.Tests.Generators;

public class ManifestGeneratorTests
{
    private static string GetEmbeddedSchemaPath(string filename)
    {
        // Write embedded resource to a temp file so ManifestGenerator can read it by path
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"DotSchema.Tests.TestData.{filename}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        // Use the original filename so Constants.ExtractVariantName resolves correctly
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotschema-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var tempPath = Path.Combine(tempDir, filename);
        File.WriteAllText(tempPath, content);

        return tempPath;
    }

    [Fact]
    public async Task RunAsync_GeneratesManifestWithCorrectTypes()
    {
        var windowsSchema = GetEmbeddedSchemaPath("windows.schema.json");
        var linuxSchema = GetEmbeddedSchemaPath("linux.schema.json");
        var outputPath = Path.Combine(Path.GetTempPath(), $"dotschema-test-manifest-{Guid.NewGuid()}.json");

        try
        {
            var options = new GenerateOptions
            {
                Mode = GenerationMode.Manifest,
                Schemas = [windowsSchema, linuxSchema],
                Output = outputPath,
                Namespace = "Test.Config"
            };

            var result = await ManifestGenerator.RunAsync(
                options,
                NullLogger.Instance);

            Assert.Equal(0, result);
            Assert.True(File.Exists(outputPath));

            var json = await File.ReadAllTextAsync(outputPath);
            var manifest = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(json);

            Assert.NotNull(manifest);
            Assert.Contains("windows", manifest.Keys);
            Assert.Contains("linux", manifest.Keys);

            // Windows should have its fields mapped
            var windows = manifest["windows"];
            Assert.Contains("shared_setting", windows.Keys);
            Assert.Contains("windows_only", windows.Keys);
            Assert.Contains("process_config", windows.Keys);

            // process_config should resolve to a C# type name
            var processConfig = windows["process_config"];
            var csharpType = processConfig.GetProperty("csharp_type").GetString();
            Assert.NotNull(csharpType);
            Assert.NotEmpty(csharpType);

            // Conflicting type should get variant prefix
            Assert.Equal("WindowsProcessConfig", csharpType);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
            if (File.Exists(windowsSchema)) File.Delete(windowsSchema);
            if (File.Exists(linuxSchema)) File.Delete(linuxSchema);
        }
    }

    [Fact]
    public async Task RunAsync_DryRunDoesNotWriteFile()
    {
        var windowsSchema = GetEmbeddedSchemaPath("windows.schema.json");
        var linuxSchema = GetEmbeddedSchemaPath("linux.schema.json");
        var outputPath = Path.Combine(Path.GetTempPath(), $"dotschema-test-dryrun-{Guid.NewGuid()}.json");

        try
        {
            var options = new GenerateOptions
            {
                Mode = GenerationMode.Manifest,
                Schemas = [windowsSchema, linuxSchema],
                Output = outputPath,
                Namespace = "Test.Config",
                DryRun = true
            };

            var result = await ManifestGenerator.RunAsync(
                options,
                NullLogger.Instance);

            Assert.Equal(0, result);
            Assert.False(File.Exists(outputPath));
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
            if (File.Exists(windowsSchema)) File.Delete(windowsSchema);
            if (File.Exists(linuxSchema)) File.Delete(linuxSchema);
        }
    }
}
