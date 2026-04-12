namespace Std.BuildTools.Clang;

public record SysrootArgs(
    string OutputDir,
    string WorkDir,
    bool Host,
    bool HostX64,
    bool Glibc,
    bool Musl,
    TargetArch EnabledArchs,
    string? Release,
    string[]? Packages,
    bool KeepWorkDir,
    bool NoPackage,
    string PyVersion)
{
    public IEnumerable<string> SelectedGlibcArchs
    {
        get
        {
            if (EnabledArchs.IsSet(TargetArch.X64)) yield return "x64";
            if (EnabledArchs.IsSet(TargetArch.Aarch64)) yield return "aarch64";
            if (EnabledArchs.IsSet(TargetArch.Armv7)) yield return "armv7";
            if (EnabledArchs.IsSet(TargetArch.Riscv64)) yield return "riscv64";
            if (EnabledArchs.IsSet(TargetArch.X86)) yield return "x86";
        }
    }

    public IEnumerable<string> SelectedMuslArchs
    {
        get
        {
            if (EnabledArchs.IsSet(TargetArch.X64)) yield return "x64";
            if (EnabledArchs.IsSet(TargetArch.Aarch64)) yield return "aarch64";
            if (EnabledArchs.IsSet(TargetArch.Armv7)) yield return "armv7";
            if (EnabledArchs.IsSet(TargetArch.Riscv64)) yield return "riscv64";
            if (EnabledArchs.IsSet(TargetArch.X86)) yield return "x86";
        }
    }
}
