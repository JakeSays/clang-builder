using Microsoft.Extensions.FileSystemGlobbing;

namespace Std.BuildTools.Clang;

public class BootstrapPackager : ToolchainPackager
{
    private readonly string _installDir;
    private readonly string _outputDir;
    private readonly string _llvmVersion;
    private readonly string _hostSysroot;

    public BootstrapPackager(
        string installDir,
        string outputDir,
        string llvmVersion,
        string hostSysroot)
    {
        _installDir = installDir;
        _outputDir = outputDir;
        _llvmVersion = llvmVersion;
        _hostSysroot = hostSysroot;
    }

    public async Task Package(string stageDir)
    {
        // The stageDir is assumed to be already moved and trimmed by MakeBootstrapCommand
        if (!Directory.Exists(stageDir))
        {
            Log.Error($"ERROR: Stage directory '{stageDir}' not found for packaging.");
            throw new DirectoryNotFoundException($"Stage directory '{stageDir}' not found.");
        }

        var tarballPath = Path.Combine(_outputDir, $"bootstrap-clang{_llvmVersion}.tar.xz");
        Log.Info($"Creating {tarballPath}...");

        // Use the system 'tar' command for packaging.
        // -c: Create an archive
        // -J: Use xz compression (equivalent to -a or --xz)
        // -f: Specify archive file name
        // -C: Change to directory before performing operation (stageDir)
        // .: Archive all contents of the current directory (stageDir)
        var args = new ArgBuilder()
            .Dash("cJf", tarballPath, Quoted.Yes)
            .Dash("C", stageDir, Quoted.Yes)
            .Text(".");

        var exitCode = await ProcessRunner.Run("tar", args.Build());
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Failed to create tarball '{tarballPath}' using 'tar'. Exit code: {exitCode}");
        }
        Log.Info(LogColor.Green, $"Done: {tarballPath}");
    }

    public void Trim(string stageDir)
    {
        // --- bin/ ---
        var binDir = Path.Combine(stageDir, "bin");
        if (Directory.Exists(binDir))
        {
            var binMatcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            binMatcher.AddIncludePatterns(new[]
            {
                // Compilers
                "clang", "clang++", "clang-*",
                // Linkers — only the ELF linker is needed; MachO/COFF/Wasm are not
                "lld", "ld.lld",
            });

            foreach (var file in Directory.GetFiles(binDir))
            {
                if (!binMatcher.Match(Path.GetFileName(file)).HasMatches)
                {
                    File.Delete(file);
                }
            }
        }

        // --- lib/ ---
        var libDir = Path.Combine(stageDir, "lib");
        if (Directory.Exists(libDir))
        {
            var libMatcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            libMatcher.AddIncludePatterns(new[]
            {
                "ld-musl-x86_64.so*", "libLLVM.so*", "libLLVM-*.so",
                "clang", "*-linux-*", "*-unknown-linux-*"
            });

            foreach (var entry in Directory.GetFileSystemEntries(libDir))
            {
                if (!libMatcher.Match(Path.GetFileName(entry)).HasMatches)
                {
                    if (Directory.Exists(entry))
                    {
                        Directory.Delete(entry, true);
                    }
                    else
                    {
                        File.Delete(entry);
                    }
                }
            }
        }

        // Delete other top-level dirs
        FileUtils.DeleteDirectory(stageDir, "share");
        FileUtils.DeleteDirectory(stageDir, "libexec");
    }
}
