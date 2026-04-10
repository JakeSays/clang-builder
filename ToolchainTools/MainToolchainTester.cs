using System.IO;
using System.Threading.Tasks;
using Std.BuildTools.Clang.TestResources;
using System.Linq;
using System.Text.RegularExpressions;
using Std.BuildTools.Clang.Testing;


namespace Std.BuildTools.Clang;

public class MainToolchainTester
{
    private readonly BuildConfiguration _config;
    private readonly TargetArch? _testArch;
    private readonly FilePath _testBuildDir;
    private int _failures;
    private int _totalCompilations;
    private int _totalRuntimeTests;

    public MainToolchainTester(BuildConfiguration config, TargetArch? testArch = null)
    {
        _config = config;
        _testArch = testArch;
        _testBuildDir = _config.WorkDir / "main-toolchain-test-build";
        _totalRuntimeTests = 0;
        _failures = 0;
        _totalCompilations = 0;
    }

    public async Task<bool> RunTests()
    {
        Log.Info("--- Running Main Toolchain Tests ---");
        FileUtils.DeleteDirectory(_testBuildDir); // Ensure clean test build directory
        Directory.CreateDirectory(_testBuildDir);

        var clangxx = _config.InstallDir / "bin" / "clang++";
        var lld = _config.InstallDir / "bin" / "lld"; // Assuming lld is installed in bin

        if (!File.Exists(clangxx))
        {
            Log.Error($"ERROR: Main toolchain clang++ not found at {clangxx}. Cannot run tests.");
            return false;
        }
        if (!File.Exists(lld))
        {
            Log.Error($"ERROR: Main toolchain lld not found at {lld}. Cannot run tests.");
            return false;
        }

        var targets = _testArch == null
            ? TargetManager.AllTargets
            : TargetManager.AllTargets.Where(t => t.Arch == _testArch).ToList();

        foreach (var testDef in TestManager.Tests)
        {
            Log.Info(LogColor.Cyan, $"  Running test: {testDef.FileName}");

            var sourceCode = TestResourcesManager.GetTestSourceCode(TestResourceType.MainToolchain, testDef.FileName);
            if (string.IsNullOrEmpty(sourceCode))
            {
                Log.Error($"ERROR: Could not load source code for {testDef.FileName}.");
                _failures++;
                continue;
            }

            // Determine C++ standard from filename (e.g., "cpp17-hello.cpp" -> "c++17")
            var match = Regex.Match(testDef.FileName, @"^(cpp\d{2})-");
            if (!match.Success)
            {
                Log.Error($"ERROR: Invalid test file name. Test file names must be prefixed with the c++ version");
                _failures++;
                continue;
            }

            var cppStandard = match.Groups[1].Value.Replace("cpp", "c++"); // "cpp17" -> "c++17"

            var testBaseName = Path.GetFileNameWithoutExtension(testDef.FileName);

            foreach (var target in targets)
            {
                Log.Info(LogColor.Yellow, $"    Targeting: {target.Triple} ({(target.IsMusl ? "musl" : "glibc")})");

                await RunTest(
                    istStatic: true,
                    target,
                    testDef,
                    testBaseName,
                    sourceCode,
                    cppStandard,
                    clangxx);

                await RunTest(
                    istStatic: false,
                    target,
                    testDef,
                    testBaseName,
                    sourceCode,
                    cppStandard,
                    clangxx);
            }
        }

        await RunLldbServerSmokeChecks();

        Log.BlankLine();
        if (_failures > 0)
        {
            Log.Error($"Main toolchain tests FAILED: {_failures} failure(s) across {_totalCompilations} compilations and {_totalRuntimeTests} runtime tests.");
            return false;
        }

        Log.Info(LogColor.Green, $"All {_totalCompilations} main toolchain compilations and {_totalRuntimeTests} runtime tests passed.");
        return true;
    }

    private string? FindGlibcNormalizedTriple(Target target)
    {
        if (_config.NormalizedTriples.TryGetValue(target.CmakeTriple, out var triple))
        {
            return triple;
        }

        var libDir = _config.InstallDir / "lib";
        if (!libDir.Exists)
        {
            return null;
        }

        return Directory.GetDirectories(libDir)
            .Select(Path.GetFileName)
            .FirstOrDefault(d => d != null && d.StartsWith(target.ArchName));
    }

