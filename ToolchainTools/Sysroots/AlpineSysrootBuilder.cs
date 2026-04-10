namespace Std.BuildTools.Clang;

public class AlpineSysrootBuilder
{
    private const string AlpineMirror = "https://dl-cdn.alpinelinux.org/alpine/latest-stable";

    private static readonly string[] HostPackages =
    [
        "musl-dev",
        "zlib-dev",
        "libedit-dev",
        "ncurses-dev",
        "zstd-dev",
        "libxml2-dev",
        "llvm-libunwind-dev",
        "libc++-dev",
        "openssl-dev",
        "bzip2-dev",
        "xz-dev",
        "sqlite-dev",
        "libffi-dev",
        "expat-dev",
        "readline-dev",
        "gdbm-dev",
        "mpdecimal-dev",
        "util-linux-dev",
        "zlib-static",
        "libedit-static",
        "ncurses-static",
        "zstd-static",
        "libxml2-static",
        "llvm-libunwind-static",
        "libc++-static",
        "openssl-libs-static",
        "bzip2-static",
        "xz-static",
        "sqlite-static",
        "expat-static",
        "readline-static",
        "util-linux-static",
    ];

    // Cross sysroots need only libc headers, kernel headers, and zlib.
    // libc++, libunwind, and compiler-rt come from the install prefix.
    private static readonly string[] CrossPackages =
    [
        "musl-dev",
        "linux-headers",
        "zlib-dev",
        "zlib-static",
    ];

    private readonly FilePath _workDir;
    private readonly FilePath _outputPath;
    private readonly string _apkArch;
    private readonly bool _buildPython;
    private readonly string _pyVersion;
    private readonly bool _keepWorkDir;
    private readonly bool _noPackage;

    public AlpineSysrootBuilder(
        FilePath workDir,
        FilePath outputPath,
        string apkArch,
        bool buildPython,
        string pyVersion,
        bool keepWorkDir,
        bool noPackage)
    {
        _workDir = workDir;
        _outputPath = outputPath;
        _apkArch = apkArch;
        _buildPython = buildPython;
        _pyVersion = pyVersion;
        _keepWorkDir = keepWorkDir;
        _noPackage = noPackage;
    }

    public async Task<bool> Build()
    {
        var apkStatic = FileUtils.FindExecutable("apk.static");
        if (apkStatic == null)
        {
            Log.Error("ERROR: 'apk.static' was not found in PATH.");
            return false;
        }

        Directory.CreateDirectory(_workDir);
        Log.Info($"Initializing {_apkArch} sysroot at '{_workDir}'...");

        var packages = _buildPython
            ? HostPackages
            : CrossPackages;
        if (!await RunApk(apkStatic.Value, packages))
        {
            return false;
        }

        CleanAndMergeSysroot();

        if (_buildPython && !await BuildAndMergeStaticPython())
        {
            return false;
        }

        if (_noPackage)
        {
            Log.Info(LogColor.Green, $"Success! Sysroot is ready at: {_workDir}");
            Log.Info("    (--no-package flag was used, skipping tar archive creation)");
            return true;
        }

        if (!await CreateArchive())
        {
            return false;
        }

        if (!_keepWorkDir)
        {
            Log.Info($"Deleting working directory '{_workDir}'...");
            FileUtils.DeleteDirectory(_workDir);
        }
        else
        {
            Log.Info($"Kept working directory at '{_workDir}'.");
        }

        Log.Info(LogColor.Green, $"Success! Sysroot archive is ready: {_outputPath}");
        return true;
    }

    private async Task<bool> RunApk(FilePath apkStatic, string[] packages)
    {
        Log.Info($"Installing packages: {string.Join(", ", packages)}");

        var args = $"--root \"{_workDir}\" --arch {_apkArch}" +
                   $" -X {AlpineMirror}/main" +
                   $" -X {AlpineMirror}/community" +
                   $" -U --allow-untrusted --initdb add" +
                   $" {string.Join(' ', packages)}";

        var exitCode = await ProcessRunner.Run(apkStatic, args);
        if (exitCode != 0)
        {
            Log.Error($"ERROR: apk.static failed with exit code {exitCode}");
            return false;
        }
        return true;
    }

    private void CleanAndMergeSysroot()
    {
        Log.Info("Cleaning up sysroot and performing usr-merge...");

        DeleteSysrootEntry("dev");
        DeleteSysrootEntry("etc");
        DeleteSysrootEntry("proc");
        DeleteSysrootEntry("tmp");
        DeleteSysrootEntry("var");
        DeleteSysrootEntry("lib/apk");

        var libDir = _workDir / "lib";
        var usrLibDir = _workDir / "usr" / "lib";

        if (libDir is not { Exists: true, IsSymLink: false })
        {
            return;
        }

        Directory.CreateDirectory(usrLibDir);
        MergeDirectory(libDir, usrLibDir);
        FileUtils.DeleteDirectory(libDir);
        Directory.CreateSymbolicLink(libDir, "usr/lib");
    }

    private void DeleteSysrootEntry(string rel)
    {
        var target = _workDir / rel;
        if (target.IsSymLink)
        {
            File.Delete(target);
        }
        else
        {
            FileUtils.DeleteDirectory(target);
        }
    }

    private static void MergeDirectory(FilePath src, FilePath dest)
    {
        foreach (var entry in src.EnumerateDirectoryEntries())
        {
            var targetPath = dest / entry.FileName;

            if (entry.IsSymLink)
            {
                if (targetPath is { Exists: false, IsSymLink: false })
                {
                    File.CreateSymbolicLink(targetPath, new FileInfo(entry).LinkTarget!);
                }

                continue;
            }

            if (Directory.Exists(entry))
            {
                Directory.CreateDirectory(targetPath);
                MergeDirectory(entry, targetPath);
                continue;
            }

            if (!targetPath.Exists)
            {
                File.Copy(entry, targetPath);
            }
        }
    }

    private async Task<bool> BuildAndMergeStaticPython()
    {
        var pyTmpDir = _workDir / "pytmp";
        Log.Info($"Building static Python {_pyVersion}...");

        var pythonBuilder = new StaticPythonBuilder();
        if (!await pythonBuilder.Build(pyTmpDir, _pyVersion))
        {
            return false;
        }

        Log.Info("Merging static Python into sysroot...");

        var pyInclude = pyTmpDir / "include";
        var pyLib = pyTmpDir / "lib";
        var sysrootInclude = _workDir / "usr" / "include";
        var sysrootLib = _workDir / "usr" / "lib";

        Directory.CreateDirectory(sysrootInclude);
        Directory.CreateDirectory(sysrootLib);

        if (Directory.Exists(pyInclude))
        {
            MergeDirectory(pyInclude, sysrootInclude);
        }
        if (Directory.Exists(pyLib))
        {
            MergeDirectory(pyLib, sysrootLib);
        }

        Log.Info("Cleaning up temporary Python build directory...");
        FileUtils.DeleteDirectory(pyTmpDir);
        return true;
    }

    private async Task<bool> CreateArchive()
    {
        Log.Info($"Compressing sysroot to '{_outputPath}' (this may take a moment)...");

        var workDirName = _workDir.FileName;
        var parentDir = Path.GetDirectoryName((string)_workDir)!;

        var exitCode = await ProcessRunner.Run(
            "tar", $"-cJf \"{_outputPath}\" -C \"{parentDir}\" \"{workDirName}\"");
        if (exitCode != 0)
        {
            Log.Error($"ERROR: Failed to create archive '{_outputPath}'");
            return false;
        }
        return true;
    }
}
