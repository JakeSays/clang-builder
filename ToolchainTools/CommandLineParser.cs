namespace Std.BuildTools.Clang;

public static class CommandLineParser
{
    public static bool ParseBuild(string[] args, out BuildConfiguration config)
    {
        config = null!;

        if (args.Length > 0 && (args[0] == "--help" || args[0] == "-h"))
        {
            PrintBuildUsage();
            return false;
        }

        string? llvmVersion = null;
        string? workDirRaw = null;
        string? prebuiltsDir = null;
        var outputDir = ".";
        var jobs = 0;
        var targetArchs = TargetArch.None;
        var keepWorkDir = false;
        var forceReconfigure = false;
        var runTests = false;
        var runTestsOnly = false;
        string? buildTarget = null;
        TargetArch? testArch = null;
        var package = false;
        var threads = 0;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--llvm-version":
                    llvmVersion = NextArg(args, ref i, "--llvm-version");
                    if (llvmVersion == null)
                    {
                        return false;
                    }
                    break;
                case "--work-dir":
                    workDirRaw = NextArg(args, ref i, "--work-dir");
                    if (workDirRaw == null)
                    {
                        return false;
                    }
                    break;
                case "--prebuilts-dir":
                    prebuiltsDir = NextArg(args, ref i, "--prebuilts-dir");
                    if (prebuiltsDir == null)
                    {
                        return false;
                    }
                    break;
                case "--output-dir":
                    outputDir = NextArg(args, ref i, "--output-dir") ?? outputDir;
                    break;
                case "--jobs":
                {
                    var val = NextArg(args, ref i, "--jobs");
                    if (val == null || !int.TryParse(val, out jobs))
                    {
                        Log.Error("--jobs requires an integer value.");
                        return false;
                    }
                    break;
                }
                case "--all":
                    targetArchs |= TargetArch.All;
                    break;
                case "--x64":
                    targetArchs |= TargetArch.X64;
                    break;
                case "--armv7":
                    targetArchs |= TargetArch.Armv7;
                    break;
                case "--aarch64":
                    targetArchs |= TargetArch.Aarch64;
                    break;
                case "--riscv64":
                    targetArchs |= TargetArch.Riscv64;
                    break;
                case "--x32":
                    targetArchs |= TargetArch.X86;
                    break;
                case "--keep-work-dir":
                    keepWorkDir = true;
                    break;
                case "--force-reconfigure":
                    forceReconfigure = true;
                    break;
                case "--run-tests":
                    runTests = true;
                    break;
                case "--run-tests-only":
                    runTestsOnly = true;
                    break;
                case "--build-target":
                    buildTarget = NextArg(args, ref i, "--build-target");
                    if (buildTarget == null)
                    {
                        return false;
                    }
                    break;
                case "--test-arch":
                {
                    var val = NextArg(args, ref i, "--test-arch");
                    if (val == null)
                    {
                        return false;
                    }
                    testArch = val.ToLowerInvariant() switch
                    {
                        "x64" => TargetArch.X64,
                        "aarch64" => TargetArch.Aarch64,
                        "armv7" => TargetArch.Armv7,
                        "riscv64" => TargetArch.Riscv64,
                        "x86" or "x32" => TargetArch.X86,
                        _ => null
                    };
                    if (testArch == null)
                    {
                        Log.Error($"Unknown --test-arch '{val}'. Valid: x64, aarch64, armv7, riscv64, x32");
                        return false;
                    }
                    break;
                }
                case "--package":
                    package = true;
                    break;
                case "--threads":
                {
                    var val = NextArg(args, ref i, "--threads");
                    if (val == null || !int.TryParse(val, out threads))
                    {
                        Log.Error("--threads requires an integer value.");
                        return false;
                    }
                    break;
                }
                default:
                    Log.Error($"Unknown option '{args[i]}'.");
                    PrintBuildUsage();
                    return false;
            }
        }

        if (string.IsNullOrEmpty(llvmVersion))
        {
            Log.Error("--llvm-version is required.");
            PrintBuildUsage();
            return false;
        }

        if (string.IsNullOrEmpty(workDirRaw))
        {
            Log.Error("--work-dir is required.");
            PrintBuildUsage();
            return false;
        }

        if (string.IsNullOrEmpty(prebuiltsDir))
        {
            Log.Error("--prebuilts-dir is required.");
            PrintBuildUsage();
            return false;
        }

        if (targetArchs == TargetArch.None)
        {
            Log.Error("No targets specified. Use --x64, --armv7, --aarch64, --riscv64, --x32, or --all.");
            PrintBuildUsage();
            return false;
        }

        if (threads > 0 && !package)
        {
            Log.Error("--threads requires --package.");
            return false;
        }

        if (!Directory.Exists(prebuiltsDir))
        {
            Log.Error($"Prebuilts directory '{prebuiltsDir}' does not exist.");
            return false;
        }

        var buildTargets = ParseBuildTargets(buildTarget);
        if (buildTargets == BuildTargets.None)
        {
            Log.Error($"Unknown --build-target '{buildTarget}'. Valid: stage1, rt-glibc, rt-musl, libcxx-glibc, libcxx-musl, san-glibc, san-musl, lldb-server, liblldb, all");
            return false;
        }

        var workDir = new FilePath(Path.GetFullPath(workDirRaw));
        var prebuiltsSourceDir = new FilePath(Path.GetFullPath(prebuiltsDir));
        var prebuiltsOutputDir = workDir / "prebuilts";

        FilePath? Sysroot(TargetArch arch, string subDir) =>
            targetArchs.IsSet(arch) ? prebuiltsOutputDir / subDir : (FilePath?)null;

        config = new BuildConfiguration
        {
            Architectures = targetArchs,
            BuildTargets = buildTargets,
            LlvmVersion = llvmVersion,
            WorkDir = workDir,
            OutputDir = Path.GetFullPath(outputDir),
            PrebuiltsDir = prebuiltsOutputDir,
            PrebuiltsSourceDir = prebuiltsSourceDir,
            Jobs = jobs > 0 ? jobs : Environment.ProcessorCount,
            ForceReconfigure = !runTestsOnly && forceReconfigure,
            RunTests = runTests || runTestsOnly,
            PackageThreads = threads,
            BootstrapClangDir = prebuiltsOutputDir / "bootstrap-clang",
            HostSysroot = prebuiltsOutputDir / "host",
            GlibcHostSysroot = prebuiltsOutputDir / "glibc-host",
            CmakeModulesDir = workDir / "cmake-modules",
            X64Sysroot = Sysroot(TargetArch.X64, "x64"),
            X64MuslSysroot = Sysroot(TargetArch.X64, "x64-musl"),
            Armv7Sysroot = Sysroot(TargetArch.Armv7, "armv7"),
            Armv7MuslSysroot = Sysroot(TargetArch.Armv7, "armv7-musl"),
            Aarch64Sysroot = Sysroot(TargetArch.Aarch64, "aarch64"),
            Aarch64MuslSysroot = Sysroot(TargetArch.Aarch64, "aarch64-musl"),
            Riscv64Sysroot = Sysroot(TargetArch.Riscv64, "riscv64"),
            Riscv64MuslSysroot = Sysroot(TargetArch.Riscv64, "riscv64-musl"),
            X86Sysroot = Sysroot(TargetArch.X86, "x86"),
            X86MuslSysroot = Sysroot(TargetArch.X86, "x86-musl"),
            KeepWorkDir = keepWorkDir,
            Package = package,
            RunTestsOnly = runTestsOnly,
            TestArch = testArch,
        };

        return true;
    }

    public static bool ParseBootstrap(string[] args, out BootstrapArgs bootstrapArgs)
    {
        bootstrapArgs = null!;

        if (args.Length > 0 && (args[0] == "--help" || args[0] == "-h"))
        {
            PrintBootstrapUsage();
            return false;
        }

        string? llvmVersion = null;
        string? prebuiltsDir = null;
        var outputDir = ".";
        string? workDir = null;
        string? srcDir = null;
        var jobs = 0;
        var keepWorkDir = false;
        var buildOnly = false;
        var forceReconfigure = false;
        var runTests = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--llvm-version":
                    llvmVersion = NextArg(args, ref i, "--llvm-version");
                    if (llvmVersion == null)
                    {
                        return false;
                    }
                    break;
                case "--prebuilts-dir":
                    prebuiltsDir = NextArg(args, ref i, "--prebuilts-dir");
                    if (prebuiltsDir == null)
                    {
                        return false;
                    }
                    break;
                case "--output-dir":
                    outputDir = NextArg(args, ref i, "--output-dir") ?? outputDir;
                    break;
                case "--work-dir":
                    workDir = NextArg(args, ref i, "--work-dir");
                    if (workDir == null)
                    {
                        return false;
                    }
                    break;
                case "--src-dir":
                    srcDir = NextArg(args, ref i, "--src-dir");
                    if (srcDir == null)
                    {
                        return false;
                    }
                    break;
                case "--jobs":
                {
                    var val = NextArg(args, ref i, "--jobs");
                    if (val == null || !int.TryParse(val, out jobs))
                    {
                        Log.Error("--jobs requires an integer value.");
                        return false;
                    }
                    break;
                }
                case "--keep-work-dir":
                    keepWorkDir = true;
                    break;
                case "--build-only":
                    buildOnly = true;
                    break;
                case "--force-reconfigure":
                    forceReconfigure = true;
                    break;
                case "--run-tests":
                    runTests = true;
                    break;
                default:
                    Log.Error($"Unknown option '{args[i]}'.");
                    PrintBootstrapUsage();
                    return false;
            }
        }

        if (string.IsNullOrEmpty(llvmVersion))
        {
            Log.Error("--llvm-version is required.");
            PrintBootstrapUsage();
            return false;
        }

        if (string.IsNullOrEmpty(workDir))
        {
            Log.Error("--work-dir is required.");
            PrintBootstrapUsage();
            return false;
        }

        if (string.IsNullOrEmpty(prebuiltsDir))
        {
            Log.Error("--prebuilts-dir is required.");
            PrintBootstrapUsage();
            return false;
        }

        bootstrapArgs = new BootstrapArgs(
            LlvmVersion: llvmVersion,
            PrebuiltsDir: Path.GetFullPath(prebuiltsDir),
            OutputDir: Path.GetFullPath(outputDir),
            WorkDir: Path.GetFullPath(workDir),
            SrcDir: srcDir != null ? Path.GetFullPath(srcDir) : null,
            Jobs: jobs,
            KeepWorkDir: keepWorkDir,
            BuildOnly: buildOnly,
            ForceReconfigure: forceReconfigure,
            RunTests: runTests);

        return true;
    }

    public static bool ParseSysroot(string[] args, out SysrootArgs sysrootArgs)
    {
        sysrootArgs = null!;

        if (args.Length > 0 && (args[0] == "--help" || args[0] == "-h"))
        {
            PrintSysrootUsage();
            return false;
        }

        string? outputDir = null;
        string? workDir = null;
        var host = false;
        var hostX64 = false;
        var glibc = false;
        var musl = false;
        var enabledArchs = TargetArch.None;
        string? release = null;
        string[]? packages = null;
        var keepWorkDir = false;
        var noPackage = false;
        var pyVersion = "3.12.3";

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--host":
                    host = true;
                    break;
                case "--host-x64":
                    hostX64 = true;
                    break;
                case "--x64":
                    enabledArchs |= TargetArch.X64;
                    break;
                case "--aarch64":
                    enabledArchs |= TargetArch.Aarch64;
                    break;
                case "--armv7":
                    enabledArchs |= TargetArch.Armv7;
                    break;
                case "--riscv64":
                    enabledArchs |= TargetArch.Riscv64;
                    break;
                case "--x32":
                    enabledArchs |= TargetArch.X86;
                    break;
                case "--glibc":
                    glibc = true;
                    break;
                case "--musl":
                    musl = true;
                    break;
                case "--output-dir":
                    outputDir = NextArg(args, ref i, "--output-dir");
                    if (outputDir == null)
                    {
                        return false;
                    }
                    break;
                case "--work-dir":
                    workDir = NextArg(args, ref i, "--work-dir");
                    if (workDir == null)
                    {
                        return false;
                    }
                    break;
                case "-r" or "--release":
                    release = NextArg(args, ref i, "--release");
                    if (release == null)
                    {
                        return false;
                    }
                    break;
                case "-p" or "--packages":
                {
                    var val = NextArg(args, ref i, "--packages");
                    if (val == null)
                    {
                        return false;
                    }
                    packages = val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    break;
                }
                case "--keep-work-dir":
                    keepWorkDir = true;
                    break;
                case "--no-package":
                    noPackage = true;
                    break;
                case "--py-version":
                    pyVersion = NextArg(args, ref i, "--py-version") ?? pyVersion;
                    break;
                default:
                    Log.Error($"Unknown option '{args[i]}'.");
                    PrintSysrootUsage();
                    return false;
            }
        }

        var hasCrossArch = enabledArchs != TargetArch.None;

        if (!host && !hostX64 && !hasCrossArch)
        {
            Log.Error("Specify at least one target: --host, --host-x64, --x64, --aarch64, --armv7, --riscv64, --x32");
            PrintSysrootUsage();
            return false;
        }

        if (hasCrossArch && !glibc && !musl)
        {
            Log.Error("Cross-arch targets require --glibc, --musl, or both.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(outputDir))
        {
            Log.Error("--output-dir is required.");
            PrintSysrootUsage();
            return false;
        }

        if (string.IsNullOrWhiteSpace(workDir))
        {
            Log.Error("--work-dir is required.");
            PrintSysrootUsage();
            return false;
        }

        sysrootArgs = new SysrootArgs(
            OutputDir: Path.GetFullPath(outputDir),
            WorkDir: Path.GetFullPath(workDir),
            Host: host,
            HostX64: hostX64,
            Glibc: glibc,
            Musl: musl,
            EnabledArchs: enabledArchs,
            Release: release,
            Packages: packages,
            KeepWorkDir: keepWorkDir,
            NoPackage: noPackage,
            PyVersion: pyVersion);

        return true;
    }

    private static string? NextArg(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length)
        {
            Log.Error($"{flag} requires a value.");
            return null;
        }

        return args[++i];
    }

    private static BuildTargets ParseBuildTargets(string? target) =>
        target?.ToLowerInvariant() switch
        {
            null or "all" => BuildTargets.All,
            "stage1" => BuildTargets.Stage1,
            "rt-glibc" => BuildTargets.RtGlibc,
            "rt-musl" => BuildTargets.RtMusl,
            "libcxx-glibc" => BuildTargets.LibcxxGlibc,
            "libcxx-musl" => BuildTargets.LibcxxMusl,
            "san-glibc" => BuildTargets.SanGlibc,
            "san-musl" => BuildTargets.SanMusl,
            "lldb-server" => BuildTargets.LldbServer,
            "liblldb" => BuildTargets.LibLldb,
            _ => BuildTargets.None,
        };

    private static void PrintBuildUsage()
    {
        Log.Info("""
            Usage: clang-builder build [options]

            Options:
              --llvm-version <ver>     LLVM version to build (e.g. 22.1.2) [required]
              --work-dir <dir>         Working/scratch directory [required]
              --prebuilts-dir <dir>    Path to prebuilt artifacts directory [required]
              --output-dir <dir>       Output directory for tarball (default: .)
              --jobs <n>               Parallel jobs (default: nproc)
              --all                    Build all targets
              --x64                    Build x86_64 target
              --armv7                  Build armv7 target
              --aarch64                Build aarch64 target
              --riscv64                Build riscv64 target
              --x32                    Build x86 (i686) target
              --keep-work-dir          Do not delete work directory when done
              --force-reconfigure      Force cmake reconfigure
              --run-tests              Run tests before packaging
              --run-tests-only         Only run tests on an existing build
              --build-target <target>  Build specific target only (stage1, rt-glibc, rt-musl,
                                       libcxx-glibc, libcxx-musl, san-glibc, san-musl, lldb-server, all)
              --test-arch <arch>       Only test this arch (x64, aarch64, armv7, riscv64, x32)
              --package                Package the toolchain after build
              --threads <n>            zstd compression threads (requires --package, default: 0 = auto)
            """);
    }

    private static void PrintBootstrapUsage()
    {
        Log.Info("""
            Usage: clang-builder build-bootstrap [options]

            Options:
              --llvm-version <ver>     LLVM version to build (e.g. 22.1.2) [required]
              --work-dir <dir>         Working/scratch directory [required]
              --prebuilts-dir <dir>    Path to prebuilt artifacts directory [required]
              --output-dir <dir>       Output directory for tarball (default: .)
              --src-dir <dir>          LLVM source directory (default: work-dir/llvm-src)
              --jobs <n>               Parallel jobs (default: nproc)
              --keep-work-dir          Do not delete work directory when done
              --build-only             Build and install only, skip trim and package
              --force-reconfigure      Force cmake reconfigure
              --run-tests              Run tests on the built bootstrap toolchain
            """);
    }

    private static void PrintSysrootUsage()
    {
        Log.Info("""
            Usage: clang-builder make-sysroot [options]

            Options:
              --output-dir <dir>    Output directory for archives [required]
              --work-dir <dir>      Temporary working directory [required]
              --host                Build Alpine musl x64 host sysroot
              --host-x64            Build Debian glibc x64 host sysroot
              --x64                 Build x64 cross sysroot
              --aarch64             Build aarch64 cross sysroot
              --armv7               Build armv7 cross sysroot
              --riscv64             Build riscv64 cross sysroot
              --x32                 Build x86 (i686) cross sysroot
              --glibc               Build Debian glibc cross sysroots
              --musl                Build Alpine musl cross sysroots
              -r, --release <suite> Override Debian release (e.g. bookworm, sid)
              -p, --packages <pkgs> Override base packages (comma-separated)
              --keep-work-dir       Do not delete musl working directories
              --no-package          Skip creating tar archives (implies --keep-work-dir)
              --py-version <ver>    Python version for host sysroot (default: 3.12.3)
            """);
    }
}
