namespace Std.BuildTools.Clang.Testing;

public record TestDefinition(string FileName, IReadOnlyList<Skip> Skips)
{
    public bool ShouldSkip(TargetArch arch, bool isMusl, bool isStatic, out string? reason)
    {
        foreach (var skip in Skips)
        {
            if (skip.Matches(arch, isMusl, isStatic))
            {
                reason = skip.Reason;
                return true;
            }
        }

        reason = null;
        return false;
    }
}
