using System.Text;
using Std.BuildTools.Common;


namespace Std.BuildTools.Clang;

// Snapshots an install directory before a build, then packs files added or
// modified during the build into a tarball. Used by the multi-lane CI workflow
// to ship only the per-lane delta over the shared stage1 install.
//
// We can't drive this with `find -newer` (mtime) because cmake's install(FILES)
// preserves source mtimes — headers installed from the LLVM tarball keep the
// tarball's mtime and would be skipped. Snapshotting the actual file set
// before/after the build avoids that pitfall entirely.
public class LaneDiffPackager
{
    private readonly FilePath _installDir;
    private readonly Dictionary<string, FileSignature> _snapshot = new();

    public LaneDiffPackager(FilePath installDir)
    {
        _installDir = installDir;
    }

    public void Snapshot()
    {
        _snapshot.Clear();

        if (!Directory.Exists(_installDir))
        {
            return;
        }

        foreach (var entry in EnumerateFilesAndLinks(_installDir))
        {
            _snapshot[entry] = FileSignature.For(entry);
        }

        Log.Info($"Lane diff snapshot: {_snapshot.Count} existing file(s) in {_installDir}.");
    }

    public async Task<bool> PackDiff(FilePath outputTarball)
    {
        if (!Directory.Exists(_installDir))
        {
            Log.Error($"ERROR: Install directory not found: {_installDir}");
            return false;
        }

        var installDirPath = ((string)_installDir).TrimEnd('/');
        var prefix = installDirPath + "/";

        var changedRelativePaths = new List<string>();
        foreach (var entry in EnumerateFilesAndLinks(_installDir))
        {
            var current = FileSignature.For(entry);
            if (_snapshot.TryGetValue(entry, out var previous) && previous == current)
            {
                continue;
            }

            var relative = entry.StartsWith(prefix, StringComparison.Ordinal)
                ? entry[prefix.Length..]
                : entry;
            changedRelativePaths.Add(relative);
        }

        Log.Info($"Packing {changedRelativePaths.Count} changed file(s) to {outputTarball}...");

        if (changedRelativePaths.Count == 0)
        {
            Log.Warning("Lane diff is empty; build produced no new or changed files.");
        }

        var outputDir = Path.GetDirectoryName((string)outputTarball);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var listFile = Path.Combine(Path.GetTempPath(), $"lane-diff-{Guid.NewGuid():N}.lst");
        try
        {
            await using (var stream = File.Create(listFile))
            {
                foreach (var relative in changedRelativePaths)
                {
                    var bytes = Encoding.UTF8.GetBytes(relative);
                    await stream.WriteAsync(bytes);
                    stream.WriteByte(0);
                }
            }

            var args = new ArgBuilder()
                .DashDash("null")
                .DashDashAssigned("owner", "0")
                .DashDashAssigned("group", "0")
                .Dash("czf", outputTarball, Quoted.Yes)
                .Dash("C", _installDir, Quoted.Yes)
                .Dash("T", listFile, Quoted.Yes);

            var exitCode = await ProcessRunner.Run("tar", args.Build());
            if (exitCode != 0)
            {
                Log.Error($"ERROR: Failed to create lane diff tarball (exit {exitCode}).");
                return false;
            }
        }
        finally
        {
            try
            {
                File.Delete(listFile);
            }
            catch
            {
                // Best-effort cleanup; ignore deletion failures.
            }
        }

        Log.Info(LogColor.Green, $"Lane diff written: {outputTarball}");
        return true;
    }

    private static IEnumerable<string> EnumerateFilesAndLinks(string root)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
        {
            var attributes = File.GetAttributes(entry);
            var isLink = (attributes & FileAttributes.ReparsePoint) != 0;
            var isDirectory = (attributes & FileAttributes.Directory) != 0;

            if (isDirectory && !isLink)
            {
                continue;
            }

            yield return entry;
        }
    }
}
