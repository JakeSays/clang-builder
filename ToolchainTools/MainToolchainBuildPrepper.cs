using System.Text.RegularExpressions;

namespace Std.BuildTools.Clang;

public class MainToolchainBuildPrepper : BuildPrepper
{
    private readonly TargetArch _architectures;
    private readonly MainBuildSettings _settings;

    public BuildConfiguration Config { get; private set; }

    public MainToolchainBuildPrepper(
        TargetArch architectures,
        MainBuildSettings settings,
        FilePath workDir,
        FilePath prebuiltsSourceDir,
        FilePath prebuiltsOutputDir,
        FileDownloader? downloader = null)
        : base(downloader, workDir, prebuiltsSourceDir, prebuiltsOutputDir, settings.KeepWorkDir)
    {
        _architectures = architectures;
        _settings = settings;

        Config = CreateBuildConfiguration();
    }

    public async Task<bool> Prepare()
    {
        Log.Info("--- Preparing Build Environment ---");

        if (!Directory.Exists(PrebuiltsSourceDir))
        {
            Log.Error($"ERROR: Prebuilts directory not found at '{PrebuiltsSourceDir}'");
            return false;
        }

        if (!CheckHostDependencies())
        {
            return false;
        }

        // Check for QEMU if tests are enabled, as they are needed for cross-arch testing.
        if (Config.RunTests)
        {
            Log.Info("Checking for QEMU user-mode binaries for cross-arch testing...");


            if (Config.Architectures.IsSet(TargetArch.Armv7) && !CheckQemuDependency("qemu-armhf"))
            {
                return false;
            }
            if (Config.Architectures.IsSet(TargetArch.Aarch64) && !CheckQemuDependency("qemu-aarch64"))
            {
                return false;
            }
            if (Config.Architectures.IsSet(TargetArch.Riscv64) && !CheckQemuDependency("qemu-riscv64"))
            {
                return false;
            }
            if (Config.Architectures.IsSet(TargetArch.X86) && !CheckQemuDependency("qemu-i386"))
            {
                return false;
            }
        }

        if (!await ExpandPrebuilts())
        {
            return false;
        }

        //var bootstrapDir = PrebuiltsUtilities.GetPrebuiltPath(PrebuiltType.BootstrapClang)!;
        //await PatchBootstrapClang(bootstrapDir); - no longer needed?

        var pythonInfo = DetectPython();
        if (pythonInfo == null)
        {
            Log.Error("ERROR: Python is required for LLDB, but no static Python library was found in the host sysroot.");
            Log.Error("The build cannot continue without Python support for LLDB.");
            return false;
        }
        Config.Python = pythonInfo;

        if (!await FetchLlvmSource(Config.LlvmVersion, Config.SourceDir))
        {
            return false;
        }

        try
        {
            if (!PatchLldbCmakeFiles())
            {
                return false;
            }

            if (!BundlePythonStdlib())
            {
                Log.Error("ERROR: Failed to bundle Python standard library.");
                return false;
            }

            GenerateCustomCmakeModules();
        }
        catch (Exception e)
        {
            Log.Error($"ERROR: Failed to patch or generate CMake files: {e.Message}");
            return false;
        }

        // The original LlvmCrossBuilder normalized triples after Stage1.
        // We can't do that here because the Stage1 compiler doesn't exist yet.
        // The logic is moved to LlvmCrossBuilder, but the principle of preparing
        // as much as possible is maintained.

        Log.Info(LogColor.Green, "Build preparation complete.");
        return true;
    }

