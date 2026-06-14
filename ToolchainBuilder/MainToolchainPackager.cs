using Std.BuildTools.Common;


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

    private async Task<bool> PackageDevLibs(string baseFileName)
    {
        var topLibDir = _config.InstallDir / "lib";
        if (!Directory.Exists(topLibDir))
        {
            return true;
        }

        var stagingRoot = _config.WorkDir / $"{baseFileName}-devlibs";
        if (Directory.Exists(stagingRoot))
        {
            Directory.Delete(stagingRoot, recursive: true);
        }

        var stagingLibDir = stagingRoot / baseFileName / "lib";
        Directory.CreateDirectory(stagingLibDir);

        var moved = 0;
        foreach (var file in Directory.EnumerateFiles(topLibDir, "*.a", SearchOption.TopDirectoryOnly))
        {
            var dest = stagingLibDir / Path.GetFileName(file);
            File.Move(file, dest, overwrite: true);
            moved++;
        }

        if (moved == 0)
        {
            Directory.Delete(stagingRoot, recursive: true);
            Log.Info("No top-level static archives to package; skipping dev-libs tarball.");
            return true;
        }

        // Host tools and these LLVM/Clang archives link against the Alpine musl
        // sysroot, so the dev libs are musl. The project is musl-only on the host,
        // so the libc tag is fixed.
        var tarballName = $"clang+llvm-{_config.LlvmVersion}-linux-x86_64-musl-dev.tar.zst";
        var tarballPath = _config.OutputDir / tarballName;

        Log.Info($"Creating {tarballPath} ({moved} static archives)...");

        var threads = _config.PackageThreads > 0 ? _config.PackageThreads : 0;
        var args = new ArgBuilder()
            .Dash("I", $"zstd -19 -T{threads}", Quoted.Yes)
            .Dash("cf", tarballPath, Quoted.Yes)
            .Dash("C", stagingRoot, Quoted.Yes)
            .Text(baseFileName);

        var exitCode = await ProcessRunner.Run("tar", args.Build());
        if (exitCode != 0)
        {
            Log.Error("ERROR: Failed to package the dev-libs tarball.");
            return false;
        }

        Directory.Delete(stagingRoot, recursive: true);

        Log.Info(LogColor.Green, $"Done: {tarballPath} ({new FileInfo(tarballPath).Length / 1024.0 / 1024.0:F2} MB)");
        return true;
    }

    private static bool IsElfFile(string path)
    {
        Span<byte> magic = stackalloc byte[4];
        using var stream = File.OpenRead(path);
        if (stream.Read(magic) != magic.Length)
        {
            return false;
        }
        return magic[0] == 0x7F && magic[1] == (byte)'E' && magic[2] == (byte)'L' && magic[3] == (byte)'F';
    }

    // Strip the host toolchain executables in bin/ and capture their debug info
    // into a parallel symbols tarball. Only real ELF files are touched; symlinks
    // (the many clang/lld aliases) are skipped so each binary is processed once.
    // The .a dev archives and per-target runtime libs under lib/<triple>/ keep
    // their debug info untouched. The symbols tarball mirrors the main tarball's
    // layout (bin/<tool>.debug next to bin/<tool>), so extracting it on top of an
    // unpacked toolchain lets a debugger find symbols via the gnu_debuglink.
    private async Task<bool> PackageSymbols(string baseFileName)
    {
        var binDir = _config.InstallDir / "bin";
        if (!Directory.Exists(binDir))
        {
            return true;
        }

        var objcopy = _config.InstallDir / "bin" / "llvm-objcopy";
        if (!objcopy.Exists)
        {
            Log.Error($"ERROR: llvm-objcopy not found at '{objcopy}'; cannot split debug info.");
            return false;
        }

        var stagingRoot = _config.WorkDir / $"{baseFileName}-symbols";
        if (Directory.Exists(stagingRoot))
        {
            Directory.Delete(stagingRoot, recursive: true);
        }

        var stagingBinDir = stagingRoot / baseFileName / "bin";
        Directory.CreateDirectory(stagingBinDir);

        var stripped = 0;
        foreach (var file in Directory.EnumerateFiles(binDir, "*", SearchOption.TopDirectoryOnly))
        {
            if (new FileInfo(file).LinkTarget != null)
            {
                continue;
            }
            if (!IsElfFile(file))
            {
                continue;
            }

            var debugDest = stagingBinDir / $"{Path.GetFileName(file)}.debug";
            var binPath = ((FilePath)file).AsQuotedPath();

            // --only-keep-debug captures the .debug_* sections AND the symbol
            // table into the .debug file; --strip-all then removes both from the
            // shipped binary (~36 MB/binary, vs ~6 MB for --strip-debug, which
            // would leave the symbol table baked in). Nothing is lost: the symbols
            // tarball holds the full debug info + symbol table, recoverable via the
            // gnu_debuglink added below.
            if (await ProcessRunner.Run(objcopy, $"--only-keep-debug {binPath} {debugDest.AsQuotedPath()}") != 0)
            {
                Log.Error($"ERROR: Failed to extract debug info from '{file}'.");
                return false;
            }
            if (await ProcessRunner.Run(objcopy, $"--strip-all {binPath}") != 0)
            {
                Log.Error($"ERROR: Failed to strip '{file}'.");
                return false;
            }
            if (await ProcessRunner.Run(objcopy, $"--add-gnu-debuglink={debugDest.AsQuotedPath()} {binPath}") != 0)
            {
                Log.Error($"ERROR: Failed to add debuglink to '{file}'.");
                return false;
            }
            stripped++;
        }

        if (stripped == 0)
        {
            Directory.Delete(stagingRoot, recursive: true);
            Log.Info("No host binaries with debug info to strip; skipping symbols tarball.");
            return true;
        }

        var tarballName = $"{baseFileName}-symbols.tar.zst";
        var tarballPath = _config.OutputDir / tarballName;

        Log.Info($"Creating {tarballPath} ({stripped} stripped binaries)...");

        var threads = _config.PackageThreads > 0 ? _config.PackageThreads : 0;
        var args = new ArgBuilder()
            .Dash("I", $"zstd -19 -T{threads}", Quoted.Yes)
            .Dash("cf", tarballPath, Quoted.Yes)
            .Dash("C", stagingRoot, Quoted.Yes)
            .Text(baseFileName);

        if (await ProcessRunner.Run("tar", args.Build()) != 0)
        {
            Log.Error("ERROR: Failed to package the symbols tarball.");
            return false;
        }

        Directory.Delete(stagingRoot, recursive: true);

        Log.Info(LogColor.Green, $"Done: {tarballPath} ({new FileInfo(tarballPath).Length / 1024.0 / 1024.0:F2} MB)");
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

        Directory.CreateDirectory(_config.OutputDir);

        // Move the ~600 MB of LLVM/Clang/LLD/LLDB static archives out of the top of
        // lib/ and into a parallel dev-libs tarball. These are dev artifacts for tools
        // that link against LLVM as a library; the main toolchain only ships clang/lld/
        // lldb for compilation. The cross-build phases need them via LLVMExports.cmake,
        // so this happens here at packaging time after all builds are complete. The
        // dev-libs tarball mirrors the main tarball's layout, so extracting it on top of
        // an unpacked toolchain restores lib/*.a. Per-target runtimes under lib/<triple>/
        // and lib/clang/<ver>/lib/<triple>/ stay in the main toolchain.
        if (!await PackageDevLibs(baseFileName))
        {
            return false;
        }

        // Strip the host binaries and split their debug info into a parallel
        // symbols tarball. Done after the dev-libs split so the .a archives are
        // already out of the tree and never get stripped.
        if (!await PackageSymbols(baseFileName))
        {
            return false;
        }

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
