namespace Std.BuildTools.Clang;

public record BootstrapArgs(
    string LlvmVersion,
    string PrebuiltsDir,
    string OutputDir,
    string WorkDir,
    string? SrcDir,
    int Jobs,
    bool KeepWorkDir,
    bool BuildOnly,
    bool ForceReconfigure,
    bool RunTests);
