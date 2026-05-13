using Std.BuildTools.Common;


namespace Std.BuildTools.Sysroots;

public enum CommandLineResult
{
    Help,
    Success,
    Failure
}

public static class CommandLineParser
{
    public static CommandLineResult ParseSysroot(string[] args, out SysrootArgs sysrootArgs)
    {
        sysrootArgs = null!;

        if (args.Length > 0 && (args[0] == "--help" || args[0] == "-h"))
        {
            PrintSysrootUsage();
            return CommandLineResult.Help;
        }

        string? outputDir = null;
        string? workDir = null;
        var host = false;
        var hostX64 = false;
        var glibc = false;
        var musl = false;
        var enabledArchs = TargetArch.None;
        string? release = null;
        string? repoUrl = null;
        string[]? packagesArg = null;
        string[]? packageListFile = null;
        string? packageListRelease = null;
        string? packageListRepoUrl = null;
        var keepWorkDir = false;
        var noPackage = false;
        var pyVersion = "3.12.3";

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--host":
                    host = true;
                    break;
                case "--host-x64":
                    hostX64 = true;
                    break;
                case "--x64":
                    enabledArchs |= TargetArch.X64;
                    break;
                case "--aarch64":
                    enabledArchs |= TargetArch.Aarch64;
                    break;
                case "--armv7":
                    enabledArchs |= TargetArch.Armv7;
                    break;
                case "--riscv64":
                    enabledArchs |= TargetArch.Riscv64;
                    break;
                case "--x32":
                    enabledArchs |= TargetArch.X86;
                    break;
                case "--glibc":
                    glibc = true;
                    break;
                case "--musl":
                    musl = true;
                    break;
                case "--output-dir":
                    outputDir = NextArg(args, ref i, "--output-dir");
                    if (outputDir == null)
                    {
                        return CommandLineResult.Failure;
                    }
                    break;
                case "--work-dir":
                    workDir = NextArg(args, ref i, "--work-dir");
                    if (workDir == null)
                    {
                        return CommandLineResult.Failure;
                    }
                    break;
                case "-r" or "--release":
                    release = NextArg(args, ref i, "--release");
                    if (release == null)
                    {
                        return CommandLineResult.Failure;
                    }
                    break;
                case "-p" or "--packages":
                {
                    var val = NextArg(args, ref i, "--packages");
                    if (val == null)
                    {
                        return CommandLineResult.Failure;
                    }
                    packagesArg = val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    break;
                }
                case "--package-list":
                {
                    var val = NextArg(args, ref i, "--package-list");
                    if (val == null)
                    {
                        return CommandLineResult.Failure;
                    }
                    if (!TryReadPackageListFile(val, out packageListFile, out packageListRelease, out packageListRepoUrl))
                    {
                        return CommandLineResult.Failure;
                    }
                    break;
                }
                case "--keep-work-dir":
                    keepWorkDir = true;
                    break;
                case "--no-package":
                    noPackage = true;
                    break;
                case "--py-version":
                    pyVersion = NextArg(args, ref i, "--py-version") ?? pyVersion;
                    break;
                default:
                    Log.Error($"Unknown option '{args[i]}'.");
                    PrintSysrootUsage();
                    return CommandLineResult.Failure;
            }
        }

        var hasCrossArch = enabledArchs != TargetArch.None;

        if (!host && !hostX64 && !hasCrossArch)
        {
            Log.Error("Specify at least one target: --host, --host-x64, --x64, --aarch64, --armv7, --riscv64, --x32");
            return CommandLineResult.Failure;
        }

        if (hasCrossArch && !glibc && !musl)
        {
            Log.Error("Cross-arch targets require --glibc, --musl, or both.");
            return CommandLineResult.Failure;
        }

        if (string.IsNullOrWhiteSpace(outputDir))
        {
            Log.Error("--output-dir is required.");
            return CommandLineResult.Failure;
        }

        if (string.IsNullOrWhiteSpace(workDir))
        {
            Log.Error("--work-dir is required.");
            return CommandLineResult.Failure;
        }

        if (!TryCombinePackageInputs(packageListFile, packagesArg, out var packages))
        {
            return CommandLineResult.Failure;
        }

        release ??= packageListRelease;
        repoUrl ??= packageListRepoUrl;

        sysrootArgs = new SysrootArgs(
            OutputDir: Path.GetFullPath(outputDir),
            WorkDir: Path.GetFullPath(workDir),
            Host: host,
            HostX64: hostX64,
            Glibc: glibc,
            Musl: musl,
            EnabledArchs: enabledArchs,
            Release: release,
            RepoUrl: repoUrl,
            Packages: packages,
            KeepWorkDir: keepWorkDir,
            NoPackage: noPackage,
            PyVersion: pyVersion);

        return CommandLineResult.Success;
    }

    private static bool TryReadPackageListFile(string path, out string[]? packages, out string? release, out string? repoUrl)
    {
        packages = null;
        release = null;
        repoUrl = null;

        if (!File.Exists(path))
        {
            Log.Error($"--package-list file '{path}' does not exist.");
            return false;
        }

        var result = new List<string>();
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine;
            var commentStart = line.IndexOf('#');
            if (commentStart >= 0)
            {
                line = line[..commentStart];
            }

            line = line.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon > 0)
            {
                var key = line[..colon].Trim();
                if (key is "repo-url" or "release")
                {
                    var value = line[(colon + 1)..].Trim();
                    if (value.Length == 0)
                    {
                        Log.Error($"--package-list: directive '{key}:' in '{path}' has no value.");
                        return false;
                    }
                    if (key == "repo-url")
                    {
                        repoUrl = value;
                    }
                    else
                    {
                        release = value;
                    }
                    continue;
                }
            }

            result.Add(line);
        }

        if (result.Count == 0)
        {
            Log.Error($"--package-list file '{path}' contained no packages.");
            return false;
        }

        packages = result.ToArray();
        return true;
    }

    private static bool TryCombinePackageInputs(string[]? baseList, string[]? deltas, out string[]? combined)
    {
        if (baseList == null)
        {
            combined = deltas;
            return true;
        }

        if (deltas == null)
        {
            combined = baseList;
            return true;
        }

        var result = new List<string>(baseList);
        foreach (var entry in deltas)
        {
            if (entry.StartsWith('-'))
            {
                var name = entry[1..];
                if (name.Length == 0)
                {
                    Log.Error("--packages contains an empty removal entry ('-' with no name).");
                    combined = null;
                    return false;
                }
                result.RemoveAll(p => string.Equals(p, name, StringComparison.Ordinal));
            }
            else
            {
                var name = entry.StartsWith('+')
                    ? entry[1..]
                    : entry;
                if (name.Length == 0)
                {
                    Log.Error("--packages contains an empty addition entry ('+' with no name).");
                    combined = null;
                    return false;
                }
                if (!result.Any(p => string.Equals(p, name, StringComparison.Ordinal)))
                {
                    result.Add(name);
                }
            }
        }

        combined = result.ToArray();
        return true;
    }

    private static string? NextArg(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length)
        {
            Log.Error($"{flag} requires a value.");
            return null;
        }

        return args[++i];
    }

    public static void PrintSysrootUsage()
    {
        Log.Info("""
            Usage: sysroot-builder [options]

            Options:
              --output-dir <dir>    Output directory for archives [required]
              --work-dir <dir>      Temporary working directory [required]
              --host                Build Alpine musl x64 host sysroot
              --host-x64            Build Debian glibc x64 host sysroot
              --x64                 Build x64 cross sysroot
              --aarch64             Build aarch64 cross sysroot
              --armv7               Build armv7 cross sysroot
              --riscv64             Build riscv64 cross sysroot
              --x32                 Build x86 (i686) cross sysroot
              --glibc               Build Debian glibc cross sysroots
              --musl                Build Alpine musl cross sysroots
              -r, --release <suite> Override release. For Debian: bookworm, sid, etc.
                                    For Alpine: latest-stable, edge, v3.20, etc.
              -p, --packages <pkgs> Override base packages (comma-separated). When used
                                    alongside --package-list, entries are deltas: plain
                                    'pkg' (or '+pkg') adds, '-pkg' removes.
              --package-list <file> Read package list from file (one per line; '#' for
                                    comments; blank lines ignored). Overrides hardcoded
                                    defaults for both Alpine and Debian builders. May
                                    include 'release: <suite>' and 'repo-url: <url>'
                                    directive lines. CLI '--release' wins over the file
                                    directive.
              --keep-work-dir       Do not delete musl working directories
              --no-package          Skip creating tar archives (implies --keep-work-dir)
              --py-version <ver>    Python version for host sysroot (default: 3.12.3)
            """);
    }
}
