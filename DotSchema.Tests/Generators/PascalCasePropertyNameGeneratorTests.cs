using DotSchema.Generators;

using NJsonSchema;

namespace DotSchema.Tests.Generators;

public class PascalCasePropertyNameGeneratorTests
{
    private readonly PascalCasePropertyNameGenerator _generator = new();

    private static JsonSchemaProperty CreateProperty(string name)
    {
        var schema = new JsonSchema();
        schema.Properties[name] = new JsonSchemaProperty();

        return schema.Properties[name];
    }

    [Theory]
    [InlineData("user_name", "UserName")]
    [InlineData("first_name", "FirstName")]
    [InlineData("id", "Id")]
    [InlineData("user_id", "UserId")]
    [InlineData("created_at", "CreatedAt")]
    [InlineData("is_active", "IsActive")]
    [InlineData("http_status_code", "HttpStatusCode")]
    public void Generate_ConvertsSnakeCaseToPascalCase(string input, string expected)
    {
        var property = CreateProperty(input);

        var result = _generator.Generate(property);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("UserName", "UserName")]
    [InlineData("firstName", "FirstName")]
    [InlineData("ID", "ID")]
    public void Generate_HandlesNonSnakeCaseInput(string input, string expected)
    {
        var property = CreateProperty(input);

        var result = _generator.Generate(property);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("_name", "Name")]
    [InlineData("name_", "Name")]
    [InlineData("user__name", "UserName")]
    public void Generate_HandlesEdgeCases(string input, string expected)
    {
        var property = CreateProperty(input);

        var result = _generator.Generate(property);

        Assert.Equal(expected, result);
    }
}