    private string? FindMuslNormalizedTriple(Target target)
    {
        if (_config.NormalizedTriples.TryGetValue(target.CmakeTriple, out var triple))
        {
            return triple;
        }

        // Fallback for --run-tests-only: scan the installed lib-musl/lib dir
        var libDir = _config.InstallDir / "lib-musl" / "lib";
        if (!libDir.Exists)
        {
            return null;
        }

        return Directory.GetDirectories(libDir)
            .Select(Path.GetFileName)
            .FirstOrDefault(d => d != null && d.StartsWith(target.ArchName));
    }

    private async Task RunLldbServerSmokeChecks()
    {
        Log.Info(LogColor.Cyan, "  Smoke-checking lldb-server binaries...");

        var expectedVersion = $"lldb version {_config.LlvmVersion}";

        var targets = _testArch == null
            ? TargetManager.GetLldbServerTargets()
            : TargetManager.GetLldbServerTargets().Where(t => t.Arch == _testArch).ToList();

        foreach (var target in targets)
        {
            var archLinux = $"{target.ArchName}-linux";
            var serverBin = _config.InstallDir / "bin" / archLinux / "lldb-server";
            if (!serverBin.Exists)
            {
                Log.Error($"    FAIL: lldb-server {archLinux} — binary not found at {serverBin}");
                _failures++;
                continue;
            }

            int exitCode;
            string output;

            if (target.Arch == TargetArch.X64)
            {
                (exitCode, output) = await ProcessRunner.GetOutput(serverBin, "version");
            }
            else if (QemuRunner.IsSupported(target.Arch))
            {
                (exitCode, output) = await QemuRunner.Run(target.Arch, serverBin, target.Sysroot, _testBuildDir, "version");
            }
            else
            {
                Log.Error($"    FAIL: lldb-server {archLinux} — no QEMU available for {target.ArchName}");
                _failures++;
                continue;
            }

            var firstLine = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
            if (exitCode == 0 && firstLine.Contains(expectedVersion))
            {
                Log.Info(LogColor.Green, $"    PASS: lldb-server {archLinux} — {firstLine}");
            }
            else
            {
                Log.Error($"    FAIL: lldb-server {archLinux} — expected '{expectedVersion}', got '{firstLine}' (exit {exitCode})");
                _failures++;
            }
        }
    }

