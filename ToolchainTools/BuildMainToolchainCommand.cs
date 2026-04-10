using Spectre.Console.Cli;

namespace Std.BuildTools.Clang;

public sealed class BuildMainToolchainCommand : AsyncCommand<MainBuildSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, MainBuildSettings settings)
    {
        var workDir = new FilePath(settings.WorkDir);
        var prebuiltsSourceDir = new FilePath(settings.PrebuiltsDir);
        var prebuiltsOutputDir = workDir / "prebuilts";

        PrebuiltsUtilities.Initialize(prebuiltsOutputDir);

        Log.Initialize(workDir);

        Log.Info(LogColor.Green, $"Work directory: {workDir}");
        Log.Info(LogColor.Green, $"Prebuilts directory: {prebuiltsSourceDir}");

        try
        {
            var architectures = TargetArch.X64.SetIf(settings.All || settings.BuildX64);
            architectures = TargetArch.Aarch64.AddIf(settings.All || settings.BuildAarch64, architectures);
            architectures = TargetArch.Armv7.AddIf(settings.All || settings.BuildArmv7, architectures);
            architectures = TargetArch.Riscv64.AddIf(settings.All || settings.BuildRiscv64, architectures);
            architectures = TargetArch.X86.AddIf(settings.All || settings.BuildX86, architectures);

            using var downloader = new FileDownloader();

            var prepper = new MainToolchainBuildPrepper(
                architectures,
                settings,
                workDir,
                prebuiltsSourceDir,
                prebuiltsOutputDir,
                downloader);
            var config = prepper.Config;

            TargetManager.Initialize(config);

            if (settings.RunTestsOnly)
            {
                Log.Warning("--run-tests-only specified. Skipping build and packaging.");

                if (config.InstallDir.Exists)
                {
                    return await RunAllTests(config, settings.TestArch)
                        ? 0
                        : 1;
                }

                Log.Error($"ERROR: --run-tests-only requires a completed build in '{workDir}'.");
                Log.Error($"Missing '{config.InstallDir}' or '{config.BootstrapClangDir}'.");
                return 1;
            }
            else // Normal build flow
            {
                if (!await prepper.Prepare())
                {
                    Log.Error("Build preparation failed. Aborting.");
                    return 1;
                }

                var builder = new LlvmCrossBuilder(config);
                if (!await builder.Build())
                {
                    Log.Error("Build failed. Aborting post-build steps.");
                    return 1;
                }

                if (settings.RunTests)
                {
                    if (!await RunAllTests(config, settings.TestArch))
                    {
                        Log.Error("Tests failed. Aborting packaging.");
                        return 1;
                    }
                }

                if (settings.Package)
                {
                    var packager = new MainToolchainPackager(config);
                    if (await packager.Package())
                    {
                        return 0;
                    }

                    Log.Error("ERROR: Failed to package the final toolchain.");
                    return 1;

                }
                else
                {
                    Log.Warning("--package not specified; skipping packaging.");
                }

                return 0;
            }
        }
        finally
        {
            if (!settings.KeepWorkDir)
            {
                Log.Warning($"Cleaning up work directory: {workDir}");
                FileUtils.DeleteDirectory(workDir);
            }
        }
    }

    private static async Task<bool> EnsureClangVersionSet(BuildConfiguration config)
    {
        if (!string.IsNullOrEmpty(config.ClangVersion))
        {
            return true;
        }

        var llvmConfig = config.InstallDir / "bin" / "llvm-config";
        var (verExit, clangVersion) = await ProcessRunner.GetOutput(llvmConfig, "--version");
        if (verExit != 0)
        {
            Log.Error($"ERROR: Failed to get clang version from {llvmConfig}.");
            return false;
        }

        config.ClangVersion = clangVersion;
        Log.Info($"Detected installed clang version: {clangVersion}");
        return true;
    }

    private static async Task<bool> RunAllTests(BuildConfiguration config, TargetArch? testArch)
    {
        if (!await EnsureClangVersionSet(config))
        {
            return false;
        }

        Log.Info(LogColor.Cyan, "Running main toolchain tests...");
        var mainTester = new MainToolchainTester(config, testArch);
        return await mainTester.RunTests();
    }
}
