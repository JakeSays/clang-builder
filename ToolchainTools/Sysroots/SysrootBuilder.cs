using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Std.BuildTools.Clang;

public class SysrootBuilder
{
    private readonly FilePath _workDir;
    private readonly FilePath _outputDir;
    private readonly string _archName;
    private readonly SysrootArch _arch;
    private readonly string[] _requestedPackages;

    private readonly FilePath _cacheDir;
    private readonly FilePath _sysrootDir;
    private readonly string _archiveName;

    private readonly Dictionary<string, DebPackage> _packagesMeta = new();
    private readonly HashSet<string> _resolved = new();

    private HttpClient _http = null!;

    private record DebPackage(string Name, string Filename, string[] Depends);

    public SysrootBuilder(FilePath workDir, FilePath outputDir, string archName, SysrootArch arch, string[] packages, string archiveName)
    {
        _workDir = workDir;
        _outputDir = outputDir;
        _archName = archName;
        _arch = arch;
        _requestedPackages = packages;
        _archiveName = archiveName;
        _cacheDir = workDir / ".cache";
        _sysrootDir = workDir / $"sysroot-{archName}";
    }

    public async Task<bool> Build()
    {
        using (_http = new HttpClient())
        {
            Directory.CreateDirectory(_cacheDir);
            Directory.CreateDirectory(_sysrootDir);

            if (!await FetchAndParsePackageMetadata())
            {
                return false;
            }
            ResolvePackages();
            if (!await DownloadPackages())
            {
                return false;
            }
            if (!await ExtractPackages())
            {
                return false;
            }

            MinimizeSysroot();
            UnifyLib();
            FixSymlinks();
            FlattenMultiarchPaths();

            if (!await CreateArchive())
            {
                return false;
            }

            Cleanup();
            Log.Info(LogColor.Green, "\nBuild complete!");
            return true;
        }
    }

    // -------------------------------------------------------------------------
    // Package metadata

    private async Task<bool> FetchAndParsePackageMetadata()
    {
        Log.Info($"Fetching package metadata for {_arch.Suite} ({_arch.DebArch}) from {_arch.Mirror}...");

        var baseUrl = $"{_arch.Mirror.TrimEnd('/')}/dists/{_arch.Suite}/main/binary-{_arch.DebArch}";
        var packagesXz = _cacheDir / "Packages.xz";
        var packagesGz = _cacheDir / "Packages.gz";

        if (!packagesXz.Exists && !packagesGz.Exists)
        {
            try
            {
                Log.Info("  Downloading Packages.xz...");
                var response = await _http.GetAsync($"{baseUrl}/Packages.xz");
                response.EnsureSuccessStatusCode();
                await using var fs = File.Create(packagesXz);
                await response.Content.CopyToAsync(fs);
            }
            catch
            {
                Log.Warning("  Packages.xz not found, falling back to Packages.gz...");
                File.Delete(packagesXz);
                var bytes = await _http.GetByteArrayAsync($"{baseUrl}/Packages.gz");
                await File.WriteAllBytesAsync(packagesGz, bytes);
            }
        }

        Log.Info("  Parsing package metadata...");

        string text;
        if (packagesXz.Exists)
        {
            text = await DecompressXz(packagesXz);
        }
        else
        {
            await using var fs = File.OpenRead(packagesGz);
            await using var gz = new GZipStream(fs, CompressionMode.Decompress);
            using var reader = new StreamReader(gz);
            text = await reader.ReadToEndAsync();
        }

        ParsePackagesFile(text);
        Log.Info($"  Found {_packagesMeta.Count} packages in repo.");
        return true;
    }

