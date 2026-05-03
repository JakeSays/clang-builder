namespace Std.BuildTools.Clang;

public enum PrebuiltType
{
    BootstrapClang,
    HostSysroot,
    GlibcHostSysroot,
    X64GlibcSysroot,
    X64MuslSysroot,
    Armv7GlibcSysroot,
    Aarch64GlibcSysroot,
    Riscv64GlibcSysroot,
    Armv7MuslSysroot,
    Aarch64MuslSysroot,
    Riscv64MuslSysroot,
    X86GlibcSysroot,
    X86MuslSysroot
}