using System.Diagnostics;
using System.Text;

namespace Aether.BuildTool;

internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

internal static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken,
        bool echoOutput = true)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = new Process { StartInfo = startInfo };
            var standardOutput = new StringBuilder();
            var standardError = new StringBuilder();
            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                {
                    standardOutput.AppendLine(args.Data);
                    if (echoOutput)
                    {
                        Console.WriteLine(args.Data);
                    }
                }
            };
            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                {
                    standardError.AppendLine(args.Data);
                    if (echoOutput)
                    {
                        Console.Error.WriteLine(args.Data);
                    }
                }
            };

            if (!process.Start())
            {
                throw new BuildToolException($"Failed to start '{fileName}'.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(cancellationToken);
            return new ProcessResult(process.ExitCode, standardOutput.ToString(), standardError.ToString());
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            throw new BuildToolException($"Could not start '{fileName}'. Ensure the required compiler is installed and available.", exception);
        }
    }

    public static async Task RunCheckedAsync(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(fileName, arguments, workingDirectory, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new BuildToolException($"'{fileName}' exited with code {result.ExitCode}.");
        }
    }
}
