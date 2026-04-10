namespace Std.BuildTools.Clang;

internal static class StampFile
{
    public static void Create(FilePath stampDir, string prefix)
    {
        var stampFile = stampDir / $".{prefix}-stamp";
        File.WriteAllText(stampFile, DateTime.UtcNow.ToString("o"));
    }

    public static bool IsPresent(FilePath stampDir, string prefix)
    {
        var stampFile = stampDir / $".{prefix}-stamp";

        return stampFile.Exists;
    }
}
