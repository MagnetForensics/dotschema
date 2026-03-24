namespace DotSchema.Tests;

public class CodePostProcessorTests
{
    private static readonly HashSet<string> EmptySet = [];

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
            EmptySet,
            variantTypes,
            EmptySet,
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
            EmptySet,
            EmptySet,
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
            EmptySet,
            EmptySet,
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
            EmptySet,
            EmptySet,
            EmptySet,
            "Config");

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
            EmptySet,
            EmptySet,
            EmptySet,
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
            EmptySet,
            EmptySet,
            EmptySet,
            "Config");

        Assert.Contains("public sealed class MyType", result);
        Assert.DoesNotContain("public partial class MyType", result);
    }

    [Fact]
    public void Process_RemovesAdditionalPropertiesBoilerplate()
    {
        var code = """
            namespace Test;

            public partial class MyType
            {
                public string Name { get; set; }

                private System.Collections.Generic.IDictionary<string, object>? _additionalProperties;

                [System.Text.Json.Serialization.JsonExtensionData]
                public System.Collections.Generic.IDictionary<string, object> AdditionalProperties
                {
                    get { return _additionalProperties ?? (_additionalProperties = new System.Collections.Generic.Dictionary<string, object>()); }
                    set { _additionalProperties = value; }
                }
            }
            """;

        var result = CodePostProcessor.Process(
            code,
            GenerationMode.All,
            "",
            EmptySet,
            EmptySet,
            EmptySet,
            "Config");

        Assert.DoesNotContain("_additionalProperties", result);
        Assert.DoesNotContain("AdditionalProperties", result);
        Assert.DoesNotContain("JsonExtensionData", result);
        Assert.Contains("Name", result);
    }

    [Fact]
    public void Process_PreservesBaseClassesAsNonSealed()
    {
        var code = """
            namespace Test;

            public partial class BaseType
            {
                public string Name { get; set; }
            }

            public partial class DerivedType : BaseType
            {
                public int Value { get; set; }
            }
            """;

        var result = CodePostProcessor.Process(
            code,
            GenerationMode.All,
            "",
            EmptySet,
            EmptySet,
            EmptySet,
            "Config");

        // BaseType should NOT be sealed (it's inherited from)
        Assert.DoesNotContain("public sealed class BaseType", result);

        // DerivedType should be sealed
        Assert.Contains("public sealed class DerivedType", result);
    }

    [Fact]
    public void Process_SharedMode_RemovesRootType()
    {
        var code = """
            namespace Test;

            public partial class SharedType
            {
                public string Name { get; set; }
            }

            public partial class Config
            {
                public int Value { get; set; }
            }
            """;

        var result = CodePostProcessor.Process(
            code,
            GenerationMode.Shared,
            "",
            EmptySet,
            EmptySet,
            EmptySet,
            "Config");

        Assert.Contains("SharedType", result);
        Assert.DoesNotContain("public sealed class Config", result);
    }

    [Fact]
    public void Process_DoesNotSealAbstractClasses()
    {
        var code = """
            namespace Test;

            public abstract partial class AbstractBase
            {
                public abstract string Name { get; set; }
            }

            public partial class ConcreteType : AbstractBase
            {
                public override string Name { get; set; }
            }
            """;

        var result = CodePostProcessor.Process(
            code,
            GenerationMode.All,
            "",
            EmptySet,
            EmptySet,
            EmptySet,
            "Config");

        // AbstractBase should remain abstract (not sealed)
        Assert.Contains("public abstract class AbstractBase", result);
        Assert.DoesNotContain("sealed abstract", result);

        // ConcreteType should NOT be sealed either (it's a base class for AbstractBase inheritance)
        // Actually, ConcreteType inherits from AbstractBase, so AbstractBase is the base class
        // ConcreteType should be sealed since it's not inherited from
        Assert.Contains("public sealed class ConcreteType", result);
    }

    [Fact]
    public void Process_DoesNotSealStaticClasses()
    {
        var code = """
            namespace Test;

            public static partial class StaticHelper
            {
                public static string GetValue() => "test";
            }

            public partial class NormalType
            {
                public string Name { get; set; }
            }
            """;

        var result = CodePostProcessor.Process(
            code,
            GenerationMode.All,
            "",
            EmptySet,
            EmptySet,
            EmptySet,
            "Config");

        // StaticHelper should remain static (not sealed)
        Assert.Contains("public static class StaticHelper", result);
        Assert.DoesNotContain("sealed static", result);

        // NormalType should be sealed
        Assert.Contains("public sealed class NormalType", result);
    }
}
