namespace Std.BuildTools.Clang;

public class LlvmCrossBuilder
{
    private readonly BuildConfiguration _config;

    public LlvmCrossBuilder(BuildConfiguration config)
    {
        _config = config;
    }

    public async Task<bool> Build()
    {
        var bt = _config.BuildTargets;
        var targets = TargetManager.AllTargets;

        if (bt.IsSet(BuildTargets.Stage1))
        {
            var stage1Builder = new Stage1HostBuilder(_config);
            if (!await stage1Builder.Build())
            {
                Log.Error("ERROR: Stage 1 host build failed.");
                return false;
            }
        }

        // Normalize triples whenever any post-stage1 step is needed.
        const BuildTargets postStage1 = BuildTargets.RtGlibc | BuildTargets.RtMusl |
                                        BuildTargets.LibcxxGlibc | BuildTargets.LibcxxMusl |
                                        BuildTargets.SanGlibc | BuildTargets.SanMusl |
                                        BuildTargets.LldbServer;
        if (bt.IsSet(postStage1))
        {
            // If stage1 was skipped, ClangVersion hasn't been set yet — read it from the existing install.
            if (string.IsNullOrEmpty(_config.ClangVersion))
            {
                var llvmConfig = _config.InstallDir / "bin" / "llvm-config";
                var (verExit, clangVersion) = await ProcessRunner.GetOutput(llvmConfig, "--version");
                if (verExit != 0)
                {
                    Log.Error($"ERROR: Failed to get clang version from {llvmConfig}. Is stage1 built?");
                    return false;
                }
                _config.ClangVersion = clangVersion;
                Log.Info($"Detected installed clang version: {clangVersion}");
            }

            var hostClang = _config.InstallDir / "bin" / "clang";
            foreach (var target in targets)
            {
                foreach (var triple in new[] { target.Triple, target.CmakeTriple }.Distinct())
                {
                    if (_config.NormalizedTriples.ContainsKey(triple))
                    {
                        continue;
                    }

                    var (exitCode, normalizedTriple) = await ProcessRunner.GetOutput(
                        hostClang, $"--target={triple} -print-target-triple");
                    if (exitCode != 0)
                    {
                        Log.Error($"ERROR: Failed to get normalized triple for {triple}");
                        return false;
                    }

                    _config.NormalizedTriples[triple] = normalizedTriple;
                    Log.Info($"Normalized {triple} -> {normalizedTriple}");
                }
            }
        }

        if (bt.IsSet(BuildTargets.RtGlibc | BuildTargets.RtMusl))
        {
            Log.Info(LogColor.Cyan, "Building compiler-rt builtins...");
            foreach (var target in targets)
            {
                if (target.IsMusl ? !bt.IsSet(BuildTargets.RtMusl) : !bt.IsSet(BuildTargets.RtGlibc))
                {
                    continue;
                }

                var builder = new CompilerRtBuilder(_config, target);
                if (!await builder.Build(CompilerRtMode.Builtins))
                {
                    return false;
                }
            }
        }

        if (bt.IsSet(BuildTargets.LibcxxGlibc | BuildTargets.LibcxxMusl))
        {
            Log.Info(LogColor.Cyan, "Building libc++...");
            foreach (var target in targets)
            {
                if (target.IsMusl ? !bt.IsSet(BuildTargets.LibcxxMusl) : !bt.IsSet(BuildTargets.LibcxxGlibc))
                {
                    continue;
                }

                var builder = new LibCxxBuilder(_config, target);
                if (!await builder.Build())
                {
                    return false;
                }
            }
        }

        if (bt.IsSet(BuildTargets.SanGlibc | BuildTargets.SanMusl))
        {
            Log.Info(LogColor.Cyan, "Building compiler-rt sanitizers...");
            foreach (var target in targets)
            {
                if (target.IsMusl ? !bt.IsSet(BuildTargets.SanMusl) : !bt.IsSet(BuildTargets.SanGlibc))
                {
                    continue;
                }

                var builder = new CompilerRtBuilder(_config, target);
                if (!await builder.Build(CompilerRtMode.Sanitizers))
                {
                    return false;
                }
            }
        }

        if (bt.IsSet(BuildTargets.LldbServer))
        {
            Log.Info(LogColor.Cyan, "Building lldb-server...");
            foreach (var target in TargetManager.GetLldbServerTargets())
            {
                var builder = new LldbServerBuilder(_config, target);
                if (!await builder.Build())
                {
                    return false;
                }
            }
        }

        // liblldb.so is built last — it uses a separate build tree against the glibc
        // host sysroot, so it doesn't interfere with the musl-based static builds above.
        if (bt.IsSet(BuildTargets.LibLldb))
        {
            Log.Info(LogColor.Cyan, "Building liblldb.so...");
            var liblldbBuilder = new LibLldbSharedBuilder(_config);
            if (!await liblldbBuilder.Build())
            {
                Log.Error("ERROR: liblldb.so build failed.");
                return false;
            }
        }

        if (_config.RunTests)
        {
            Log.Warning("TODO: Implement and run toolchain tests based on tools/tests/toolchain/run-tests.sh");
        }

        Log.Info(LogColor.Green, "All build steps completed successfully.");
        return true;
    }
}
