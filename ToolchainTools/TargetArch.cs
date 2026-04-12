namespace Std.BuildTools.Clang;

[Flags]
public enum TargetArch
{
    None = 0,
    X64 = 1,
    Armv7 = 2,
    Aarch64 = 4,
    Riscv64 = 8,
    X86 = 16,
    All = X64 | Armv7 | Aarch64 | Riscv64 | X86
}

public static class TargetArchExtensions
{
    extension(TargetArch value)
    {
        public string Name => value switch
        {
            TargetArch.None => "none",
            TargetArch.X64 => "x86_64",
            TargetArch.Armv7 => "armv7",
            TargetArch.Aarch64 => "aarch64",
            TargetArch.Riscv64 => "riscv64",
            TargetArch.X86 => "i686",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };

        public bool IsSet(TargetArch flag)
        {
            return (value & flag) != 0;
        }

        public TargetArch Set(TargetArch flag) => value | flag;

        public TargetArch AddIf(bool condition, TargetArch arg)
        {
            return condition
                ? value | arg
                : arg;
        }

        public TargetArch SetIf(bool condition, TargetArch arg)
        {
            return condition
                ? value | arg
                : value & ~arg;
        }

        public TargetArch SetIf(bool condition)
        {
            return condition
                ? value
                : TargetArch.None;
        }
    }

    extension(TargetArch)
    {
        public static TargetArch operator |(TargetArch left, TargetArch right) => left | right;
    }
}
