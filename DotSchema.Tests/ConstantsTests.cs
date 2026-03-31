namespace DotSchema.Tests;

public class ConstantsTests
{
    [Theory]
    [InlineData("windows.schema.json", "Windows")]
    [InlineData("linux.schema.json", "Linux")]
    [InlineData("windows.config.schema.json", "Windows")]
    [InlineData("windows-config.json", "Windows")]
    [InlineData("windows_config.json", "Windows")]
    [InlineData("WINDOWS.schema.json", "Windows")]
    public void ExtractVariantName_HandlesCommonPatterns(string filename, string expected)
    {
        var result = Constants.ExtractVariantName(filename);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("WindowsConfig.json", "Windows")]
    [InlineData("LinuxSettings.json", "Linux")]
    [InlineData("MacOSPreferences.json", "Mac")]
    public void ExtractVariantName_HandlesPascalCaseFilenames(string filename, string expected)
    {
        var result = Constants.ExtractVariantName(filename);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("c:\\schemas\\windows.schema.json", "Windows")]
    [InlineData("/home/user/schemas/linux.schema.json", "Linux")]
    public void ExtractVariantName_HandlesFullPaths(string path, string expected)
    {
        var result = Constants.ExtractVariantName(path);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetInterfaceName_AddsIPrefix()
    {
        Assert.Equal("IConfig", Constants.GetInterfaceName("Config"));
        Assert.Equal("ISettings", Constants.GetInterfaceName("Settings"));
    }

    [Fact]
    public void GetInterfaceFileName_GeneratesCorrectFilename()
    {
        Assert.Equal("IConfig.cs", Constants.GetInterfaceFileName("Config"));
    }

    [Fact]
    public void GetSharedFileName_GeneratesCorrectFilename()
    {
        Assert.Equal("SharedConfig.cs", Constants.GetSharedFileName("Config"));
    }

    [Fact]
    public void GetVariantFileName_GeneratesCorrectFilename()
    {
        Assert.Equal("WindowsConfig.cs", Constants.GetVariantFileName("Windows", "Config"));
    }
}