    private async Task RunTest(
        bool istStatic,
        Target target,
        TestDefinition testDef,
        string testBaseName,
        string sourceCode,
        string cppStandard,
        FilePath clangxx)
    {
        var name = istStatic
            ? "static"
            : "shared";
        var libcType = target.IsMusl
            ? "musl"
            : "glibc";

        var buildLabel = $"{libcType}-{name}";

        if (testDef.ShouldSkip(target.Arch, target.IsMusl, istStatic, out var skipReason))
        {
            Log.Info($"      Skipping: {testDef.FileName} ({buildLabel}) — {skipReason}");
            return;
        }

        _totalCompilations++;

        Log.Info(LogColor.Blue, $"      Building variant: {buildLabel}");

        var targetTestBuildDir = _testBuildDir / target.Triple / name;
        Directory.CreateDirectory(targetTestBuildDir);

        var cppFilePath = targetTestBuildDir / testDef.FileName;
        var executablePath = targetTestBuildDir / testBaseName;

        await File.WriteAllTextAsync(cppFilePath, sourceCode);

        // Musl libc++ headers live under lib-musl/include/<triple>/c++/v1 and need
        // to be spelled out explicitly; -nostdinc++ suppresses the default glibc path.
        var libCxxIncludePrefix = target.IsMusl
            ? _config.InstallDir / "lib-musl" / "include"
            : _config.InstallDir / "include";

        var muslNormalizedTriple = target.IsMusl ? FindMuslNormalizedTriple(target) : null;
        var muslTripleInclude = muslNormalizedTriple != null
            ? (string?)(_config.InstallDir / "lib-musl" / "include" / muslNormalizedTriple / "c++" / "v1")
            : null;
        var muslLibDir = muslNormalizedTriple != null
            ? (string?)(_config.InstallDir / "lib-musl" / "lib" / muslNormalizedTriple)
            : null;

        var glibcNormalizedTriple = !target.IsMusl ? FindGlibcNormalizedTriple(target) : null;
        var glibcLibDir = glibcNormalizedTriple != null
            ? (string?)(_config.InstallDir / "lib" / glibcNormalizedTriple)
            : null;

        // Musl dynamic linker: cross sysroot crt files don't embed an interpreter, so we
        // must pass --dynamic-linker explicitly with the absolute path into the sysroot.
        var muslInterpreter = (target.IsMusl && !istStatic && target.MuslInterpreterName != null)
            ? (string?)(target.Sysroot / "usr" / "lib" / target.MuslInterpreterName)
            : null;

        // Common compile and link flags for the main toolchain
        var commonCompileAndLinkFlagsBuilder = new ArgBuilder()
            .DashAssigned("std", cppStandard)
            .Dash("O2")
            .Target(target.Triple)
            .Sysroot(target.Sysroot)
            .ColorAlways()
            .UseLd("lld")
            .StdLib("libc++")
            .NoStdIncCxxIf(target.IsMusl)
            .ISystemIf(muslTripleInclude != null, muslTripleInclude!)
            .ISystemIf(target.IsMusl, libCxxIncludePrefix / "c++" / "v1")
            .DefineIf(target.IsMusl, "_LIBCPP_PROVIDES_DEFAULT_RUNE_TABLE")
            .LibPathIf(muslLibDir != null, muslLibDir!)
            // Embed rpath so dynamic binaries can find libc++.so at runtime.
            .WlIf(!istStatic && muslLibDir != null, $"-rpath,{muslLibDir}")
            .WlIf(!istStatic && glibcLibDir != null, $"-rpath,{glibcLibDir}")
            // Embed the musl interpreter path from the sysroot so the binary runs on any host
            // that has the sysroot available (cross crt files don't set PT_INTERP).
            .WlIf(muslInterpreter != null, $"--dynamic-linker,{muslInterpreter}")
            .RtLib("compiler-rt")
            .UnwindLib("libunwind")
            .WlIf(istStatic, "-Bstatic", "-lc++", "-lc++abi", "-lunwind")
            .Text(target.ExtraFlags);

        var perTestCommandArgs = new ArgBuilder()
            .Text(commonCompileAndLinkFlagsBuilder.Build())
            .DashIf(istStatic, "static")
            .Dash("o", executablePath.AsQuotedPath())
            .Text(cppFilePath, Quoted.Yes)
            .Build();

        var (compileExitCode, compileOutput) = await ProcessRunner.GetOutput(clangxx, perTestCommandArgs, targetTestBuildDir);

        if (compileExitCode != 0)
        {
            Log.Error($"      FAIL: {testDef.FileName} ({buildLabel}) — Compilation failed.");
            Log.Info($"        {compileOutput}");
            _failures++;
            return;
        }
        Log.Info(LogColor.Green, $"      PASS: {testDef.FileName} ({buildLabel}) — Compiled successfully.");

        // x64: always run natively. The musl interpreter is embedded from the sysroot so
        // musl dynamic binaries work on glibc hosts too.
        if (target.Arch == TargetArch.X64)
        {
            _totalRuntimeTests++;
            var (runExitCode, runOutput) = await ProcessRunner.GetOutput(executablePath, "", targetTestBuildDir);
            if (runExitCode != 0)
            {
                Log.Error($"      FAIL: {testDef.FileName} ({buildLabel}) — Runtime error (exit {runExitCode}).");
                Log.Info($"        {runOutput}");
                _failures++;
                return;
            }

            Log.Info(LogColor.Green, $"      PASS: {testDef.FileName} ({buildLabel}) — Executed successfully.");
            return;
        }

        if (QemuRunner.IsSupported(target.Arch))
        {
            _totalRuntimeTests++;
            var (runExitCode, runOutput) = await QemuRunner.Run(target.Arch, executablePath, target.Sysroot, targetTestBuildDir);
            if (runExitCode != 0)
            {
                Log.Error($"      FAIL: {testDef.FileName} ({buildLabel}) — QEMU runtime error (exit {runExitCode}).");
                Log.Info($"        {runOutput}");
                _failures++;
                return;
            }

            Log.Info(LogColor.Green, $"      PASS: {testDef.FileName} ({buildLabel}) — Executed successfully via QEMU.");
            return;
        }

        Log.Error($"      FAIL: runtime test for {buildLabel} (QEMU not found for {target.ArchName}).");
    }
}
