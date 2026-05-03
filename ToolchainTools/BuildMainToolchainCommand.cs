namespace Std.BuildTools.Clang;

public static class BuildMainToolchainCommand
{
    public static async Task<int> Execute(string[] args)
    {
        if (!CommandLineParser.ParseBuild(args, out var config))
        {
            return 1;
        }

        PrebuiltsUtilities.Initialize(config.PrebuiltsDir);

        Log.Initialize(config.WorkDir);

        Log.Info(LogColor.Green, $"Work directory: {config.WorkDir}");
        Log.Info(LogColor.Green, $"Prebuilts directory: {config.PrebuiltsSourceDir}");

        try
        {
            using var downloader = new FileDownloader();

            var prepper = new MainToolchainBuildPrepper(config, downloader);

            TargetManager.Initialize(config);

            if (config.RunTestsOnly)
            {
                Log.Warning("--run-tests-only specified. Skipping build and packaging.");

                if (config.InstallDir.Exists)
                {
                    return await RunAllTests(config, config.TestArch)
                        ? 0
                        : 1;
                }

                Log.Error($"ERROR: --run-tests-only requires a completed build in '{config.WorkDir}'.");
                Log.Error($"Missing '{config.InstallDir}' or '{config.BootstrapClangDir}'.");
                return 1;
            }

            if (config.PackageOnly)
            {
                Log.Warning("--package-only specified. Skipping build; packaging existing install dir.");

                if (!config.InstallDir.Exists)
                {
                    Log.Error($"ERROR: --package-only requires an existing install at '{config.InstallDir}'.");
                    return 1;
                }

                if (!await EnsureClangVersionSet(config))
                {
                    return 1;
                }

                var packager = new MainToolchainPackager(config);
                if (await packager.Package())
                {
                    return 0;
                }

                Log.Error("ERROR: Failed to package the toolchain.");
                return 1;
            }

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

            if (config.RunTests)
            {
                if (!await RunAllTests(config, config.TestArch))
                {
                    Log.Error("Tests failed. Aborting packaging.");
                    return 1;
                }
            }

            if (config.Package)
            {
                var packager = new MainToolchainPackager(config);
                if (await packager.Package())
                {
                    return 0;
                }

                Log.Error("ERROR: Failed to package the final toolchain.");
                return 1;
            }

            Log.Warning("--package not specified; skipping packaging.");
            return 0;
        }
        finally
        {
            if (!config.KeepWorkDir)
            {
                Log.Warning($"Cleaning up work directory: {config.WorkDir}");
                FileUtils.DeleteDirectory(config.WorkDir);
            }
        }
    }

    private static Task<bool> EnsureClangVersionSet(BuildConfiguration config)
    {
        if (string.IsNullOrEmpty(config.ClangVersion))
        {
            config.ClangVersion = config.LlvmVersion;
        }
        return Task.FromResult(true);
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
