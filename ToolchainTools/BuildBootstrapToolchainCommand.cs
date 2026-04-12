namespace Std.BuildTools.Clang;

public static class BuildBootstrapToolchainCommand
{
    public static async Task<int> Execute(string[] args)
    {
        if (!CommandLineParser.ParseBootstrap(args, out var bootstrapArgs))
        {
            return 1;
        }

        var workDir = new FilePath(bootstrapArgs.WorkDir);
        var outputDir = bootstrapArgs.OutputDir;
        var prebuiltsSourceDir = new FilePath(bootstrapArgs.PrebuiltsDir);
        var prebuiltsOutputDir = workDir / "prebuilts";

        Log.Info(LogColor.Green, $"Work directory: {workDir}");
        Log.Info(LogColor.Green, $"Output directory: {outputDir}");
        Log.Info(LogColor.Green, $"Prebuilts directory: {prebuiltsSourceDir}");

        try
        {
            using var downloader = new FileDownloader();
            var prepper = new BootstrapBuildPrepper(bootstrapArgs, downloader, workDir, prebuiltsSourceDir, prebuiltsOutputDir);

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
                llvmVersion: bootstrapArgs.LlvmVersion,
                llvmSrcDir: paths.SrcDir,
                buildDir: paths.BuildDir,
                installDir: paths.InstallDir,
                hostSysroot: paths.HostSysrootPath,
                jobs: bootstrapArgs.Jobs > 0
                    ? bootstrapArgs.Jobs
                    : Environment.ProcessorCount,
                forceReconfigure: bootstrapArgs.ForceReconfigure);

            if (!await builder.Build())
            {
                Log.Error("Bootstrap build failed.");
                return 1;
            }

            // Move install dir to a stage directory with the versioned name for trimming
            var stageDir = Path.Combine(Path.GetDirectoryName(paths.InstallDir)!, $"clang{bootstrapArgs.LlvmVersion}");
            FileUtils.DeleteDirectory(stageDir);
            Directory.Move(paths.InstallDir, stageDir);

            // Trim the installed toolchain
            var packager = new BootstrapPackager(stageDir, outputDir, bootstrapArgs.LlvmVersion, paths.HostSysrootPath);
            Log.Info(LogColor.Green, "Trimming bootstrap Clang for testing and packaging...");
            packager.Trim(stageDir);
            Log.Info(LogColor.Green, "Trimming complete.");

            if (bootstrapArgs.RunTests)
            {
                Log.Info(LogColor.Cyan, "Running bootstrap toolchain tests...");
                var config = new BuildConfiguration
                {
                    Architectures = TargetArch.X64,
                    LlvmVersion = bootstrapArgs.LlvmVersion,
                    WorkDir = workDir,
                    OutputDir = outputDir,
                    PrebuiltsDir = prebuiltsOutputDir,
                    PrebuiltsSourceDir = prebuiltsSourceDir,
                    Jobs = bootstrapArgs.Jobs > 0 ? bootstrapArgs.Jobs : Environment.ProcessorCount,
                    ForceReconfigure = bootstrapArgs.ForceReconfigure,
                    RunTests = bootstrapArgs.RunTests,
                    BuildTargets = BuildTargets.All,
                    BootstrapClangDir = stageDir,
                    HostSysroot = paths.HostSysrootPath,
                    CmakeModulesDir = workDir / "cmake-modules",
                    PackageThreads = 0
                };

                var tester = new BootstrapToolchainTester(config);
                if (!await tester.RunTests())
                {
                    Log.Error("Bootstrap tests FAILED. Aborting packaging.");
                    return 1;
                }
            }

            if (bootstrapArgs.BuildOnly)
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
            if (!bootstrapArgs.KeepWorkDir)
            {
                Log.Warning($"Cleaning up work directory: {workDir}");
                FileUtils.DeleteDirectory(workDir);
            }
        }
    }
}
