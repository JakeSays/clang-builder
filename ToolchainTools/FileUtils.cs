namespace Std.BuildTools.Clang;

public static class FileUtils
{
    private static readonly string[]? PathDirectories = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator);

    /// <summary>
    /// Recursively deletes a directory, ignoring errors if the directory doesn't exist.
    /// </summary>
    public static void DeleteDirectory(FilePath path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception e)
        {
            Log.Warning($"Warning: Failed to delete directory '{path}': {e.Message}");
        }
    }

    /// <summary>
    /// Checks if a file exists in any of the directories specified in the PATH environment variable.
    /// </summary>
    public static bool ExistsOnPath(string fileName)
    {
        if (PathDirectories == null)
        {
            return false;
        }

        foreach (var path in PathDirectories)
        {
            var fullPath = Path.Combine(path, fileName);
            if (File.Exists(fullPath))
            {
                return true;
            }
        }

        return false;
    }

    public static void DeleteDirectory(params ReadOnlySpan<string> pathParts)
    {
        var path = Path.Join(pathParts);
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, true);
        }
        catch (Exception e)
        {
            Log.Warning($"Warning: Failed to delete directory '{path}': {e.Message}");
        }
    }

    /// <summary>
    /// Finds the full path to an executable by searching the directories in the PATH environment variable.
    /// </summary>
    /// <param name="names">One or more executable names to search for.</param>
    /// <returns>The full path to the first found executable, or null if none are found.</returns>
    public static FilePath? FindExecutable(params string[] names)
    {
        if (PathDirectories == null)
        {
            return null;
        }

        foreach (var name in names)
        {
            foreach (var path in PathDirectories)
            {
                var fullPath = Path.Combine(path, name);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }
        return null;
    }

    public static async Task<bool> ExtractArchive(FilePath archivePath, FilePath destinationDirectory, string label)
    {
        try
        {
            if (StampFile.IsPresent(destinationDirectory, "extract"))
            {
                Log.Info($"{label}: Already extracted, skipping.");
                return true;
            }

            Log.Info(LogColor.Green, $"{label}: Extracting...");
            DeleteDirectory(destinationDirectory); // Ensure a clean slate
            Directory.CreateDirectory(destinationDirectory);

            // -x: extract
            // -a: auto-decompress based on file extension
            // -f: from file
            // --strip-components=1: remove the top-level directory from the archive
            // -C: change to directory before extracting
            var args = new ArgBuilder()
                .Dash("xaf", archivePath, Quoted.Yes)
                .DashDash("strip-components=1")
                .Dash("C", destinationDirectory, Quoted.Yes);

            var exitCode = await ProcessRunner.Run("tar", args.Build());
            if (exitCode != 0)
            {
                Log.Error($"Failed to extract archive '{archivePath}' using 'tar'. Exit code: {exitCode}");
                return false;
            }

            // Create a stamp file to mark completion
            StampFile.Create(destinationDirectory, "extract");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Error extracting {archivePath} -> {destinationDirectory}: {ex.Message}");
            return false;
        }
    }
}
