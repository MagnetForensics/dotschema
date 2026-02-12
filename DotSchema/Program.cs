using CommandLine;

using Magnet.Live2Json.SchemaGenerator;
using Magnet.Live2Json.SchemaGenerator.Generators;

using Microsoft.Extensions.Logging;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = null;
            });

            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger(nameof(Program));

        return await Parser.Default
                           .ParseArguments<GenerateOptions>(args)
                           .MapResult(
                               options => SchemaGenerator.RunAsync(options, logger),
                               _ => Task.FromResult(1));
    }
}
