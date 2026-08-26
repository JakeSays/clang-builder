namespace Std.BuildTools.Sysroots;

/// <summary>
/// The tar options that make a sysroot archive depend on its contents and nothing else.
/// </summary>
/// <remarks>
/// Without these, two builds of one sysroot produce different archives. tar walks a directory in
/// readdir order, which varies with how the tree was written, so the member order changes between
/// runs — and with it the byte stream xz sees. Three builds of the glibc host sysroot came out
/// 74,347,256, 75,121,780 and 75,404,924 bytes holding the same 4826 files at the same sizes.
/// <para>
/// <c>--sort=name</c> is what settles that. <c>--mtime</c> covers the directories and symlinks the
/// builder creates rather than unpacks — those record the time of the build, where everything out of a
/// package already has a fixed one — and the ownership options keep whoever ran the build out of the
/// archive, which otherwise differs between a workstation and CI.
/// </para>
/// <para>
/// What it buys is that an unchanged sysroot rebuilds to the same bytes: republishing one is then a
/// no-op in git rather than a fresh LFS object the size of the archive.
/// </para>
/// </remarks>
public static class ReproducibleTar
{
    /// <summary>
    /// Passed before <c>-cJf</c>. GNU tar has had <c>--sort</c> since 1.28.
    /// </summary>
    public const string Flags = "--sort=name --mtime=@0 --owner=0 --group=0 --numeric-owner";
}
