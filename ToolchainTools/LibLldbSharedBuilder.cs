using System.Text.RegularExpressions;

namespace Std.BuildTools.Clang;

public class LibLldbSharedBuilder
{
    private readonly BuildConfiguration _config;
    private readonly FilePath _buildDir;
    private readonly FilePath _sysroot;

    public LibLldbSharedBuilder(BuildConfiguration config)
    {
        _config = config;
        _buildDir = config.BuildDir / "liblldb";
        _sysroot = config.GlibcHostSysroot;
    }

    public async Task<bool> Build()
    {
        Log.Info("--- Building liblldb.so (glibc, separate build tree) ---");

        if (!Directory.Exists(_sysroot))
        {
            Log.Error($"ERROR: Glibc host sysroot not found: {_sysroot}");
            Log.Error("Build the sysroot with: clang-builder make-sysroot --host-x64");
            return false;
        }

        // Use the stage1 compiler we just built.
        var cc = _config.InstallDir / "bin" / "clang";
        var cxx = _config.InstallDir / "bin" / "clang++";
        var ld = _config.InstallDir / "bin" / "ld.lld";

        if (!cc.Exists)
        {
            Log.Error($"ERROR: Stage1 clang not found at {cc}. Stage1 must be built first.");
            return false;
        }

        if (!await Configure(cc, cxx, ld))
        {
            return false;
        }

        if (!await BuildTarget())
        {
            return false;
        }

        var installLibDir = _config.InstallDir / "lib";

        if (!CopyLiblldb(installLibDir))
        {
            return false;
        }

        if (!await CopySharedDeps(installLibDir))
        {
            return false;
        }

        Log.Info(LogColor.Green, "liblldb.so built and installed successfully.");
        return true;
    }

    private async Task<bool> Configure(FilePath cc, FilePath cxx, FilePath ld)
    {
        Directory.CreateDirectory(_buildDir);

        // Executables (tablegen, etc.): static libc++ — avoids runtime dependency
        // on libc++.so.1. TPOFF32 TLS relocations are fine in executables.
        var exeLdFlags = new ArgBuilder()
            .UseLd("lld")
            .NoStdLibCxx()
            .Wl("-Bstatic")
            .Lib("c++")
            .Lib("c++abi")
            .Lib("unwind")
            .Wl("-Bdynamic")
            .Build();

        // Shared libraries (liblldb.so): dynamic libc++ — avoids TPOFF32 TLS
        // relocation errors from static libc++abi.a. CopySharedDeps will copy
        // libc++.so.1 into install/lib/ alongside liblldb.so.
        var soLdFlags = new ArgBuilder()
            .UseLd("lld")
            .Build();

        var args = new ArgBuilder()
            .NinjaGenerator()
            .CmakeSourceDir(_config.SourceDir / "llvm")
            .CmakeBinaryDir(_buildDir)
            .CmakeBuildRelease()
            .Define("CMAKE_INSTALL_PREFIX", _buildDir / "install")
            .Define("LLVM_ENABLE_PROJECTS", "clang;lldb")
            .Define("LLVM_ENABLE_RUNTIMES", "")
            .Define("LLVM_HOST_TRIPLE", "x86_64-linux-gnu")
            .Define("LLVM_DEFAULT_TARGET_TRIPLE", "x86_64-linux-gnu")
            .Define("LLVM_TARGETS_TO_BUILD", "X86;ARM;AArch64;RISCV")
            .DefineQuoted("CMAKE_C_COMPILER", cc)
            .DefineQuoted("CMAKE_CXX_COMPILER", cxx)
            .DefineQuoted("CMAKE_LINKER", ld)
            .DefineQuoted("CMAKE_C_FLAGS", "-fdiagnostics-color=always -fPIC")
            .DefineQuoted("CMAKE_CXX_FLAGS", "-fdiagnostics-color=always -fPIC -stdlib=libc++")
            .DefineQuoted("CMAKE_EXE_LINKER_FLAGS", exeLdFlags)
            .DefineQuoted("CMAKE_SHARED_LINKER_FLAGS", soLdFlags)
            .DefineQuoted("CMAKE_MODULE_LINKER_FLAGS", soLdFlags)
            .Define("CMAKE_SYSROOT", _sysroot)
            .CmakeOn("CMAKE_BUILD_WITH_INSTALL_RPATH")
            .CmakeOn("LLVM_ENABLE_LLD")
            .CmakeOff("LLVM_BUILD_LLVM_DYLIB")
            .CmakeOff("LLVM_LINK_LLVM_DYLIB")
            .CmakeOff("CLANG_LINK_CLANG_DYLIB")
            .CmakeOff("LLVM_BUILD_TOOLS")
            .CmakeOff("LLVM_BUILD_EXAMPLES")
            .CmakeOff("LLVM_INCLUDE_EXAMPLES")
            .CmakeOff("LLVM_BUILD_TESTS")
            .CmakeOff("LLVM_INCLUDE_TESTS")
            .CmakeOff("LLVM_BUILD_BENCHMARKS")
            .CmakeOff("LLVM_INCLUDE_BENCHMARKS")
            .CmakeOff("LLVM_BUILD_DOCS")
            .CmakeOff("LLVM_INCLUDE_DOCS")
            .CmakeOn("LLVM_ENABLE_RTTI")
            .CmakeOn("LLVM_ENABLE_EH")
            .CmakeOff("LLVM_ENABLE_ASSERTIONS")
            .CmakeOn("LLVM_ENABLE_ZLIB")
            .Define("LLVM_ENABLE_ZSTD", "FORCE_ON")
            .CmakeOff("LLDB_ENABLE_LZMA")
            .CmakeOn("LLDB_BUILD_SHARED_LIB")
            .CmakeOn("LLDB_ENABLE_LIBEDIT")
            .CmakeOn("LLDB_ENABLE_CURSES")
            .CmakeOn("LLDB_ENABLE_LIBXML2")
            .CmakeOff("LLDB_ENABLE_PYTHON")
            .CmakeOff("LLDB_ENABLE_LUA")
            .CmakeOff("LLDB_ENABLE_FBSDVMCORE")
            .CmakeOff("LLDB_USE_SYSTEM_DEBUGSERVER")
            .CmakeOff("LLDB_BUILD_FRAMEWORK")
            .CmakeOff("LLDB_INCLUDE_TESTS")
            .CmakeOff("LLDB_TOOL_LLDB_SERVER_BUILD");

        return await Configurator.Configure(_buildDir, _config.ForceReconfigure, args.Build(), "liblldb.so (glibc)");
    }

