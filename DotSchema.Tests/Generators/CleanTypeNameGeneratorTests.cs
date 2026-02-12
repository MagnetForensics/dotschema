using DotSchema.Generators;

using NJsonSchema;

namespace DotSchema.Tests.Generators;

public class CleanTypeNameGeneratorTests
{
    [Theory]
    [InlineData("FlattenedValue_ActiveUsersCollector_and_ConfigMetadata", "ActiveUsersCollectorConfigMetadata")]
    [InlineData("Wrapper_TypeA_or_TypeB", "TypeATypeB")]
    [InlineData("Container_Item_with_Metadata", "ItemMetadata")]
    [InlineData("Parent_Child_of_GrandChild", "ChildGrandChild")]
    public void Generate_CleansConjunctionNames(string input, string expected)
    {
        var generator = new CleanTypeNameGenerator();
        var schema = new JsonSchema();

        var result = generator.Generate(schema, input, []);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("SimpleType", "SimpleType")]
    [InlineData("UserConfig", "UserConfig")]
    [InlineData("Type_With_Underscores", "TypeWithUnderscores")]
    public void Generate_PreservesSimpleNames(string input, string expected)
    {
        var generator = new CleanTypeNameGenerator();
        var schema = new JsonSchema();

        var result = generator.Generate(schema, input, []);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Generate_RenamesRootTypeWithVariant()
    {
        var generator = new CleanTypeNameGenerator("Windows", "Config", []);
        var schema = new JsonSchema();

        var result = generator.Generate(schema, "Config", []);

        Assert.Equal("WindowsConfig", result);
    }

    [Fact]
    public void Generate_PrefixesConflictingTypesWithVariant()
    {
        var conflictingTypes = new HashSet<string> { "ProcessConfig", "NetworkSettings" };
        var generator = new CleanTypeNameGenerator("Linux", "Config", conflictingTypes);
        var schema = new JsonSchema();

        var result = generator.Generate(schema, "ProcessConfig", []);

        Assert.Equal("LinuxProcessConfig", result);
    }

    [Fact]
    public void Generate_DoesNotPrefixNonConflictingTypes()
    {
        var conflictingTypes = new HashSet<string> { "ProcessConfig" };
        var generator = new CleanTypeNameGenerator("Linux", "Config", conflictingTypes);
        var schema = new JsonSchema();

        var result = generator.Generate(schema, "SharedType", []);

        Assert.Equal("SharedType", result);
    }

    [Fact]
    public void Generate_EnsuresUniqueNames()
    {
        var generator = new CleanTypeNameGenerator();
        var schema = new JsonSchema();
        var reserved = new[] { "MyType", "MyType1" };

        var result = generator.Generate(schema, "MyType", reserved);

        Assert.Equal("MyType2", result);
    }

    [Fact]
    public void Generate_UsesSchemaTitle_WhenNoHint()
    {
        var generator = new CleanTypeNameGenerator();
        var schema = new JsonSchema { Title = "MySchemaTitle" };

        var result = generator.Generate(schema, null, []);

        Assert.Equal("MySchemaTitle", result);
    }

    [Fact]
    public void Generate_UsesAnonymous_WhenNoHintOrTitle()
    {
        var generator = new CleanTypeNameGenerator();
        var schema = new JsonSchema();

        var result = generator.Generate(schema, null, []);

        Assert.Equal(Constants.AnonymousTypeName, result);
    }
}
