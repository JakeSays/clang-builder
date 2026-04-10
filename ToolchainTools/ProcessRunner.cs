using System.Diagnostics;
using System.Text;

namespace Std.BuildTools.Clang;

internal static class ProcessRunner
{
    public static async Task<int> Run(
        string fileName,
        string arguments,
        string? workingDirectory = null)
    {
        Log.Info(LogColor.Cyan, $"Executing: {fileName} {arguments}");

        var process = new Process
        {
            StartInfo =
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
            }
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                Log.Info(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                Log.Error(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
        {
            Log.Info(LogColor.Green, "Success");
        }
        else
        {
            Log.Error($"Finished with exit code {process.ExitCode}");
        }
        return process.ExitCode;
    }

    public static async Task<(int ExitCode, string Output)> GetOutput(
        FilePath fileName,
        string arguments,
        string? workingDirectory = null)
    {
        return await GetOutput((string)fileName, arguments, workingDirectory);
    }

    public static async Task<(int ExitCode, string Output)> GetOutput(
        string fileName,
        string arguments,
        string? workingDirectory = null)
    {
        var process = new Process
        {
            StartInfo =
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
            }
        };

        var outputBuilder = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                Log.Info(e.Data);
                outputBuilder.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                Log.Error(e.Data);
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        return (process.ExitCode, outputBuilder.ToString().Trim());
    }
}
