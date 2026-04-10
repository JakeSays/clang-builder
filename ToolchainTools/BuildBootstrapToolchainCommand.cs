using Spectre.Console.Cli;

namespace Std.BuildTools.Clang;

public sealed class BuildBootstrapToolchainCommand : AsyncCommand<BootstrapBuildSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BootstrapBuildSettings settings)
    {
        var workDir = new FilePath(settings.WorkDir!);
        var outputDir = Path.GetFullPath(settings.OutputDir);
        var prebuiltsSourceDir = Path.GetFullPath(settings.PrebuiltsDir);
        var prebuiltsOutputDir = workDir / "prebuilts";

        Log.Info(LogColor.Green, $"Work directory: {workDir}");
        Log.Info(LogColor.Green, $"Output directory: {outputDir}");
        Log.Info(LogColor.Green, $"Prebuilts directory: {prebuiltsSourceDir}");

        try
        {
            using var downloader = new FileDownloader();
            var prepper = new BootstrapBuildPrepper(settings, downloader, workDir, prebuiltsSourceDir, prebuiltsOutputDir);

            var paths = await prepper.Prepare();
            if (paths == null)
            {
                Log.Error("Bootstrap build preparation failed. Aborting.");
                return 1;
            }

            // Ensure sysroot is musl
            if (!File.Exists(Path.Combine(paths.HostSysrootPath, "lib", "ld-musl-x86_64.so.1")) &&
                !File.Exists(Path.Combine(paths.HostSysrootPath, "usr", "lib", "ld-musl-x86_64.so.1")))
            {
                Log.Error("ERROR: Only musl sysroots are supported for bootstrap builds.");
                return 1;
            }

            // Build
            var builder = new BootstrapBuilder(
                llvmVersion: settings.LlvmVersion,
                llvmSrcDir: paths.SrcDir,
                buildDir: paths.BuildDir,
                installDir: paths.InstallDir,
                hostSysroot: paths.HostSysrootPath,
                jobs: settings.Jobs > 0
                    ? settings.Jobs
                    : Environment.ProcessorCount,
                forceReconfigure: settings.ForceReconfigure);

            if (!await builder.Build())
            {
                Log.Error("Bootstrap build failed.");
                return 1;
            }

            // Move install dir to a stage directory with the versioned name for trimming
            var stageDir = Path.Combine(Path.GetDirectoryName(paths.InstallDir)!, $"clang{settings.LlvmVersion}");
            FileUtils.DeleteDirectory(stageDir);
            Directory.Move(paths.InstallDir, stageDir);

            // Trim the installed toolchain
            var packager = new BootstrapPackager(stageDir, outputDir, settings.LlvmVersion, paths.HostSysrootPath);
            Log.Info(LogColor.Green, "Trimming bootstrap Clang for testing and packaging...");
            packager.Trim(stageDir);
            Log.Info(LogColor.Green, "Trimming complete.");

            if (settings.RunTests)
            {
                Log.Info(LogColor.Cyan, "Running bootstrap toolchain tests...");
                var config = new BuildConfiguration
                {
                    Architectures = TargetArch.X64,
                    LlvmVersion = settings.LlvmVersion,
                    WorkDir = workDir,
                    OutputDir = settings.OutputDir,
                    PrebuiltsDir = prebuiltsOutputDir,
                    Jobs = settings.Jobs > 0 ? settings.Jobs : Environment.ProcessorCount,
                    ForceReconfigure = settings.ForceReconfigure,
                    RunTests = settings.RunTests,
                    BuildTargets = BuildTargets.All,
                    BootstrapClangDir = stageDir, // The newly trimmed bootstrap clang is the "toolchain" to test
                    HostSysroot = paths.HostSysrootPath,
                    CmakeModulesDir = "", // Not needed for this test
                    PackageThreads = 0
                };

                var tester = new BootstrapToolchainTester(config);
                if (!await tester.RunTests())
                {
                    Log.Error("Bootstrap tests FAILED. Aborting packaging.");
                    return 1;
                }
            }

            if (settings.BuildOnly)
            {
                Log.Info(LogColor.Green, $"--build-only specified. Bootstrap Clang installed at: {paths.InstallDir}");
                return 0;
            }

            // Package the already trimmed stageDir
            await packager.Package(stageDir);

            return 0;
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
}