    private void GenerateCustomCmakeModules()
    {
        Log.Info("Generating custom CMake modules...");

        if (StampFile.IsPresent(Config.CmakeModulesDir, "modules"))
        {
            Log.Info("  Cmake files already generated.");
            return;
        }

        Directory.CreateDirectory(Config.CmakeModulesDir);

        // --- FindLibXml2.cmake ---
        const string findLibXml2Content = """
# Custom FindLibXml2.cmake — adds liblzma as an interface dep so the lzma
# symbols referenced by Alpine's static libxml2.a are resolved at link time.
set(LIBXML2_INCLUDE_DIR "${CMAKE_SYSROOT}/usr/include/libxml2" CACHE PATH "")
set(LIBXML2_LIBRARY     "${CMAKE_SYSROOT}/usr/lib/libxml2.a"   CACHE FILEPATH "")
set(LIBXML2_INCLUDE_DIRS "${LIBXML2_INCLUDE_DIR}")
set(LIBXML2_LIBRARIES    "${LIBXML2_LIBRARY}")
set(LIBXML2_DEFINITIONS  "")

if(EXISTS "${LIBXML2_INCLUDE_DIR}/libxml/xmlversion.h")
  file(STRINGS "${LIBXML2_INCLUDE_DIR}/libxml/xmlversion.h" _ver
       REGEX "^#define[ \t]+LIBXML_DOTTED_VERSION[ \t]+\"[^\"]+\"")
  string(REGEX REPLACE ".*\"([^\"]+)\".*" "\\1" LIBXML2_VERSION_STRING "${_ver}")
  unset(_ver)
endif()

include(FindPackageHandleStandardArgs)
find_package_handle_standard_args(LibXml2
  REQUIRED_VARS LIBXML2_LIBRARY LIBXML2_INCLUDE_DIR
  VERSION_VAR   LIBXML2_VERSION_STRING)
mark_as_advanced(LIBXML2_INCLUDE_DIR LIBXML2_LIBRARY)

if(LibXml2_FOUND AND NOT TARGET LibXml2::LibXml2)
  add_library(LibXml2::LibXml2 STATIC IMPORTED)
  set_target_properties(LibXml2::LibXml2 PROPERTIES
    IMPORTED_LOCATION             "${CMAKE_SYSROOT}/usr/lib/libxml2.a"
    INTERFACE_INCLUDE_DIRECTORIES "${CMAKE_SYSROOT}/usr/include/libxml2"
    INTERFACE_LINK_LIBRARIES      "${CMAKE_SYSROOT}/usr/lib/liblzma.a")
endif()
""";

        var findLibXml2Path = Config.CmakeModulesDir / "FindLibXml2.cmake";
        File.WriteAllText(findLibXml2Path, findLibXml2Content);
        Log.Info($"  Generated: {findLibXml2Path}");

        // --- FindPython3.cmake ---
        const string findPython3Content = """
# Wrapper: delegate to cmake's real FindPython3, then staple on the transitive
# static library deps that our bundled libpython3.12.a extension modules need.
include("${CMAKE_ROOT}/Modules/FindPython3.cmake")

if(Python3_FOUND AND CMAKE_SYSROOT)
  set(_py_extra_libs "${CMAKE_SYSROOT}/usr/lib/libssl.a" "${CMAKE_SYSROOT}/usr/lib/libcrypto.a" "${CMAKE_SYSROOT}/usr/lib/libuuid.a" "${CMAKE_SYSROOT}/usr/lib/libsqlite3.a" "${CMAKE_SYSROOT}/usr/lib/libbz2.a" "${CMAKE_SYSROOT}/usr/lib/liblzma.a" "${CMAKE_SYSROOT}/usr/lib/libreadline.a" "${CMAKE_SYSROOT}/usr/lib/libncurses.a" "${CMAKE_SYSROOT}/usr/lib/libffi.a" "${CMAKE_SYSROOT}/usr/lib/libexpat.a" "${CMAKE_SYSROOT}/usr/lib/libmpdec.a" "${CMAKE_SYSROOT}/usr/lib/libgdbm_compat.a" "${CMAKE_SYSROOT}/usr/lib/libgdbm.a" "${CMAKE_SYSROOT}/usr/lib/libz.a")
  foreach(_lib IN LISTS _py_extra_libs)
    if(EXISTS "${_lib}")
      list(APPEND Python3_LIBRARIES "${_lib}")
      if(TARGET Python3::Python)
        set_property(TARGET Python3::Python APPEND PROPERTY INTERFACE_LINK_LIBRARIES "${_lib}")
      endif()
    endif()
  endforeach()
endif()
""";

        var findPython3Path = Config.CmakeModulesDir / "FindPython3.cmake";
        File.WriteAllText(findPython3Path, findPython3Content);
        Log.Info($"  Generated: {findPython3Path}");

        StampFile.Create(Config.CmakeModulesDir, "modules");
    }

