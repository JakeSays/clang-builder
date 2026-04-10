namespace Std.BuildTools.Clang;

public class MainToolchainPackager : ToolchainPackager
{
    private readonly BuildConfiguration _config;

    public MainToolchainPackager(BuildConfiguration config)
        : base()
    {
        _config = config;
    }

    public async Task<bool> Package()
    {
        var baseFileName = $"clang-{_config.LlvmVersion}-linux-x86_64";

        var tarballName = $"{baseFileName}.tar.zst";
        var tarballPath = _config.OutputDir / tarballName;

        Log.Info($"Creating {tarballPath}...");

        if (!PatchLlvmConfig(_config.InstallDir, "main"))
        {
            Log.Error("ERROR: Failed to patch main toolchain LLVMConfig.cmake.");
            return false;
        }

        CreateConvenienceSymlinks(_config.InstallDir);

        var threads = _config.PackageThreads > 0 ? _config.PackageThreads : 0;
        var args = new ArgBuilder()
            .Dash("I", $"zstd -19 -T{threads}", Quoted.Yes)
            .Dash("cf", tarballPath, Quoted.Yes)
            .Dash("C", _config.WorkDir, Quoted.Yes)
            .Text(baseFileName);

        var exitCode = await ProcessRunner.Run("tar", args.Build());
        if (exitCode != 0)
        {
            Log.Error("ERROR: Failed to package the toolchain.");
            return false;
        }

        Log.Info(LogColor.Green, $"Done: {tarballPath} ({new FileInfo(tarballPath).Length / 1024.0 / 1024.0:F2} MB)");
        return true;
    }
}
