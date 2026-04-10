using Spectre.Console.Cli;

namespace Std.BuildTools.Clang;

public sealed class BuildMakeSysrootCommand : AsyncCommand<MakeSysrootSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, MakeSysrootSettings settings)
    {
        var outputDir = new FilePath(Path.GetFullPath(settings.OutputDir));
        var workDir = new FilePath(Path.GetFullPath(settings.WorkDir));
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(workDir);

        Log.Initialize(workDir);

        if (settings.Host)
        {
            var hostWorkDir = workDir / "musl-host";
            if (hostWorkDir.Exists)
            {
                Log.Error($"ERROR: Host musl work directory '{hostWorkDir}' already exists. Delete it first.");
                return 1;
            }

            var outputPath = outputDir / "sysroot-x64-musl-host.tar.xz";
            var builder = new AlpineSysrootBuilder(
                hostWorkDir,
                outputPath,
                apkArch: "x86_64",
                buildPython: true,
                pyVersion: settings.PyVersion,
                keepWorkDir: settings.KeepWorkDir,
                noPackage: settings.NoPackage);

            if (!await builder.Build())
            {
                return 1;
            }
        }

        if (settings.HostX64)
        {
            var archConfig = SysrootArchConfigs.All["host-x64"];
            var suite = settings.Release ?? archConfig.Suite;
            var packages = settings.Packages ?? archConfig.DefaultPackages;

            var builder = new SysrootBuilder(workDir, outputDir, "host-x64", archConfig with { Suite = suite }, packages, "sysroot-x64-glibc-host.tar.xz");
            if (!await builder.Build())
            {
                return 1;
            }
        }

        if (settings.Glibc)
        {
            foreach (var arch in settings.SelectedGlibcArchs)
            {
                var archConfig = SysrootArchConfigs.All[arch];
                var suite = settings.Release ?? archConfig.Suite;
                var packages = settings.Packages ?? archConfig.DefaultPackages;

                var builder = new SysrootBuilder(workDir, outputDir, arch, archConfig with { Suite = suite }, packages, $"sysroot-{arch}-glibc-cross.tar.xz");
                if (!await builder.Build())
                {
                    return 1;
                }
            }
        }

        if (settings.Musl)
        {
            foreach (var arch in settings.SelectedMuslArchs)
            {
                var muslWorkDir = workDir / $"musl-{arch}";
                if (muslWorkDir.Exists)
                {
                    Log.Error($"ERROR: Musl work directory '{muslWorkDir}' already exists. Delete it first.");
                    return 1;
                }

                var apkArch = ToApkArch(arch);
                var outputPath = outputDir / $"sysroot-{arch}-musl-cross.tar.xz";
                var builder = new AlpineSysrootBuilder(
                    muslWorkDir,
                    outputPath,
                    apkArch,
                    buildPython: false,
                    pyVersion: settings.PyVersion,
                    keepWorkDir: settings.KeepWorkDir,
                    noPackage: settings.NoPackage);

                if (!await builder.Build())
                {
                    return 1;
                }
            }
        }

        return 0;
    }

    private static string ToApkArch(string arch) => arch switch
    {
        "x64"     => "x86_64",
        "aarch64" => "aarch64",
        "armv7"   => "armv7",
        "riscv64" => "riscv64",
        "x86"     => "x86",
        _ => throw new ArgumentOutOfRangeException(nameof(arch), arch, null)
    };
}
