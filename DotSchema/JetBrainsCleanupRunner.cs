using System.Diagnostics;

using Microsoft.Extensions.Logging;

namespace DotSchema;

/// <summary>
///     Runs JetBrains cleanup code tool on generated files.
/// </summary>
public static class JetBrainsCleanupRunner
{
    /// <summary>
    ///     Runs JetBrains cleanup on the specified files.
    /// </summary>
    public static async Task RunAsync(
        IReadOnlyList<string> filePaths,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (filePaths.Count == 0)
        {
            return;
        }

        var absolutePaths = filePaths.Select(Path.GetFullPath).ToList();

        // Find solution directory by walking up from the first file
        var (solutionDir, slnFile) = FindSolutionDirectory(absolutePaths[0]);

        if (solutionDir == null || slnFile == null)
        {
            logger.LogWarning("Could not find solution directory containing .sln file");

            return;
        }

        // jb cleanupcode needs relative paths for --include, semicolon-separated
        var relativePaths = absolutePaths
                            .Select(p => Path.GetRelativePath(solutionDir, p))
                            .ToList();

        var includePattern = string.Join(";", relativePaths);

        var processInfo = new ProcessStartInfo
        {
            FileName = Constants.JetBrains.DotnetExecutable,
            Arguments =
                $"tool run jb cleanupcode --profile=\"{Constants.JetBrains.CleanupProfile}\" --include=\"{includePattern}\" \"{slnFile}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = solutionDir
        };

        try
        {
            logger.LogInformation("Running jb cleanupcode on {FileCount} file(s)...", filePaths.Count);
            logger.LogDebug("Command: {FileName} {Arguments}", processInfo.FileName, processInfo.Arguments);

            using var process = Process.Start(processInfo);

            if (process != null)
            {
                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                    {
                        logger.LogDebug("{Output}", e.Data);
                    }
                };

                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                    {
                        logger.LogDebug("{Error}", e.Data);
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    logger.LogWarning("jb cleanupcode exit code: {ExitCode}", process.ExitCode);
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("jb cleanupcode was cancelled");

            throw;
        }
        catch (Exception ex)
        {
            // jb tool not available, skip formatting
            logger.LogWarning("jb cleanupcode failed: {Message}", ex.Message);
        }
    }

    /// <summary>
    ///     Walks up the directory tree from the starting path to find a directory containing a .sln file.
    /// </summary>
    /// <returns>A tuple of (solution directory, solution file path) or (null, null) if not found.</returns>
    private static (string? SolutionDir, string? SlnFile) FindSolutionDirectory(string startPath)
    {
        var dir = Path.GetDirectoryName(startPath);

        while (dir != null)
        {
            var slnFiles = Directory.GetFiles(dir, Constants.FilePatterns.SolutionPattern);

            if (slnFiles.Length > 0)
            {
                return (dir, slnFiles[0]);
            }

            dir = Path.GetDirectoryName(dir);
        }

        return (null, null);
    }
}
