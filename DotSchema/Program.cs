using CommandLine;

using DotSchema.Generators;

using Microsoft.Extensions.Logging;

namespace DotSchema;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        return await Parser.Default
                           .ParseArguments<GenerateOptions>(args)
                           .MapResult(
                               RunAsync,
                               _ => Task.FromResult(1));
    }

    private static async Task<int> RunAsync(GenerateOptions options)
    {
        // Determine log level based on verbose/quiet flags
        var logLevel = options.Verbose
            ? LogLevel.Debug
            : options.Quiet
                ? LogLevel.Error
                : LogLevel.Information;

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSimpleConsole(consoleOptions =>
            {
                consoleOptions.SingleLine = true;
                consoleOptions.TimestampFormat = null;
            });

            builder.SetMinimumLevel(logLevel);
        });

        var logger = loggerFactory.CreateLogger(nameof(Program));

        if (options.DryRun)
        {
            logger.LogInformation("Dry run mode - no files will be written");
        }

        return await SchemaGenerator.RunAsync(options, logger);
    }
}