    private async Task<bool> BuildTarget()
    {
        Log.Info($"Building liblldb target ({_config.Jobs} jobs)...");
        var buildArgs = new ArgBuilder()
            .DashDash("build", _buildDir, Quoted.Yes)
            .DashDash("target", "liblldb")
            .DashDash("parallel", _config.Jobs.ToString())
            .Build();

        var exitCode = await ProcessRunner.Run("cmake", buildArgs);
        if (exitCode != 0)
        {
            Log.Error("ERROR: liblldb.so build failed.");
            return false;
        }

        return true;
    }

    private bool CopyLiblldb(FilePath installLibDir)
    {
        var buildLibDir = _buildDir / "lib";

        var soFiles = Directory.GetFiles(buildLibDir, "liblldb.so*");
        if (soFiles.Length == 0)
        {
            Log.Error($"ERROR: No liblldb.so files found in {buildLibDir}");
            return false;
        }

        Directory.CreateDirectory(installLibDir);

        foreach (var soFile in soFiles)
        {
            var destPath = Path.Combine(installLibDir, Path.GetFileName(soFile));
            File.Copy(soFile, destPath, overwrite: true);
            Log.Info($"  Installed: {destPath}");
        }

        return true;
    }

    /// <summary>
    /// Reads NEEDED entries from liblldb.so and transitively copies all shared
    /// library dependencies from the glibc sysroot into the install lib directory.
    /// </summary>
    private async Task<bool> CopySharedDeps(FilePath installLibDir)
    {
        Log.Info("Resolving shared library dependencies...");

        // Build the list of directories to search for shared libraries.
        // Order matters — earlier directories are preferred.
        var searchDirs = new List<FilePath>();

        // Sysroot multiarch path (Debian puts .so files here).
        var sysrootMultiarch = _sysroot / "usr" / "lib" / "x86_64-linux-gnu";
        if (Directory.Exists(sysrootMultiarch))
        {
            searchDirs.Add(sysrootMultiarch);
        }

        // Sysroot base lib path.
        var sysrootLibDir = _sysroot / "usr" / "lib";
        if (Directory.Exists(sysrootLibDir))
        {
            searchDirs.Add(sysrootLibDir);
        }

        // Stage1 install — libc++, libc++abi, libunwind live here.
        var installMultiarch = _config.InstallDir / "lib" / "x86_64-unknown-linux-gnu";
        if (Directory.Exists(installMultiarch))
        {
            searchDirs.Add(installMultiarch);
        }

        // Build tree lib directory.
        searchDirs.Add(_buildDir / "lib");

        if (searchDirs.Count == 0)
        {
            Log.Error("ERROR: No library search directories found");
            return false;
        }

        var liblldbPath = installLibDir / "liblldb.so";
        if (!liblldbPath.Exists)
        {
            Log.Error($"ERROR: liblldb.so not found at {liblldbPath}");
            return false;
        }

        // Collect all NEEDED deps transitively starting from liblldb.so.
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(liblldbPath);

        while (queue.Count > 0)
        {
            var soPath = queue.Dequeue();
            var needed = await GetNeededLibraries(soPath);

            foreach (var lib in needed)
            {
                if (!visited.Add(lib))
                {
                    continue;
                }

                // Skip core system libs — users will have these already.
                if (IsSystemLibrary(lib))
                {
                    Log.Info($"  Skipping system lib: {lib}");
                    continue;
                }

                string? foundSo = null;
                foreach (var dir in searchDirs)
                {
                    foundSo = FindSharedLibrary(dir, lib);
                    if (foundSo != null)
                    {
                        break;
                    }
                }

                if (foundSo == null)
                {
                    Log.Warning($"  WARNING: dependency '{lib}' not found — skipping");
                    continue;
                }

                var destPath = installLibDir / lib;
                if (destPath.Exists)
                {
                    continue;
                }

                // Resolve the real file (follow symlinks) and copy it under the NEEDED name.
                var realPath = Path.GetFullPath(foundSo);
                File.Copy(realPath, destPath, overwrite: true);
                Log.Info($"  Copied dep: {lib}");

                // Recurse into this dep's own NEEDED entries.
                queue.Enqueue(destPath);
            }
        }

        return true;
    }

