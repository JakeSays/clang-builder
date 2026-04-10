using System;
using System.Collections.Generic;
using System.Linq;

namespace Std.BuildTools.Clang;

public static class TargetManager
{
    private record TargetInfo(
        TargetArch Arch,
        string Triple,
        string CmakeArch,
        string GnuTriple,
        string ExtraFlags,
        bool IsMusl,
        string? MuslCmakeTriple = null);

    private static readonly List<TargetInfo> AllTargetInfos;
    public static readonly List<Target> AllTargets = [];

    static TargetManager()
    {
        AllTargetInfos =
        [
            new TargetInfo(
                TargetArch.X64, "x86_64-linux-gnu", "X86", "x86_64-linux-gnu", "-march=x86-64",
                false),
            new TargetInfo(
                TargetArch.Armv7, "armv7-linux-gnueabihf", "ARM", "arm-linux-gnueabihf",
                "-march=armv7-a -mfpu=neon -mfloat-abi=hard", false),
            new TargetInfo(
                TargetArch.Aarch64, "aarch64-linux-gnu", "AArch64", "aarch64-linux-gnu", "",
                false),
            new TargetInfo(
                TargetArch.Riscv64, "riscv64-linux-gnu", "RISCV", "riscv64-linux-gnu", "",
                false),
            new TargetInfo(
                TargetArch.X86, "i686-linux-gnu", "X86", "i386-linux-gnu", "-march=i686",
                false),

            // Musl host target
            new TargetInfo(
                TargetArch.X64, "x86_64-linux-musl", "X86", "x86_64-linux-gnu", "-march=x86-64", true),

            // Musl targets — compilation uses the same glibc-equivalent triple as the non-musl
            // targets (musl-ness comes from the sysroot). MuslCmakeTriple is the musl-specific
            // triple used for CMAKE_C_COMPILER_TARGET so compiler-rt and libc++ install into a
            // separate musl subdirectory instead of colliding with the glibc install.
            new TargetInfo(
                TargetArch.Armv7, "armv7-linux-gnueabihf", "ARM", "arm-linux-gnueabihf",
                "-march=armv7-a -mfpu=neon -mfloat-abi=hard -D_LIBCPP_PROVIDES_DEFAULT_RUNE_TABLE",
                true, "armv7-linux-musleabihf"),
            new TargetInfo(
                TargetArch.Aarch64, "aarch64-linux-gnu", "AArch64", "aarch64-linux-gnu",
                "-D_LIBCPP_PROVIDES_DEFAULT_RUNE_TABLE",
                true, "aarch64-linux-musl"),
            new TargetInfo(
                TargetArch.Riscv64, "riscv64-linux-gnu", "RISCV", "riscv64-linux-gnu",
                "-D_LIBCPP_PROVIDES_DEFAULT_RUNE_TABLE",
                true, "riscv64-linux-musl"),
            new TargetInfo(
                TargetArch.X86, "i686-linux-musl", "X86", "i686-linux-gnu", "-march=i686",
                true, "i686-linux-musl")
        ];
    }

    public static void Initialize(BuildConfiguration config)
    {
        if (AllTargets.Count > 0)
        {
            throw  new InvalidOperationException("TargetManager has already been initialized");
        }

        AddTarget(TargetArch.X64, false, config.X64Sysroot);
        AddTarget(TargetArch.Armv7, false, config.Armv7Sysroot);
        AddTarget(TargetArch.Aarch64, false, config.Aarch64Sysroot);
        AddTarget(TargetArch.Riscv64, false, config.Riscv64Sysroot);

        AddTarget(TargetArch.X64, true, config.X64MuslSysroot);
        AddTarget(TargetArch.Armv7, true, config.Armv7MuslSysroot);
        AddTarget(TargetArch.Aarch64, true, config.Aarch64MuslSysroot);
        AddTarget(TargetArch.Riscv64, true, config.Riscv64MuslSysroot);
        AddTarget(TargetArch.X86, false, config.X86Sysroot);
        AddTarget(TargetArch.X86, true, config.X86MuslSysroot);

        static void AddTarget(TargetArch arch, bool isMusl, FilePath? sysroot)
        {
            if (sysroot == null)
            {
                return;
            }
            var info = AllTargetInfos.FirstOrDefault(i => i.Arch == arch && i.IsMusl == isMusl);
            if (info != null)
            {
                AllTargets.Add(
                    new Target(
                        arch,
                        info.Triple,
                        info.MuslCmakeTriple ?? info.Triple,
                        ArchToString(info.Arch),
                        info.CmakeArch,
                        info.GnuTriple,
                        sysroot.Value,
                        info.ExtraFlags,
                        info.IsMusl));
            }
        }
    }

    public static Target? GetTarget(TargetArch architecture, bool muslLibc) =>
        AllTargets.FirstOrDefault(t => t.Arch == architecture && t.IsMusl == muslLibc);

    public static List<Target> GetLldbServerTargets()
    {
        // lldb-server is always built against musl only.
        // Filter AllTargets for musl variants, ensuring only one per architecture.
        return AllTargets
            .Where(t => t.IsMusl)
            .GroupBy(t => t.Arch) // Group by enum arch to handle cases where a different musl sysroot might yield same triple
            .Select(g => g.First()) // Take the first musl target for that arch
            .ToList();
    }

    private static string ArchToString(TargetArch arch) => arch switch
    {
        TargetArch.X64 => "x86_64",
        TargetArch.Armv7 => "armv7",
        TargetArch.Aarch64 => "aarch64",
        TargetArch.Riscv64 => "riscv64",
        TargetArch.X86 => "i686",
        _ => throw new ArgumentOutOfRangeException(nameof(arch)),
    };
}
