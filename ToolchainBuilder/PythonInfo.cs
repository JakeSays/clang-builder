namespace Std.BuildTools.Clang;

public sealed class PythonInfo
{
    public string Version { get; }
    public string IncludeDir { get; }
    public string LibraryPath { get; }
    public string HostExecutable { get; }

    public PythonInfo(string version, string includeDir, string libraryPath, string hostExecutable)
    {
        Version = version;
        IncludeDir = includeDir;
        LibraryPath = libraryPath;
        HostExecutable = hostExecutable;
    }
}