    private void ParsePackagesFile(string text)
    {
        var current = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd();

            if (line.Length == 0)
            {
                CommitPackage(current);
                current.Clear();
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon > 0)
            {
                var key = line[..colon];
                if (key is "Package" or "Filename" or "Depends")
                {
                    current[key] = line[(colon + 1)..].TrimStart();
                }
            }
        }
        CommitPackage(current); // handle file with no trailing blank line
    }

    private void CommitPackage(Dictionary<string, string> fields)
    {
        if (!fields.TryGetValue("Package", out var name))
        {
            return;
        }
        var filename = fields.GetValueOrDefault("Filename", "");
        var depends = fields.TryGetValue("Depends", out var raw)
            ? ParseDepends(raw)
            : [];
        _packagesMeta[name] = new DebPackage(name, filename, depends);
    }

    private static string[] ParseDepends(string raw) =>
        raw.Split(',')
           .Select(d =>
           {
               d = Regex.Replace(d, @"\(.*?\)", ""); // strip version constraint
               d = d.Split('|')[0];                  // first alternative only
               d = d.Split(':')[0];                  // strip multi-arch qualifier
               return d.Trim();
           })
           .Where(d => d.Length > 0)
           .ToArray();

    // -------------------------------------------------------------------------
    // Dependency resolution

    private void ResolvePackages()
    {
        Log.Info($"Resolving dependencies for: {string.Join(", ", _requestedPackages)}");
        foreach (var pkg in _requestedPackages)
        {
            ResolvePackage(pkg);
        }
        Log.Info($"Total packages to download: {_resolved.Count}");
    }

    private void ResolvePackage(string name)
    {
        if (!_resolved.Add(name))
        {
            return;
        }
        if (!_packagesMeta.TryGetValue(name, out var pkg))
        {
            Log.Warning($"  Warning: package '{name}' not found in repo. Skipping.");
            return;
        }
        foreach (var dep in pkg.Depends)
        {
            ResolvePackage(dep);
        }
    }

    // -------------------------------------------------------------------------
    // Download

    private async Task<bool> DownloadPackages()
    {
        foreach (var name in _resolved)
        {
            if (!_packagesMeta.TryGetValue(name, out var pkg))
            {
                continue;
            }
            var destPath = _cacheDir / new FilePath(pkg.Filename).FileName;
            if (destPath.Exists)
            {
                continue;
            }

            Log.Info($"  -> Downloading {name}...");
            var url = $"{_arch.Mirror.TrimEnd('/')}/{pkg.Filename}";
            try
            {
                await using var s = await _http.GetStreamAsync(url);
                await using var fs = File.Create(destPath);
                await s.CopyToAsync(fs);
            }
            catch (Exception ex)
            {
                Log.Error($"ERROR: Failed to download {name}: {ex.Message}");
                return false;
            }
        }
        return true;
    }

    // -------------------------------------------------------------------------
    // Extraction

    private async Task<bool> ExtractPackages()
    {
        Log.Info("Extracting packages into sysroot...");
        foreach (var name in _resolved)
        {
            if (!_packagesMeta.TryGetValue(name, out var pkg))
            {
                continue;
            }
            var debPath = _cacheDir / new FilePath(pkg.Filename).FileName;
            if (!debPath.Exists)
            {
                continue;
            }

            var exitCode = await ProcessRunner.Run(
                "dpkg-deb", $"-x \"{debPath}\" \"{_sysrootDir}\"");
            if (exitCode != 0)
            {
                Log.Error($"ERROR: Failed to extract {name}");
                return false;
            }
        }
        return true;
    }

    // -------------------------------------------------------------------------
    // Sysroot fixups

    private void MinimizeSysroot()
    {
        Log.Info("Minimizing sysroot (removing docs, man pages, binaries)...");
        foreach (var rel in new[]
                 { "usr/share/doc", "usr/share/man", "usr/share/info",
                   "usr/share/locale", "usr/bin", "bin", "sbin", "usr/sbin", "etc" })
        {
            var path = _sysrootDir / rel;
            if (path.Exists)
            {
                FileUtils.DeleteDirectory(path);
            }
        }
    }

    private void UnifyLib()
    {
        Log.Info("Applying usrmerge (lib -> usr/lib)...");
        var usrLib = _sysrootDir / "usr" / "lib";
        var lib    = _sysrootDir / "lib";

        Directory.CreateDirectory(usrLib);

        if (lib.IsSymLink)
        {
            // Broken or absolute symlink — remove so we can replace it
            File.Delete(lib);
        }
        else if (lib.Exists)
        {
            // Real directory — merge contents into usr/lib then remove
            MergeDirectory(lib, usrLib);
            Directory.Delete(lib, recursive: true);
        }

        // Create clean relative symlink:  lib -> usr/lib
        if (!lib.Exists && !lib.IsSymLink)
        {
            Directory.CreateSymbolicLink(lib, "usr/lib");
        }
    }

    private static void MergeDirectory(FilePath src, FilePath dest)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(src))
        {
            var entryPath  = new FilePath(entry);
            var targetPath = dest / entryPath.FileName;

            if (entryPath.IsSymLink)
            {
                if (!targetPath.Exists && !targetPath.IsSymLink)
                {
                    File.CreateSymbolicLink(targetPath, new FileInfo(entry).LinkTarget!);
                }
            }
            else if (Directory.Exists(entry))
            {
                Directory.CreateDirectory(targetPath);
                MergeDirectory(entryPath, targetPath);
            }
            else
            {
                File.Copy(entry, targetPath, overwrite: true);
            }
        }
    }

    private void FixSymlinks()
    {
        Log.Info("Fixing absolute symlinks to relative symlinks...");
        FixSymlinksRecursive(_sysrootDir);
    }

    private void FixSymlinksRecursive(FilePath dir)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(dir))
        {
            var entryPath = new FilePath(entry);
            if (entryPath.IsSymLink)
            {
                var target = new FileInfo(entry).LinkTarget!;
                if (!target.StartsWith('/'))
                {
                    continue;
                }

                var absTarget = _sysrootDir / target.TrimStart('/');
                var relTarget = absTarget.AsRelativePath(Path.GetDirectoryName(entry)!);
                File.Delete(entry);
                File.CreateSymbolicLink(entry, relTarget);
                // Don't recurse into symlinks
            }
            else if (Directory.Exists(entry))
            {
                FixSymlinksRecursive(entryPath);
            }
        }
    }

    private void FlattenMultiarchPaths()
    {
        Log.Info("Flattening Debian multiarch paths...");
        var usrLib = _sysrootDir / "usr" / "lib";

        foreach (var basePath in new[] { usrLib, _sysrootDir / "usr" / "lib" / "gcc" })
        {
            if (!basePath.Exists || basePath.IsSymLink)
            {
                continue;
            }

            foreach (var staticLib in Directory.EnumerateFiles(basePath, "*.a", SearchOption.AllDirectories))
            {
                var dest = usrLib / new FilePath(staticLib).FileName;
                if (dest is { Exists: false, IsSymLink: false })
                {
                    File.CreateSymbolicLink(dest, new FilePath(staticLib).AsRelativePath(usrLib));
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Archive

    private async Task<bool> CreateArchive()
    {
        var tarName = _archiveName;
        var tarPath = _outputDir / tarName;
        Log.Info($"Creating archive: {tarName} (this may take a moment)...");

        var sysrootDirName = _sysrootDir.FileName;
        var exitCode = await ProcessRunner.Run(
            "tar", $"-cJf \"{tarPath}\" -C \"{_workDir}\" \"{sysrootDirName}\"");
        if (exitCode != 0)
        {
            Log.Error($"ERROR: Failed to create {tarName}");
            return false;
        }

        Log.Info(LogColor.Green, $"Archive saved to: {tarPath}");
        return true;
    }

    private void Cleanup()
    {
        Log.Info("Cleaning up...");
        FileUtils.DeleteDirectory(_workDir);
    }

    // -------------------------------------------------------------------------
    // Helpers

    /// <summary>Decompresses an .xz file and returns its content as a string.</summary>
    private static async Task<string> DecompressXz(FilePath xzFile)
    {
        var psi = new ProcessStartInfo("xz")
        {
            Arguments = $"-dc {xzFile.AsQuotedPath()}",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = Process.Start(psi)!;
        var text = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return text;
    }
}
