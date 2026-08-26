namespace Std.BuildTools.Sysroots;

public record SysrootArch(
    string DebArch,
    string Mirror,
    string Suite,
    string[] DefaultPackages);

public static class SysrootArchConfigs
{
    public static readonly IReadOnlyDictionary<string, SysrootArch> All =
        new Dictionary<string, SysrootArch>
        {
            ["host-x64"] = new("amd64", "http://deb.debian.org/debian", "bookworm",
            [
                "libc6-dev", "libgcc-12-dev", "zlib1g-dev",
                "python3-dev", "libedit-dev", "libncurses-dev",
                // libxml2.a is built with xz support, so its xzlib.o needs liblzma. Debian ships the
                // static liblzma in liblzma-dev; without it a static libxml2 leaves lzma_code and
                // three others undefined at link.
                "libzstd-dev", "libxml2-dev", "liblzma-dev", "libstdc++-12-dev", "gcc-12"
            ]),
            ["x64"] = new("amd64", "http://deb.debian.org/debian", "bookworm",
                ["libc6-dev", "linux-libc-dev", "zlib1g-dev"]),
            ["aarch64"] = new("arm64", "http://deb.debian.org/debian", "bookworm",
                ["libc6-dev", "linux-libc-dev", "zlib1g-dev"]),
            ["armv7"] = new("armhf", "http://deb.debian.org/debian", "bookworm",
                ["libc6-dev", "linux-libc-dev", "zlib1g-dev"]),
            ["riscv64"] = new("riscv64", "http://deb.debian.org/debian", "sid",
                ["libc6-dev", "linux-libc-dev", "zlib1g-dev"]),
            ["x86"] = new("i386", "http://deb.debian.org/debian", "bookworm",
                ["libc6-dev", "linux-libc-dev", "zlib1g-dev"]),
        };
}
