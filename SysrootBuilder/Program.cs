using System.Diagnostics;
using Std.BuildTools.Common;


namespace Std.BuildTools.Sysroots;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var stopwatch = Stopwatch.StartNew();
        var displayTiming = false;
        try
        {
            var result = await BuildMakeSysrootCommand.Execute(args);
            if (result != CommandLineResult.Success)
            {
                return 1;
            }

            displayTiming = true;

            return 0;
        }
        catch (Exception ex)
        {
            Log.Error($"Unhandled exception: {ex.Message}");
            return 1;
        }
        finally
        {
            stopwatch.Stop();

            if (displayTiming)
            {
                Log.Info(LogColor.HotPink, $@"Total time: {stopwatch.Elapsed:hh\:mm\:ss}");
            }
        }
    }

    private static int PrintHelpAndReturn()
    {
        PrintHelp();
        return 0;
    }

    private static void PrintHelp()
    {
        Console.Error.WriteLine("""
            Usage: sysroot-builder [options]

            Use 'sysroot-builder --help' for command-specific options.
            """);
    }
}
