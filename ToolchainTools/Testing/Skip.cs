namespace Std.BuildTools.Clang.Testing;

public record Skip(TargetArch? Arch, TestLibc? Libc, TestType? Type, string? Reason)
{
    public bool Matches(TargetArch arch, bool isMusl, bool isStatic)
    {
        if (Arch != null && Arch.Value != arch)
        {
            return false;
        }

        if (Libc != null)
        {
            var targetLibc = isMusl ? TestLibc.Musl : TestLibc.Glibc;
            if (Libc.Value != targetLibc)
            {
                return false;
            }
        }

        if (Type != null)
        {
            var targetType = isStatic ? TestType.Static : TestType.Shared;
            if (Type.Value != targetType)
            {
                return false;
            }
        }

        return true;
    }
}
