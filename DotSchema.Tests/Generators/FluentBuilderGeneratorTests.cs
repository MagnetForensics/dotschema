using DotSchema.Generators;

namespace DotSchema.Tests.Generators;

public class FluentBuilderGeneratorTests
{
    [Fact]
    public void Generate_SimpleConfig_GeneratesEnabledOnlyMethods()
    {
        var variantCode = """
            namespace Test;

            public sealed class WindowsConfig
            {
                public SysInfoCollectorConfigMetadata? SysInfo { get; set; } = null;
            }
            """;

        var sharedCode = """
            namespace Test;

            public sealed class SysInfoCollectorConfigMetadata
            {
                public SysInfoCollectorConfigMetadata(bool enabled)
                {
                    Enabled = enabled;
                }

                public bool Enabled { get; }
            }
            """;

        var result = FluentBuilderGenerator.Generate(variantCode, [sharedCode], "Test");

        Assert.NotNull(result);
        Assert.Contains("public static WindowsConfig SysInfo(this WindowsConfig config, bool enabled = true)", result);
        Assert.Contains("config.SysInfo = new SysInfoCollectorConfigMetadata(enabled);", result);
        Assert.Contains("return config;", result);
    }

    [Fact]
    public void Generate_ExtendedConfig_PutsEnabledLastWithDefault()
    {
        var variantCode = """
            namespace Test;

            public sealed class WindowsConfig
            {
                public CommandCollectorConfigMetadata? Commands { get; set; } = null;
            }

            public sealed class CommandCollectorConfigMetadata
            {
                public CommandCollectorConfigMetadata(System.Collections.Generic.ICollection<Command> commands, bool enabled)
                {
                    Commands = commands;
                    Enabled = enabled;
                }

                public System.Collections.Generic.ICollection<Command> Commands { get; }
                public bool Enabled { get; }
            }
            """;

        var result = FluentBuilderGenerator.Generate(variantCode, [], "Test");

        Assert.NotNull(result);

        // enabled should be last with default true
        Assert.Contains("System.Collections.Generic.ICollection<Command> commands, bool enabled = true", result);
        Assert.Contains("config.Commands = new CommandCollectorConfigMetadata(commands, enabled);", result);
    }

    [Fact]
    public void Generate_NoConfigClass_ReturnsNull()
    {
        var code = """
            namespace Test;

            public sealed class SomeOtherType
            {
                public string Name { get; set; }
            }
            """;

        var result = FluentBuilderGenerator.Generate(code, [], "Test");

        Assert.Null(result);
    }

    [Fact]
    public void Generate_MultipleProperties_GeneratesAllMethods()
    {
        var variantCode = """
            namespace Test;

            public sealed class LinuxConfig
            {
                public ActiveUsersConfigMetadata? ActiveUsers { get; set; } = null;
                public ArpConfigMetadata? Arp { get; set; } = null;
            }
            """;

        var sharedCode = """
            namespace Test;

            public sealed class ActiveUsersConfigMetadata
            {
                public ActiveUsersConfigMetadata(bool enabled) { }
            }

            public sealed class ArpConfigMetadata
            {
                public ArpConfigMetadata(bool enabled) { }
            }
            """;

        var result = FluentBuilderGenerator.Generate(variantCode, [sharedCode], "Test");

        Assert.NotNull(result);
        Assert.Contains("LinuxConfigExtensions", result);
        Assert.Contains("public static LinuxConfig ActiveUsers(", result);
        Assert.Contains("public static LinuxConfig Arp(", result);
    }

    [Fact]
    public void Generate_ExtensionClass_IsStatic()
    {
        var variantCode = """
            namespace Test;

            public sealed class MacosConfig
            {
                public SysInfoCollectorConfigMetadata? SysInfo { get; set; } = null;
            }

            public sealed class SysInfoCollectorConfigMetadata
            {
                public SysInfoCollectorConfigMetadata(bool enabled) { }
            }
            """;

        var result = FluentBuilderGenerator.Generate(variantCode, [], "Test");

        Assert.NotNull(result);
        Assert.Contains("public static class MacosConfigExtensions", result);
    }
}
