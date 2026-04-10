namespace Std.BuildTools.Clang;

public record Target(
    TargetArch Arch,
    string Triple,
    string CmakeTriple,
    string ArchName,
    string CmakeArch,
    string GnuTriple,
    FilePath Sysroot,
    string ExtraFlags,
    bool IsMusl = false)
{
    /// <summary>
    /// The filename of the musl dynamic linker for this target, e.g. "ld-musl-x86_64.so.1".
    /// Null when IsMusl is false.
    /// </summary>
    public string? MuslInterpreterName => IsMusl
        ? Arch switch
        {
            TargetArch.X64     => "ld-musl-x86_64.so.1",
            TargetArch.Aarch64 => "ld-musl-aarch64.so.1",
            TargetArch.Armv7   => "ld-musl-armhf.so.1",
            TargetArch.Riscv64 => "ld-musl-riscv64.so.1",
            TargetArch.X86     => "ld-musl-i386.so.1",
            _ => null,
        }
        : null;
}
