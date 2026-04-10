using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;


namespace Std.BuildTools.Clang;

public sealed class BootstrapBuildSettings : CommandSettings
{
    [CommandOption("--llvm-version <VERSION>")]
    [Description("LLVM version to build (required)")]
    public required string LlvmVersion { get; init; }

    [CommandOption("--output-dir <dir>")]
    [Description("Where to write the tarball (default: current dir)")]
    [DefaultValue(".")]
    public string OutputDir { get; init; } = ".";

    [CommandOption("--prebuilts-dir <dir>")]
    [Description("Path to the directory containing prebuilt artifacts.")]
    public required string PrebuiltsDir { get; init; }

    [CommandOption("--src-dir <dir>")]
    [Description("LLVM source directory (default: work-dir/llvm-src)")]
    public string? SrcDir { get; init; }

    [CommandOption("--work-dir <dir>")]
    [Description("Working/scratch directory (default: system temp dir)")]
    public string? WorkDir { get; init; }

    [CommandOption("--jobs <N>")]
    [Description("Parallel jobs (default: nproc)")]
    [DefaultValue(0)]
    public int Jobs { get; init; }

    [CommandOption("--keep-work-dir")]
    [Description("Don't delete the work directory when done")]
    [DefaultValue(false)]
    public bool KeepWorkDir { get; init; }

    [CommandOption("--build-only")]
    [Description("Build and install only, skip trim and package")]
    [DefaultValue(false)]
    public bool BuildOnly { get; init; }

    [CommandOption("--force-reconfigure")]
    [Description("Force cmake reconfigure even if build.ninja exists")]
    [DefaultValue(false)]
    public bool ForceReconfigure { get; init; }

    [CommandOption("--run-tests")]
    [Description("Run tests on the built bootstrap toolchain before packaging.")]
    [DefaultValue(false)]
    public bool RunTests { get; init; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrEmpty(LlvmVersion))
        {
            return ValidationResult.Error("--llvm-version is required.");
        }
        if (string.IsNullOrEmpty(WorkDir))
        {
            return ValidationResult.Error("--work-dir is required.");
        }
        return ValidationResult.Success();
    }
}