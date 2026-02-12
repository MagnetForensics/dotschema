namespace DotSchema.Tests;

public class CodePostProcessorTests
{
    [Fact]
    public void Process_SharedMode_RemovesVariantSpecificTypes()
    {
        var code = """
            namespace Test;

            public partial class SharedType
            {
                public string Name { get; set; }
            }

            public partial class VariantOnlyType
            {
                public int Value { get; set; }
            }
            """;

        var variantTypes = new HashSet<string> { "VariantOnlyType" };

        var result = CodePostProcessor.Process(
            code,
            GenerationMode.Shared,
            "",
            [],
            variantTypes,
            [],
            "Config");

        Assert.Contains("SharedType", result);
        Assert.DoesNotContain("VariantOnlyType", result);
    }

    [Fact]
    public void Process_SharedMode_RemovesConflictingTypes()
    {
        var code = """
            namespace Test;

            public partial class SharedType
            {
                public string Name { get; set; }
            }

            public partial class ConflictingType
            {
                public int Value { get; set; }
            }
            """;

        var conflictingTypes = new HashSet<string> { "ConflictingType" };

        var result = CodePostProcessor.Process(
            code,
            GenerationMode.Shared,
            "",
            [],
            [],
            conflictingTypes,
            "Config");

        Assert.Contains("SharedType", result);
        Assert.DoesNotContain("ConflictingType", result);
    }

    [Fact]
    public void Process_VariantMode_RemovesSharedTypes()
    {
        var code = """
            namespace Test;

            public partial class SharedType
            {
                public string Name { get; set; }
            }

            public partial class WindowsConfig
            {
                public int Value { get; set; }
            }
            """;

        var sharedTypes = new HashSet<string> { "SharedType" };

        var result = CodePostProcessor.Process(
            code,
            GenerationMode.Variant,
            "Windows",
            sharedTypes,
            [],
            [],
            "Config");

        Assert.DoesNotContain("public sealed class SharedType", result);
        Assert.Contains("WindowsConfig", result);
    }

    [Fact]
    public void Process_VariantMode_AddsInterfaceToRootType()
    {
        var code = """
            namespace Test;

            public partial class WindowsConfig
            {
                public int Value { get; set; }
            }
            """;

        var result = CodePostProcessor.Process(
            code,
            GenerationMode.Variant,
            "Windows",
            [],
            [],
            [],
            "Config",
            true);

        Assert.Contains("WindowsConfig : IConfig", result);
    }

    [Fact]
    public void Process_VariantMode_SkipsInterfaceWhenDisabled()
    {
        var code = """
            namespace Test;

            public partial class WindowsConfig
            {
                public int Value { get; set; }
            }
            """;

        var result = CodePostProcessor.Process(
            code,
            GenerationMode.Variant,
            "Windows",
            [],
            [],
            [],
            "Config",
            false);

        Assert.DoesNotContain(": IConfig", result);
    }

    [Fact]
    public void Process_MakesClassesSealed()
    {
        var code = """
            namespace Test;

            public partial class MyType
            {
                public string Name { get; set; }
            }
            """;

        var result = CodePostProcessor.Process(
            code,
            GenerationMode.All,
            "",
            [],
            [],
            [],
            "Config");

        Assert.Contains("public sealed class MyType", result);
        Assert.DoesNotContain("public partial class MyType", result);
    }
}
