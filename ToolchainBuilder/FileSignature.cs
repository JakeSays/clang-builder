using Std.BuildTools.Common;


namespace Std.BuildTools.Clang;

// Identity used by the lane diff packager to detect whether a file changed
// between the pre-build snapshot and the post-build state. For regular files
// we use (size, mtime); for symlinks we use the link target (size/mtime of a
// symlink are not meaningful for our purposes). The `IsLink` discriminator
// distinguishes "regular zero-byte file" from "symlink with empty target".
public readonly record struct FileSignature(bool IsLink, long Size, DateTime ModifiedUtc, string? LinkTarget)
{
    public static FileSignature For(string path)
    {
        var attributes = File.GetAttributes(path);
        var isLink = (attributes & FileAttributes.ReparsePoint) != 0;

        if (isLink)
        {
            var target = File.ResolveLinkTarget(path, returnFinalTarget: false)?.FullName;
            return new FileSignature(IsLink: true, Size: 0, ModifiedUtc: DateTime.MinValue, LinkTarget: target);
        }

        var info = new FileInfo(path);
        return new FileSignature(IsLink: false, Size: info.Length, ModifiedUtc: info.LastWriteTimeUtc, LinkTarget: null);
    }
}
