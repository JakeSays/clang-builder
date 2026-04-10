using Spectre.Console.Cli;

namespace Std.BuildTools.Clang;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var app = new CommandApp();

        app.Configure(config =>
        {
            config.SetApplicationName("toolchain-tools");
            config.AddCommand<BuildMainToolchainCommand>("build")
                .WithDescription("Builds the complete cross-compilation toolchain.");
            config.AddCommand<BuildBootstrapToolchainCommand>("build-bootstrap")
                .WithDescription("Builds a minimal bootstrap Clang from source.");
            config.AddCommand<BuildMakeSysrootCommand>("make-sysroot")
                .WithDescription("Builds glibc (Debian) or musl (Alpine) sysroots for cross-compilation.");
        });

        return await app.RunAsync(args);
    }
}
