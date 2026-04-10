namespace Std.BuildTools.Clang;

public record PythonInfo(string Version, string IncludeDir, string LibraryPath);

public class BuildConfiguration
{
    private FilePath? _srcDir;
    private FilePath? _buildDir;
    private FilePath? _installDir;

    public required TargetArch Architectures { get; init; }
    public required BuildTargets BuildTargets { get; init; }
    public required string LlvmVersion { get; init; }
    public required FilePath WorkDir { get; init; }
    public required FilePath OutputDir { get; init; }
    public required int Jobs { get; init; }
    public required bool ForceReconfigure { get; init; }
    public required bool RunTests { get; init; }
    public required int PackageThreads { get; init; }
    public required FilePath PrebuiltsDir { get; init; }
    public required FilePath BootstrapClangDir { get; init; }
    public required FilePath HostSysroot { get; init; }
    public required FilePath CmakeModulesDir { get; init; }
    public FilePath? X64Sysroot { get; init; }
    public FilePath? X64MuslSysroot { get; init; }
    public FilePath? Armv7Sysroot { get; init; }
    public FilePath? Aarch64Sysroot { get; init; }
    public FilePath? Riscv64Sysroot { get; init; }
    public FilePath? Armv7MuslSysroot { get; init; }
    public FilePath? Aarch64MuslSysroot { get; init; }
    public FilePath? Riscv64MuslSysroot { get; init; }
    public FilePath? X86Sysroot { get; init; }
    public FilePath? X86MuslSysroot { get; init; }
    public PythonInfo? Python { get; set; }

    private string _clangVersion = "";
    public string ClangVersion
    {
        get => _clangVersion;
        set
        {
            _clangVersion = value;
            ClangVersionMajor = value.Split('.')[0];
            ClangResourceDir = InstallDir / "lib" / "clang" / ClangVersionMajor;
        }
    }
    public string ClangVersionMajor { get; private set; } = "";
    public FilePath ClangResourceDir { get; private set; }

    public FilePath InstallDir => _installDir ??= WorkDir / $"clang-{LlvmVersion}-linux-x86_64";

    public FilePath SourceDir => _srcDir ??= WorkDir / "llvm-src";

    public FilePath BuildDir => _buildDir ??= WorkDir / "build";
    public Dictionary<string, string> NormalizedTriples { get; } = new();
}
