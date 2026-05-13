using System.Diagnostics;
using Std.BuildTools.Common;


namespace Std.BuildTools.Sysroots;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return 1;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            return args[0] switch
            {
                "--help" or "-h" => PrintHelpAndReturn(),
                _ => await BuildMakeSysrootCommand.Execute(args)
            };
        }
        catch (Exception ex)
        {
            Log.Error($"Unhandled exception: {ex.Message}");
            return 1;
        }
        finally
        {
            stopwatch.Stop();
            Log.Info(LogColor.HotPink, $@"Total time: {stopwatch.Elapsed:hh\:mm\:ss}");
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
