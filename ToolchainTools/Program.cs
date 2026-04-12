namespace Std.BuildTools.Clang;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return 1;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            return args[0] switch
            {
                "build" => await BuildMainToolchainCommand.Execute(args[1..]),
                "build-bootstrap" => await BuildBootstrapToolchainCommand.Execute(args[1..]),
                "make-sysroot" => await BuildMakeSysrootCommand.Execute(args[1..]),
                "--help" or "-h" => PrintHelpAndReturn(),
                _ => UnknownCommand(args[0]),
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

    private static int UnknownCommand(string cmd)
    {
        Console.Error.WriteLine($"Unknown command '{cmd}'.");
        PrintHelp();
        return 1;
    }

    private static void PrintHelp()
    {
        Console.Error.WriteLine("""
            Usage: clang-builder <command> [options]

            Commands:
              build             Build the complete cross-compilation toolchain.
              build-bootstrap   Build a minimal bootstrap Clang from source.
              make-sysroot      Build glibc (Debian) or musl (Alpine) sysroots.

            Use 'clang-builder <command> --help' for command-specific options.
            """);
    }
}