    private bool PatchLldbCmakeFiles()
    {
        Log.Info("Patching LLDB CMake files to force static linking...");

        if (StampFile.IsPresent(Config.SourceDir, "patch"))
        {
            Log.Info("  Patching already done.");
            return true;
        }

        var apiCmakePath = Config.SourceDir / "lldb" / "source" / "API" / "CMakeLists.txt";
        if (!PatchFile(apiCmakePath, "add_lldb_library(liblldb SHARED", "add_lldb_library(liblldb STATIC"))
        {
            return false;
        }

        var intelFeaturesCmakePath = Config.SourceDir / "lldb" / "tools" / "intel-features" / "CMakeLists.txt";
        if (!PatchFile(
            intelFeaturesCmakePath, "add_lldb_library(lldbIntelFeatures SHARED",
            "add_lldb_library(lldbIntelFeatures STATIC"))
        {
            return false;
        }

        StampFile.Create(Config.SourceDir, "patch");

        return true;
    }

    private static bool PatchFile(FilePath path, string oldValue, string newValue)
    {
        try
        {
            if (!path.Exists)
            {
                Log.Error($"ERROR: Could not find file to patch: {path}");
                return false;
            }

            var content = File.ReadAllText(path);
            var result = content.Patch(oldValue, newValue);

            if (!result)
            {
                Log.Error($"ERROR: Patching file '{path}' failed. The text to replace was not found:\n  '{oldValue}'");
                return false;
            }

            File.WriteAllText(path, (string)result);
            Log.Info($"  Patched: {path}");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"ERROR: Could not patch: {path}: {ex.Message}");
            return false;
        }
    }

    private bool BundlePythonStdlib()
    {
        Log.Info("Bundling Python standard library for LLDB...");

        if (StampFile.IsPresent(Config.WorkDir, "bundle-python"))
        {
            Log.Info("  Bundling already done.");
            return true;
        }

        // Determine Python source and destination paths
        var pythonSrcDir = Config.HostSysroot / "usr" / "lib" / "python3.12";
        var pythonDestDir = Config.InstallDir / "lib" / "python3.12";

        if (!Directory.Exists(pythonSrcDir))
        {
            Log.Error($"ERROR: Python stdlib not found at '{pythonSrcDir}'");
            return false;
        }

        Log.Info($"  Copying from '{pythonSrcDir}' to '{pythonDestDir}'...");
        try
        {
            CopyPythonFiles(pythonSrcDir, pythonDestDir);
            CleanupPythonFiles(pythonDestDir);
        }
        catch (Exception e)
        {
            Log.Error($"ERROR: Failed to bundle Python standard library: {e.Message}");
            return false;
        }

        StampFile.Create(Config.WorkDir, "bundle-python");

        Log.Info($"  Python stdlib bundled: {pythonDestDir}");

        return true;
    }

    /// <summary>
    /// Recursively copies .py files and directories, excluding specific ones.
    /// Mimics `rsync -a --include="*.py" --include="*/" --exclude="*"` with specific excludes.
    /// </summary>
    private static void CopyPythonFiles(FilePath sourceDir, FilePath destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var entry in Directory.EnumerateFileSystemEntries(sourceDir, "*", SearchOption.TopDirectoryOnly))
        {
            var entryName = Path.GetFileName(entry);
            var destEntryPath = destDir / entryName;

            // Exclude specific directories as per build-stage1-host.sh
            if (entryName.Equals("test", StringComparison.OrdinalIgnoreCase) ||
                entryName.Equals("tests", StringComparison.OrdinalIgnoreCase) ||
                entryName.Equals("__pycache__", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Directory.Exists(entry))
            {
                CopyPythonFiles(entry, destEntryPath); // Recursive call for directories
            }
            else if (File.Exists(entry) && entryName.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(entry, destEntryPath, true);
            }
        }
    }

    /// <summary>
    /// Deletes .pyc files and __pycache__ directories.
    /// </summary>
    private void CleanupPythonFiles(string targetDir)
    {
        Log.Info("  Cleaning up Python .pyc and __pycache__ files...");
        foreach (var dir in Directory.EnumerateDirectories(targetDir, "__pycache__", SearchOption.AllDirectories))
        {
            FileUtils.DeleteDirectory(dir);
        }
        foreach (var file in Directory.EnumerateFiles(targetDir, "*.pyc", SearchOption.AllDirectories))
        {
            File.Delete(file);
        }
    }

    private async Task<bool> ExpandPrebuilts()
    {
        var typesToExpand = new List<PrebuiltType> { PrebuiltType.BootstrapClang, PrebuiltType.HostSysroot };
        if (Config.Architectures.IsSet(TargetArch.X64))
        {
            typesToExpand.Add(PrebuiltType.X64GlibcSysroot);
            typesToExpand.Add(PrebuiltType.X64MuslSysroot);
        }
        if (Config.Architectures.IsSet(TargetArch.Armv7))
        {
            typesToExpand.Add(PrebuiltType.Armv7GlibcSysroot);
            typesToExpand.Add(PrebuiltType.Armv7MuslSysroot);
        }
        if (Config.Architectures.IsSet(TargetArch.Aarch64))
        {
            typesToExpand.Add(PrebuiltType.Aarch64GlibcSysroot);
            typesToExpand.Add(PrebuiltType.Aarch64MuslSysroot);
        }
        if (Config.Architectures.IsSet(TargetArch.Riscv64))
        {
            typesToExpand.Add(PrebuiltType.Riscv64GlibcSysroot);
            typesToExpand.Add(PrebuiltType.Riscv64MuslSysroot);
        }
        if (Config.Architectures.IsSet(TargetArch.X86))
        {
            typesToExpand.Add(PrebuiltType.X86GlibcSysroot);
            typesToExpand.Add(PrebuiltType.X86MuslSysroot);
        }

        return await PrebuiltsUtilities.ExpandPrebuilts(PrebuiltsSourceDir, PrebuiltsOutputDir, Downloader!, typesToExpand.ToArray());
    }

    private async Task PatchBootstrapClang(FilePath bootstrapDir)
    {
        var muslLoader = bootstrapDir / "lib" / "ld-musl-x86_64.so.1";
        if (muslLoader.Exists)
        {
            Log.Warning("Musl bootstrap detected — patching ELF interpreters...");
            var muslInterpDest = bootstrapDir / "bin" / "ld-musl-x86_64.so.1";
            File.Copy(muslLoader, muslInterpDest, true);

            // File.Copy does not preserve execute permissions on Linux. The loader must be executable.
            File.SetUnixFileMode(muslInterpDest,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

            foreach (var file in Directory.EnumerateFiles(bootstrapDir / "bin"))
            {
                var exitCode = await ProcessRunner.Run("patchelf", $"--set-interpreter {muslInterpDest.AsQuotedPath()} {file.Quoted()}", WorkDir);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"Failed to patch ELF interpreter for '{file}'. Patchelf exited with code {exitCode}.");
                }
            }
        }
    }

    private static BuildTargets ParseBuildTargets(string? target) =>
        target?.ToLowerInvariant() switch
        {
            "stage1"      => BuildTargets.Stage1,
            "rt-glibc"    => BuildTargets.RtGlibc,
            "rt-musl"     => BuildTargets.RtMusl,
            "libcxx-glibc" => BuildTargets.LibcxxGlibc,
            "libcxx-musl" => BuildTargets.LibcxxMusl,
            "san-glibc"   => BuildTargets.SanGlibc,
            "san-musl"    => BuildTargets.SanMusl,
            "lldb-server" => BuildTargets.LldbServer,
            _             => BuildTargets.All,
        };

    private BuildConfiguration CreateBuildConfiguration()
    {
        return new BuildConfiguration
        {
            Architectures = _architectures,
            BuildTargets = ParseBuildTargets(_settings.BuildTarget),
            LlvmVersion = _settings.LlvmVersion,
            WorkDir = WorkDir,
            OutputDir = _settings.OutputDir,
            PrebuiltsDir = PrebuiltsOutputDir,
            Jobs = _settings.Jobs > 0 ? _settings.Jobs : Environment.ProcessorCount,
            ForceReconfigure = _settings is { RunTestsOnly: false, ForceReconfigure: true },
            RunTests = _settings.RunTests || _settings.RunTestsOnly,
            PackageThreads = _settings.Threads,
            BootstrapClangDir = PrebuiltsUtilities.GetPrebuiltPath(PrebuiltType.BootstrapClang),
            HostSysroot = PrebuiltsUtilities.GetPrebuiltPath(PrebuiltType.HostSysroot),
            CmakeModulesDir = WorkDir / "cmake-modules",
            X64Sysroot = MakeRoot(TargetArch.X64),
            X64MuslSysroot = MakeRoot(TargetArch.X64, true),
            Armv7Sysroot = MakeRoot(TargetArch.Armv7),
            Aarch64Sysroot = MakeRoot(TargetArch.Aarch64),
            Riscv64Sysroot = MakeRoot(TargetArch.Riscv64),
            Armv7MuslSysroot = MakeRoot(TargetArch.Armv7, true),
            Aarch64MuslSysroot = MakeRoot(TargetArch.Aarch64, true),
            Riscv64MuslSysroot = MakeRoot(TargetArch.Riscv64, true),
            X86Sysroot = MakeRoot(TargetArch.X86),
            X86MuslSysroot = MakeRoot(TargetArch.X86, true)
        };

        FilePath? MakeRoot(TargetArch arch, bool musl = false) =>
            _architectures.IsSet(arch)
                ? PrebuiltsUtilities.GetSysrootDir(arch, musl)
                : (FilePath?) null;

    }

    private bool CheckQemuDependency(string qemuBinary)
    {
        if (FileUtils.ExistsOnPath(qemuBinary))
        {
            return true;
        }

        Log.Warning($"WARNING: '{qemuBinary}' not found on PATH. Cross-architecture runtime tests for this target will be skipped.");
        return false;

    }

    private PythonInfo? DetectPython()
    {
        var includeBaseDir = Config.HostSysroot / "usr" / "include";
        var libBaseDir = Config.HostSysroot / "usr" / "lib";

        if (!Directory.Exists(includeBaseDir) || !Directory.Exists(libBaseDir))
        {
            return null;
        }

        // Order by name descending to prefer newer versions (e.g., python3.12 over python3.11)
        foreach (var dir in Directory.EnumerateDirectories(includeBaseDir, "python*").OrderByDescending(p => p))
        {
            var dirName = Path.GetFileName(dir); // e.g., "python3.12"
            var match = Regex.Match(dirName, @"python(\d+\.\d+)");
            if (!match.Success)
            {
                continue;
            }

            var version = match.Groups[1].Value; // e.g., "3.12"
            var libPath = libBaseDir / $"libpython{version}.a";

            if (libPath.Exists)
            {
                Log.Info(LogColor.Green, $"Detected Python {version} in sysroot.");
                return new PythonInfo(version, dir, libPath);
            }
        }

        return null;
    }
}
