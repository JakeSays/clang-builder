using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Std.BuildTools.Clang;

public sealed class MakeSysrootSettings : CommandSettings
{
    [CommandOption("--host")]
    [Description("Build Alpine musl x64 host sysroot (includes static Python)")]
    [DefaultValue(false)]
    public bool Host { get; init; }

    [CommandOption("--host-x64")]
    [Description("Build Debian glibc x64 host sysroot with LLVM build dependencies")]
    [DefaultValue(false)]
    public bool HostX64 { get; init; }

    [CommandOption("--x64")]
    [Description("Build x64 (amd64 bookworm) cross sysroot")]
    [DefaultValue(false)]
    public bool X64 { get; init; }

    [CommandOption("--aarch64")]
    [Description("Build aarch64 (arm64 bookworm) cross sysroot")]
    [DefaultValue(false)]
    public bool Aarch64 { get; init; }

    [CommandOption("--armv7")]
    [Description("Build armv7 (armhf bookworm) cross sysroot")]
    [DefaultValue(false)]
    public bool Armv7 { get; init; }

    [CommandOption("--riscv64")]
    [Description("Build riscv64 (sid) cross sysroot")]
    [DefaultValue(false)]
    public bool Riscv64 { get; init; }

    [CommandOption("--x32")]
    [Description("Build x86 (i386 bookworm) cross sysroot")]
    [DefaultValue(false)]
    public bool X86 { get; init; }

    [CommandOption("--glibc")]
    [Description("Build Debian glibc cross sysroots for the selected arch(es)")]
    [DefaultValue(false)]
    public bool Glibc { get; init; }

    [CommandOption("--musl")]
    [Description("Build Alpine musl cross sysroots for the selected arch(es)")]
    [DefaultValue(false)]
    public bool Musl { get; init; }

    [CommandOption("--output-dir <dir>")]
    [Description("Output directory for the .tar.xz archive(s)")]
    public string OutputDir { get; init; } = null!;

    [CommandOption("--work-dir <dir>")]
    [Description("Temporary working directory (deleted when done)")]
    public string WorkDir { get; init; } = null!;

    [CommandOption("-r|--release <suite>")]
    [Description("Override Debian release for glibc sysroots (e.g. bookworm, sid)")]
    public string? Release { get; init; }

    [CommandOption("-p|--packages <pkg>")]
    [Description("Override base packages for glibc sysroots")]
    public string[]? Packages { get; init; }

    [CommandOption("--keep-work-dir")]
    [Description("Do not delete musl sysroot working directories after archiving")]
    [DefaultValue(false)]
    public bool KeepWorkDir { get; init; }

    [CommandOption("--no-package")]
    [Description("Skip creating tar archives for musl sysroots (implies --keep-work-dir)")]
    [DefaultValue(false)]
    public bool NoPackage { get; init; }

    [CommandOption("--py-version <version>")]
    [Description("Python version to compile into the host sysroot")]
    [DefaultValue("3.12.3")]
    public string PyVersion { get; init; } = "3.12.3";

    /// <summary>Arch keys for glibc (Debian) cross builds.</summary>
    public IEnumerable<string> SelectedGlibcArchs
    {
        get
        {
            if (X64) yield return "x64";
            if (Aarch64) yield return "aarch64";
            if (Armv7) yield return "armv7";
            if (Riscv64) yield return "riscv64";
            if (X86) yield return "x86";
        }
    }

    /// <summary>Arch keys for musl (Alpine) cross builds.</summary>
    public IEnumerable<string> SelectedMuslArchs
    {
        get
        {
            if (X64) yield return "x64";
            if (Aarch64) yield return "aarch64";
            if (Armv7) yield return "armv7";
            if (Riscv64) yield return "riscv64";
            if (X86) yield return "x86";
        }
    }

    public override ValidationResult Validate()
    {
        var hasCrossArch = X64 || Aarch64 || Armv7 || Riscv64 || X86;

        if (!Host && !HostX64 && !hasCrossArch)
        {
            return ValidationResult.Error("Specify at least one target: --host, --host-x64, --x64, --aarch64, --armv7, --riscv64, --x32");
        }

        if (hasCrossArch && !Glibc && !Musl)
        {
            return ValidationResult.Error("Cross-arch targets require --glibc, --musl, or both.");
        }

        if (string.IsNullOrWhiteSpace(OutputDir))
        {
            return ValidationResult.Error("--output-dir is required");
        }

        if (string.IsNullOrWhiteSpace(WorkDir))
        {
            return ValidationResult.Error("--work-dir is required");
        }

        return ValidationResult.Success();
    }
}
