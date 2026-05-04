namespace Std.BuildTools.Clang;

public class MainToolchainPackager : ToolchainPackager
{
    private readonly BuildConfiguration _config;

    public MainToolchainPackager(BuildConfiguration config)
        : base()
    {
        _config = config;
    }

    private bool CopyCmakeToolchains()
    {
        var sourceDir = _config.CmakeToolchainsDir;
        if (!sourceDir.Exists)
        {
            Log.Error($"ERROR: cmake toolchains directory not found at '{sourceDir}'.");
            return false;
        }

        var destDir = _config.InstallDir / "cmake";
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*.cmake"))
        {
            var dest = destDir / Path.GetFileName(file);
            File.Copy(file, dest, overwrite: true);
        }

        Log.Info($"Copied cmake toolchain files to {destDir}.");
        return true;
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

        if (!CopyCmakeToolchains())
        {
            return false;
        }

        CreateConvenienceSymlinks(_config.InstallDir);

        // Drop the ~600 MB of LLVM/Clang/LLD/LLDB static archives at the top of lib/.
        // These are dev artifacts for tools that link against LLVM as a library; this
        // toolchain only ships clang/lld/lldb for compilation. The cross-build phases
        // need them via LLVMExports.cmake, so deletion happens here at packaging time
        // after all builds are complete. Per-target runtimes under lib/<triple>/ and
        // lib/clang/<ver>/lib/<triple>/ are preserved.
        var topLibDir = _config.InstallDir / "lib";
        if (Directory.Exists(topLibDir))
        {
            foreach (var file in Directory.EnumerateFiles(topLibDir, "*.a", SearchOption.TopDirectoryOnly))
            {
                File.Delete(file);
            }
        }

        Directory.CreateDirectory(_config.OutputDir);

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
