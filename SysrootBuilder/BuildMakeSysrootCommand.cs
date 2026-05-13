using Std.BuildTools.Common;


namespace Std.BuildTools.Sysroots;

public static class BuildMakeSysrootCommand
{
    public static async Task<CommandLineResult> Execute(string[] args)
    {
        CommandLineResult result;
        if ((result = CommandLineParser.ParseSysroot(args, out var sysrootArgs)) != CommandLineResult.Success)
        {
            return result;
        }

        var outputDir = sysrootArgs.OutputDir;
        var workDir = sysrootArgs.WorkDir;
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(workDir);

        Log.Initialize(workDir);


        if (sysrootArgs.Host)
        {
            var hostWorkDir = workDir / "musl-host";
            if (hostWorkDir.Exists)
            {
                Log.Error($"ERROR: Host musl work directory '{hostWorkDir}' already exists. Delete it first.");
                return CommandLineResult.Failure;
            }

            var outputPath = outputDir / "sysroot-x64-musl-host.tar.xz";
            var builder = new AlpineSysrootBuilder(
                sysrootArgs,
                hostWorkDir,
                outputPath,
                apkArch: "x86_64",
                buildPython: true);

            if (!await builder.Build())
            {
                return CommandLineResult.Failure;
            }
            Log.Info(LogColor.Green, $"Success! Sysroot archive is ready: {outputPath}");
        }

        if (sysrootArgs.HostX64)
        {
            var archConfig = SysrootArchConfigs.All["host-x64"];
            var suite = sysrootArgs.Release ?? archConfig.Suite;
            var mirror = sysrootArgs.RepoUrl ?? archConfig.Mirror;
            var packages = sysrootArgs.Packages ?? archConfig.DefaultPackages;
            const string archiveName = "sysroot-x64-glibc-host.tar.xz";

            var builder = new DebianSysrootBuilder(workDir, outputDir, "host-x64", archConfig with { Suite = suite, Mirror = mirror }, packages, archiveName);
            if (!await builder.Build())
            {
                return CommandLineResult.Failure;
            }
            var outputPath = outputDir / archiveName;
            Log.Info(LogColor.Green, $"Success! Sysroot archive is ready: {outputPath}");
        }

        if (sysrootArgs.Glibc)
        {
            foreach (var arch in sysrootArgs.SelectedGlibcArchs)
            {
                var archConfig = SysrootArchConfigs.All[arch];
                var suite = sysrootArgs.Release ?? archConfig.Suite;
                var mirror = sysrootArgs.RepoUrl ?? archConfig.Mirror;
                var packages = sysrootArgs.Packages ?? archConfig.DefaultPackages;
                var archiveName = $"sysroot-{arch}-glibc-cross.tar.xz";

                var builder = new DebianSysrootBuilder(workDir, outputDir, arch, archConfig with { Suite = suite, Mirror = mirror }, packages, archiveName);
                if (!await builder.Build())
                {
                    return CommandLineResult.Failure;
                }

                var outputPath = outputDir / archiveName;
                Log.Info(LogColor.Green, $"Success! Sysroot archive is ready: {outputPath}");
            }
        }

        if (sysrootArgs.Musl)
        {
            foreach (var arch in sysrootArgs.SelectedMuslArchs)
            {
                var muslWorkDir = workDir / $"musl-{arch}";
                if (muslWorkDir.Exists)
                {
                    Log.Error($"ERROR: Musl work directory '{muslWorkDir}' already exists. Delete it first.");
                    return CommandLineResult.Failure;
                }

                var apkArch = ToApkArch(arch);
                var outputPath = outputDir / $"sysroot-{arch}-musl-cross.tar.xz";
                var builder = new AlpineSysrootBuilder(
                    sysrootArgs,
                    muslWorkDir,
                    outputPath,
                    apkArch,
                    buildPython: false);

                if (!await builder.Build())
                {
                    return CommandLineResult.Failure;
                }

                Log.Info(LogColor.Green, $"Success! Sysroot archive is ready: {outputPath}");
            }
        }

        Cleanup(sysrootArgs);

        return CommandLineResult.Success;
    }

    private static void Cleanup(SysrootArgs args)
    {
        if (!args.KeepWorkDir)
        {
            Log.Info($"Deleting working directory '{args.WorkDir}'...");
            FileUtils.DeleteDirectory(args.WorkDir);
        }
        else
        {
            Log.Info($"Kept working directory at '{args.WorkDir}'.");
        }
    }

    private static string ToApkArch(string arch) => arch switch
    {
        "x64" => "x86_64",
        "aarch64" => "aarch64",
        "armv7" => "armv7",
        "riscv64" => "riscv64",
        "x86" => "x86",
        _ => throw new ArgumentOutOfRangeException(nameof(arch), arch, null)
    };
}
