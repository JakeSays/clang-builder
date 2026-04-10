using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;


namespace Std.BuildTools.Clang;

public sealed class MainBuildSettings : CommandSettings
{
    [CommandOption("--llvm-version <VERSION>")]
    [Description("LLVM version to build (e.g. 19.1.0)")]
    public required string LlvmVersion { get; init; }

    [CommandOption("--all")]
    [Description("Build all targets (x64, armv7, aarch64, riscv64, x86)")]
    [DefaultValue(false)]
    public bool All { get; init; }

    [CommandOption("--x64")]
    [DefaultValue(false)]
    public bool BuildX64 { get; init; }

    [CommandOption("--armv7")]
    [DefaultValue(false)]
    public bool BuildArmv7 { get; init; }

    [CommandOption("--aarch64")]
    [DefaultValue(false)]
    public bool BuildAarch64 { get; init; }

    [CommandOption("--riscv64")]
    [DefaultValue(false)]
    public bool BuildRiscv64 { get; init; }

    [CommandOption("--x32")]
    [DefaultValue(false)]
    public bool BuildX86 { get; init; }

    [CommandOption("--prebuilts-dir <dir>")]
    [Description("Path to the directory containing prebuilt artifacts.")]
    public required string PrebuiltsDir { get; init; }

    [CommandOption("--output-dir")]
    [DefaultValue(".")]
    public string OutputDir { get; init; } = ".";

    [CommandOption("--work-dir")]
    public required string WorkDir { get; init; }

    [CommandOption("--jobs")]
    [DefaultValue(0)]
    public int Jobs { get; init; }

    [CommandOption("--keep-work-dir")]
    [DefaultValue(false)]
    public bool KeepWorkDir { get; init; }

    [CommandOption("--force-reconfigure")]
    [DefaultValue(false)]
    public bool ForceReconfigure { get; init; }

    [CommandOption("--run-tests")]
    [Description("Run toolchain tests before packaging; skip package on failure")]
    [DefaultValue(false)]
    public bool RunTests { get; init; } // This property is for the normal build flow

    [CommandOption("--run-tests-only")]
    [Description("Assume a completed build in work-dir and only run tests, skipping build and packaging.")]
    [DefaultValue(false)]
    public bool RunTestsOnly { get; init; } // This property is for the test-only flow

    [CommandOption("--build-target <target>")]
    [Description("Specific build target: stage1, rt-glibc, rt-musl, libcxx-glibc, libcxx-musl, san-glibc, san-musl, lldb-server, all (default: all)")]
    public string? BuildTarget { get; init; }

    [CommandOption("--test-arch <arch>")]
    [Description("Only test this architecture (x64, aarch64, armv7, riscv64). Tests all architectures if not specified.")]
    public TargetArch? TestArch { get; init; }

    [CommandOption("--package")]
    [Description("Package the toolchain after a successful build")]
    [DefaultValue(false)]
    public bool Package { get; init; }

    [CommandOption("--threads <count>")]
    [Description("Number of threads for zstd compression during packaging (default: 0 = auto)")]
    [DefaultValue(0)]
    public int Threads { get; init; }

    public override ValidationResult Validate()
    {
        if (!All && !BuildX64 && !BuildArmv7 && !BuildAarch64 && !BuildRiscv64 && !BuildX86)
        {
            return ValidationResult.Error("No targets specified. Use --x64, --armv7, --aarch64, --riscv64, --x32, or --all.");
        }

        if (RunTestsOnly && string.IsNullOrEmpty(WorkDir))
        {
            return ValidationResult.Error("--run-tests-only requires --work-dir to be specified.");
        }
        if (string.IsNullOrEmpty(WorkDir))
        {
            return ValidationResult.Error("--work-dir is required.");
        }

        if (string.IsNullOrEmpty(PrebuiltsDir))
        {
            return ValidationResult.Error("--prebuilts-dir is required.");
        }

        if (BuildTarget != null)
        {
            var valid = new[] { "stage1", "rt-glibc", "rt-musl", "libcxx-glibc", "libcxx-musl", "san-glibc", "san-musl", "lldb-server", "all" };
            if (!valid.Contains(BuildTarget.ToLowerInvariant()))
            {
                return ValidationResult.Error($"Unknown --build-target '{BuildTarget}'. Valid: {string.Join(", ", valid)}");
            }
        }

        if (Threads > 0 && !Package)
        {
            return ValidationResult.Error("--threads requires --package.");
        }

        return !Directory.Exists(PrebuiltsDir)
            ? ValidationResult.Error($"Prebuilts directory {PrebuiltsDir} does not exist or is not a directory.")
            : ValidationResult.Success();
    }
}
