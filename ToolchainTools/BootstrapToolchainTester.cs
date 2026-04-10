using System.IO;
using System.Threading.Tasks;
using Std.BuildTools.Clang.TestResources;

namespace Std.BuildTools.Clang;

public class BootstrapToolchainTester
{
    private readonly BuildConfiguration _config;
    private readonly string _testBuildDir;

    public BootstrapToolchainTester(BuildConfiguration config)
    {
        _config = config;
        _testBuildDir = _config.WorkDir / "bootstrap-test-build";
    }

    public async Task<bool> RunTests()
    {
        Log.Info("--- Running Bootstrap Toolchain Tests ---");
        FileUtils.DeleteDirectory(_testBuildDir); // Ensure clean test build directory
        Directory.CreateDirectory(_testBuildDir);

        var bootstrapBin = _config.BootstrapClangDir / "bin";
        var clangxx = bootstrapBin / "clang++";
        var lld = bootstrapBin / "ld.lld";

        if (!File.Exists(clangxx))
        {
            Log.Error($"ERROR: Bootstrap clang++ not found at {clangxx}. Cannot run tests.");
            return false;
        }
        if (!File.Exists(lld))
        {
            Log.Error($"ERROR: Bootstrap lld not found at {lld}. Cannot run tests.");
            return false;
        }

        var commonCompileAndLinkFlagsString = new ArgBuilder()
            .DashAssigned("std", "c++17")
            .Dash("O2")
            .Target("x86_64-linux-musl")
            .Sysroot(_config.HostSysroot)
            .ColorAlways()
            .Pic()
            .StdLib("libc++")
            .UseLd("lld")
            .Dash("static")
            .RtLib("compiler-rt")
            .UnwindLib("none")
            .NoStdLibCxx()
            .Wl("-Bstatic") // Start static linking context
            .Lib("c++")
            .Lib("c++abi")
            .Lib("unwind")
            .Lib("c")
            .Wl("-Bstatic") // Ensure static linking context is maintained
            .Build();

        var failures = 0;
        var totalTests = 0;

        foreach (var testName in TestResourcesManager.BootstrapTestNames)
        {
            totalTests++;
            Log.Info(LogColor.Cyan, $"  Running test: {testName}");

            var sourceCode = TestResourcesManager.GetTestSourceCode(TestResourceType.BootstrapToolchain, testName);
            if (string.IsNullOrEmpty(sourceCode))
            {
                Log.Error($"ERROR: Could not load source code for {testName}.");
                failures++;
                continue;
            }

            var cppFilePath = Path.Combine(_testBuildDir, testName);
            var executablePath = Path.Combine(_testBuildDir, Path.GetFileNameWithoutExtension(testName));

            await File.WriteAllTextAsync(cppFilePath, sourceCode);

            var perTestCommandBuilder = new ArgBuilder()
                .Text(commonCompileAndLinkFlagsString) // Add the common flags as a raw string
                .Dash("o", executablePath.Quoted())
                .Text(cppFilePath, Quoted.Yes);

            var (compileExitCode, compileOutput) = await ProcessRunner.GetOutput(clangxx, perTestCommandBuilder.Build(), _testBuildDir);

            if (compileExitCode != 0)
            {
                Log.Error($"  FAIL: {testName} — Compilation failed.");
                Log.Info($"    {compileOutput}");
                failures++;
                continue;
            }
            Log.Info(LogColor.Green, $"  PASS: {testName} — Compiled successfully.");

            // Bootstrap tests are always x86_64-linux-musl, so they can be run natively.
            var (runExitCode, runOutput) = await ProcessRunner.GetOutput(executablePath, "", _testBuildDir);
            if (runExitCode != 0)
            {
                Log.Error($"  FAIL: {testName} — Runtime error (exit {runExitCode}).");
                Log.Info($"    {runOutput}");
                failures++;
            }
            else
            {
                Log.Info(LogColor.Green, $"  PASS: {testName} — Executed successfully.");
            }
        }

        Log.Info("");
        if (failures > 0)
        {
            Log.Error($"Bootstrap tests FAILED: {failures}/{totalTests} tests failed.");
            return false;
        }

        Log.Info(LogColor.Green, $"All {totalTests} bootstrap tests passed.");
        return true;
    }
}
