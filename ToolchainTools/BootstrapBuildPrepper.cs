namespace Std.BuildTools.Clang;

public record BootstrapBuildPaths(
    string HostSysrootPath,
    string SrcDir,
    string BuildDir,
    string InstallDir
);

public class BootstrapBuildPrepper : BuildPrepper
{
    private readonly BootstrapBuildSettings _settings;

    public BootstrapBuildPrepper(
        BootstrapBuildSettings settings,
        FileDownloader downloader,
        FilePath workDir,
        FilePath prebuiltsSourceDir,
        FilePath prebuiltsOutputDir)
        : base(downloader, workDir, prebuiltsSourceDir, prebuiltsOutputDir, settings.KeepWorkDir)
    {
        _settings = settings;
    }

    public async Task<BootstrapBuildPaths?> Prepare()
    {
        Log.Info("--- Preparing Bootstrap Build Environment ---");

        if (!Directory.Exists(PrebuiltsSourceDir))
        {
            Log.Error($"ERROR: Prebuilts directory not found at '{PrebuiltsSourceDir}'");
            return null;
        }

        if (!CheckHostDependencies())
        {
            return null;
        }

        if (!await PrebuiltsUtilities.ExpandPrebuilts(PrebuiltsSourceDir, WorkDir, Downloader, PrebuiltType.HostSysroot))
        {
            return null;
        }

        var hostSysrootPath = PrebuiltsUtilities.GetPrebuiltPath(PrebuiltType.HostSysroot)!;
        var srcDir = _settings.SrcDir ?? Path.Combine(WorkDir, "llvm-src");

        if (!await FetchLlvmSource(_settings.LlvmVersion, srcDir))
        {
            return null;
        }

        var installDir = Path.Combine(WorkDir, "install");
        var buildDir = Path.Combine(WorkDir, "build");
        Directory.CreateDirectory(installDir);
        Directory.CreateDirectory(buildDir);

        Log.Info(LogColor.Green, "Bootstrap build preparation complete.");
        return new BootstrapBuildPaths(hostSysrootPath, srcDir, buildDir, installDir);
    }
}
