namespace Std.BuildTools.Clang;

public static class Configurator
{
    /// <summary>
    /// Deletes build.ninja and CMakeCache.txt when forceReconfigure is requested,
    /// then runs cmake configure if needed. Returns true on success (including
    /// when already configured), false if cmake configure fails.
    /// </summary>
    public static async Task<bool> Configure(FilePath buildDir, bool forceReconfigure, string cmakeArgs, string label)
    {
        var ninjaFile = buildDir / "build.ninja";
        if (forceReconfigure && ninjaFile.Exists)
        {
            File.Delete(ninjaFile);
            File.Delete(buildDir / "CMakeCache.txt");
            Log.Info("Deleted build.ninja and CMakeCache.txt for clean reconfigure.");
        }

        if (!ninjaFile.Exists)
        {
            Log.Info($"Configuring {label}...");
            return await ProcessRunner.Run("cmake", cmakeArgs) == 0;
        }

        Log.Info($"{label}: already configured -- skipping cmake configure.");
        return true;
    }
}