    private static bool IsSystemLibrary(string lib)
    {
        // libc, libm, libdl, librt, libpthread, ld-linux — always present on glibc systems.
        return lib.StartsWith("libc.so", StringComparison.Ordinal) ||
               lib.StartsWith("libm.so", StringComparison.Ordinal) ||
               lib.StartsWith("libdl.so", StringComparison.Ordinal) ||
               lib.StartsWith("librt.so", StringComparison.Ordinal) ||
               lib.StartsWith("libpthread.so", StringComparison.Ordinal) ||
               lib.StartsWith("ld-linux", StringComparison.Ordinal) ||
               lib.StartsWith("linux-vdso", StringComparison.Ordinal) ||
               lib.StartsWith("libgcc_s.so", StringComparison.Ordinal) ||
               lib.StartsWith("libstdc++.so", StringComparison.Ordinal);
    }

    private static async Task<List<string>> GetNeededLibraries(string soPath)
    {
        var (exitCode, output) = await ProcessRunner.GetOutput("readelf", $"-d \"{soPath}\"");
        if (exitCode != 0)
        {
            Log.Warning($"  WARNING: readelf failed on {Path.GetFileName(soPath)}");
            return [];
        }

        // Match lines like: 0x0000000000000001 (NEEDED)  Shared library: [libz.so.1]
        var needed = new List<string>();
        foreach (var match in Regex.Matches(output, @"\(NEEDED\)\s+Shared library:\s+\[(.+?)\]").Cast<Match>())
        {
            needed.Add(match.Groups[1].Value);
        }

        return needed;
    }

    private static string? FindSharedLibrary(FilePath libDir, string soName)
    {
        var candidate = libDir / soName;
        if (candidate.Exists || candidate.IsSymLink)
        {
            return candidate;
        }

        return null;
    }
}
