namespace Std.BuildTools.Clang;

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
            ["host-x64"] = new("amd64", "http://deb.debian.org/debian", "bullseye",
            [
                "libc6-dev", "libgcc-10-dev", "zlib1g-dev", "libpthread-stubs0-dev",
                "python3-dev", "libedit-dev", "libncurses-dev",
                "libzstd-dev", "libxml2-dev", "libstdc++-10-dev", "gcc-10"
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
