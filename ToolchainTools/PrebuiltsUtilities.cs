namespace Std.BuildTools.Clang;

public record struct PrebuiltInfo(string Label, string FileName, string SysrootDir, TargetArch Arch);

public static class PrebuiltsUtilities
{
    private static readonly Dictionary<PrebuiltType, FilePath> PrebuiltPaths = new();

    public static void Initialize(FilePath sysrootBaseDir)
    {
        foreach (PrebuiltType type in Enum.GetValuesAsUnderlyingType<PrebuiltType>())
        {
            var info = GetPrebuiltInfo(type);
            var sysrootDir = sysrootBaseDir / info.SysrootDir;
            PrebuiltPaths[type] = sysrootDir;
        }
    }

    public static async Task<bool> ExpandPrebuilts(
        FilePath prebuiltsSourceDir,
        FilePath workDir,
        FileDownloader? downloader,
        params PrebuiltType[] types)
    {
        foreach (var type in types)
        {
            var info = GetPrebuiltInfo(type);
            var archivePath = FindPrebuiltArchive(info, prebuiltsSourceDir);
            if (archivePath == null)
            {
                return false;
            }

            var destPath = PrebuiltPaths[type];
            if (!await ExtractArchive(archivePath, destPath, info.Label, workDir, downloader))
            {
                return false;
            }
        }
        return true;
    }

    public static FilePath GetPrebuiltPath(PrebuiltType type) => PrebuiltPaths[type];

    public static FilePath GetSysrootDir(TargetArch arch, bool musl)
    {
        return arch switch
        {
            TargetArch.Armv7 when musl => GetPrebuiltPath(PrebuiltType.Armv7MuslSysroot),
            TargetArch.Aarch64 when musl => GetPrebuiltPath(PrebuiltType.Aarch64MuslSysroot),
            TargetArch.Riscv64 when musl => GetPrebuiltPath(PrebuiltType.Riscv64MuslSysroot),
            TargetArch.X64 when musl => GetPrebuiltPath(PrebuiltType.X64MuslSysroot),
            TargetArch.X86 when musl => GetPrebuiltPath(PrebuiltType.X86MuslSysroot),
            TargetArch.X86 => GetPrebuiltPath(PrebuiltType.X86GlibcSysroot),
            TargetArch.X64 => GetPrebuiltPath(PrebuiltType.X64GlibcSysroot),
            TargetArch.Armv7 => GetPrebuiltPath(PrebuiltType.Armv7GlibcSysroot),
            TargetArch.Aarch64 => GetPrebuiltPath(PrebuiltType.Aarch64GlibcSysroot),
            TargetArch.Riscv64 => GetPrebuiltPath(PrebuiltType.Riscv64GlibcSysroot),
            _ => throw new ArgumentOutOfRangeException(nameof(arch), arch, null)
        };
    }
    public static PrebuiltInfo GetPrebuiltInfo(PrebuiltType type) => type switch
    {
        PrebuiltType.BootstrapClang => new PrebuiltInfo("bootstrap Clang", "bootstrap-clang22.1.2.tar.xz", "bootstrap-clang", TargetArch.X64),
        PrebuiltType.HostSysroot => new PrebuiltInfo("x64 musl host sysroot", "sysroot-x64-musl-host.tar.xz", "host", TargetArch.X64),
        PrebuiltType.GlibcHostSysroot => new PrebuiltInfo("x64 glibc host sysroot", "sysroot-x64-glibc-host.tar.xz", "glibc-host", TargetArch.X64),
        PrebuiltType.X64GlibcSysroot => new PrebuiltInfo("x64 glibc cross sysroot", "sysroot-x64-glibc-cross.tar.xz", "x64", TargetArch.X64),
        PrebuiltType.X64MuslSysroot => new PrebuiltInfo("x64 musl cross sysroot", "sysroot-x64-musl-cross.tar.xz", "x64-musl", TargetArch.X64),
        PrebuiltType.Armv7GlibcSysroot => new PrebuiltInfo("armv7 glibc cross sysroot", "sysroot-armv7-glibc-cross.tar.xz", "armv7", TargetArch.Armv7),
        PrebuiltType.Aarch64GlibcSysroot => new PrebuiltInfo("aarch64 glibc cross sysroot", "sysroot-aarch64-glibc-cross.tar.xz", "aarch64", TargetArch.Aarch64),
        PrebuiltType.Riscv64GlibcSysroot => new PrebuiltInfo("riscv64 glibc cross sysroot", "sysroot-riscv64-glibc-cross.tar.xz", "riscv64", TargetArch.Riscv64),
        PrebuiltType.Armv7MuslSysroot => new PrebuiltInfo("armv7 musl cross sysroot", "sysroot-armv7-musl-cross.tar.xz", "armv7-musl", TargetArch.Armv7),
        PrebuiltType.Aarch64MuslSysroot => new PrebuiltInfo("aarch64 musl cross sysroot", "sysroot-aarch64-musl-cross.tar.xz", "aarch64-musl", TargetArch.Aarch64),
        PrebuiltType.Riscv64MuslSysroot => new PrebuiltInfo("riscv64 musl cross sysroot", "sysroot-riscv64-musl-cross.tar.xz", "riscv64-musl", TargetArch.Riscv64),
        PrebuiltType.X86GlibcSysroot => new PrebuiltInfo("x86 glibc cross sysroot", "sysroot-x86-glibc-cross.tar.xz", "x86", TargetArch.X86),
        PrebuiltType.X86MuslSysroot => new PrebuiltInfo("x86 musl cross sysroot", "sysroot-x86-musl-cross.tar.xz", "x86-musl", TargetArch.X86),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    private static string? FindPrebuiltArchive(PrebuiltInfo prebuilt, FilePath prebuiltsDir)
    {
        Log.Info($"Resolving {prebuilt.Label}...");
        try
        {
            var file = prebuiltsDir / prebuilt.FileName;
            if (file.Exists)
            {
                Log.Info(LogColor.Green, $"  found: {prebuilt.FileName}");
                return file;
            }
        }
        catch (DirectoryNotFoundException)
        {
            // Fall through to error
        }

        Log.Warning($"  not found.");
        Log.Error($"ERROR: Cannot find {prebuilt.Label} in {prebuiltsDir} (file name: {prebuilt.FileName}).");

        return null;
    }

    private static async Task<bool> ExtractArchive(
        string source,
        FilePath destinationDir,
        string label,
        FilePath workDir,
        FileDownloader? downloader)
    {
        FilePath tarballPath;
        if (source.StartsWith("http://") || source.StartsWith("https://"))
        {
            if (downloader == null)
            {
                Log.Error($"ERROR: {label}: No downloader specified");
                return false;
            }
            tarballPath = workDir / Path.GetFileName(source);
            await downloader.DownloadFile(source, tarballPath, label);
        }
        else
        {
            if (!File.Exists(source))
            {
                Log.Error($"ERROR: {label}: file not found: {source}");
                return false;
            }
            tarballPath = source;
        }

        return await FileUtils.ExtractArchive(tarballPath, destinationDir, label);
    }
}
