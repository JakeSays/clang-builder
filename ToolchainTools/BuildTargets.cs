namespace Std.BuildTools.Clang;

[Flags]
public enum BuildTargets
{
    None = 0,
    Stage1 = 1 << 0,
    RtGlibc = 1 << 1,
    RtMusl = 1 << 2,
    LibcxxGlibc = 1 << 3,
    LibcxxMusl = 1 << 4,
    SanGlibc = 1 << 5,
    SanMusl = 1 << 6,
    LldbServer = 1 << 7,
    LibLldb = 1 << 8,
    All = Stage1 | RtGlibc | RtMusl | LibcxxGlibc | LibcxxMusl | SanGlibc | SanMusl | LldbServer | LibLldb,
}

public static class BuildTargetsExtensions
{
    extension(BuildTargets value)
    {
        public bool IsSet(BuildTargets flag) => (value & flag) != 0;
    }
}
