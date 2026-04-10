using SysPath = System.IO.Path;

namespace Std.BuildTools.Clang;

/// <summary>
/// Represents a file system path with behavior modeled after C++17's std::filesystem::path.
/// This class provides an intuitive API for path manipulation, including operator overloads
/// for concatenation. It is implicitly convertible to and from a string.
///
/// NOTE: This class currently only supports unix paths.
///
/// </summary>
public struct FilePath : IEquatable<FilePath>
{
    private string? _path;
    private bool _normalized;

    /// <summary>
    /// Represents the string for an empty path, which is typically "." for the current directory.
    /// </summary>
    public const string EmptyPathString = "./";

    /// <summary>
    /// Gets an empty <see cref="FilePath"/> instance, representing the current directory (".").
    /// </summary>
    public static FilePath EmptyPath => new();

    private readonly string Path => _path ?? EmptyPathString;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilePath"/> struct representing the current directory (".").
    /// </summary>
    public FilePath()
    {
        _path = EmptyPathString;
        _normalized = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FilePath"/> class.
    /// </summary>
    /// <param name="path">The initial path string.</param>
    public FilePath(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        _path = path;
        NormalizePath();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FilePath"/> class by joining a sequence of path components.
    /// </summary>
    /// <param name="components">The path components to join.</param>
    public FilePath(params ReadOnlySpan<string> components)
    {
        _path = SysPath.Join(components);
        NormalizePath();
    }

    /// <summary>
    /// Implicitly converts a <see cref="FilePath"/> to a <see cref="string"/>.
    /// This conversion returns the normalized path.
    /// </summary>
    public static implicit operator string(FilePath filePath) => filePath.NormalizePath();

    /// <summary>
    /// Implicitly converts a <see cref="string"/> to a <see cref="FilePath"/>.
    /// The path is normalized upon creation.
    /// </summary>
    public static implicit operator FilePath(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return new FilePath(path);
    }

    /// <summary>
    /// Returns the string representation of the path.
    /// This returns the raw, un-normalized path.
    /// </summary>
    public readonly override string ToString() => Path;

    /// <summary>
    /// Gets a value indicating whether the path is an absolute path (starts with '/').
    /// </summary>
    public readonly bool IsAbsolute => Path.StartsWith('/');

    /// <summary>
    /// Gets the last component of the path.
    /// </summary>
    /// <remarks>
    /// If the path ends with a directory separator ('/'), this returns an empty string.
    /// If the path has no directory separators, this also returns an empty string.
    /// Otherwise, it returns the substring after the final directory separator.
    /// </remarks>
    public readonly string LastComponent
    {
        get
        {
            var path = Path;
            if (path.EndsWith('/'))
            {
                return "";
            }

            var pos = path.LastIndexOf('/');
            return pos < 0
                ? ""
                : path[(pos + 1)..];
        }
    }

    /// <summary>
    /// Resolves the path to an absolute path, using the provided base path if the current path is relative.
    /// </summary>
    /// <param name="absoluteBasePath">The fully qualified base path to resolve against.</param>
    /// <returns>A new <see cref="FilePath"/> representing the absolute path.</returns>
    public readonly FilePath AsAbsolutePath(string absoluteBasePath)
    {
        ArgumentNullException.ThrowIfNull(absoluteBasePath);
        if (!SysPath.IsPathFullyQualified(absoluteBasePath))
        {
            throw new ArgumentException("Absolute path is not a valid absolute path.", nameof(absoluteBasePath));
        }

        var path = Path;
        if (IsAbsolute)
        {
            return path.StartsWith(absoluteBasePath)
                ? this
                : throw new InvalidOperationException("Path is already absolute.");
        }


        return new FilePath(absoluteBasePath, path);
    }

    /// <summary>
    /// Resolves the path to an absolute path, using the provided base path if the current path is relative.
    /// </summary>
    /// <param name="absoluteBasePath">The base path to resolve against.</param>
    /// <returns>A new <see cref="FilePath"/> representing the absolute path.</returns>
    public readonly FilePath AsAbsolutePath(FilePath absoluteBasePath) => AsAbsolutePath(absoluteBasePath.Path);

    /// <summary>
    /// Creates a relative path from a base path to the current path.
    /// </summary>
    /// <param name="relativeBasePath">The path to make the current path relative to.</param>
    /// <returns>A new <see cref="FilePath"/> representing the relative path.</returns>
    public readonly FilePath AsRelativePath(string relativeBasePath)
    {
        ArgumentNullException.ThrowIfNull(relativeBasePath);

        return new FilePath(SysPath.GetRelativePath(relativeBasePath, Path));
    }

    /// <summary>
    /// Creates a relative path from a base path to the current path.
    /// </summary>
    /// <param name="relativeBasePath">The path to make the current path relative to.</param>
    /// <returns>A new <see cref="FilePath"/> representing the relative path.</returns>
    public readonly FilePath AsRelativePath(FilePath relativeBasePath) => AsRelativePath(relativeBasePath.Path);

    /// <summary>Gets the extension of the path string.</summary>
    public readonly string Extension => SysPath.GetExtension(Path);

    /// <summary>Gets a value indicating whether the path includes a file extension.</summary>
    public readonly bool HasExtension => _path != EmptyPathString && SysPath.HasExtension(_path);

    /// <summary>
    /// Changes the extension of a path string.
    /// </summary>
    /// <param name="extension">The new extension (with or without a leading period).</param>
    /// <returns>A new <see cref="FilePath"/> with the changed extension.</returns>
    public readonly FilePath SetExtension(string extension)
    {
        ArgumentException.ThrowIfNullOrEmpty(extension);

        return IsDirectory
            ? throw new InvalidOperationException("Cannot set extension on a a directory.")
            : new FilePath(SysPath.ChangeExtension(Path, extension));
    }

    /// <summary>
    /// Returns a new <see cref="FilePath"/> with the extension removed.
    /// </summary>
    public readonly FilePath WithoutExtension()
    {
        if (IsDirectory)
        {
            return this;
        }

        var path = Path;
        if (path.EndsWith('.'))
        {
            return new FilePath(path[..^1]);
        }

        var pos = path.LastIndexOf('.');
        return pos < 0
            ? this
            : new FilePath(path[(pos + 1)..]);
    }

    /// <summary>
    /// Gets the file name and extension of the path string.
    /// </summary>
    public readonly string FileName => LastComponent;

    /// <summary>Gets a value indicating whether the path represents a directory (ends with '/') or the current directory (".").</summary>
    public readonly bool IsDirectory => Path.EndsWith('/');

    /// <summary>Gets a value indicating whether this path is empty (represents the current directory).</summary>
    public readonly bool IsEmpty => Path == EmptyPathString;

    /// <summary>Returns the path as a double-quoted string.</summary>
    public readonly string AsQuotedPath() => $"\"{Path}\"";

    /// <summary>Gets a value indicating whether the path exists as a file or directory on disk.</summary>
    public readonly bool Exists => File.Exists(Path) || Directory.Exists(Path);

    /// <summary>Gets a value indicating whether the path is a symbolic link.</summary>
    public readonly bool IsSymLink =>
        Exists && (File.GetAttributes(Path) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;

    /// <summary>
    /// Concatenates two path segments using the system's directory separator.
    /// </summary>
    public static FilePath operator /(FilePath left, FilePath right)
    {
        return right.IsAbsolute
            ? throw new InvalidOperationException("Cannot concatenate an absolute path.")
            : new FilePath(SysPath.Combine(left.Path, right.Path));
    }

   /// <summary>
    /// Concatenates a <see cref="FilePath"/> with a string segment using the system's directory separator.
    /// </summary>
    /// <param name="left">The left-hand <see cref="FilePath"/> operand.</param>
    /// <param name="right">The right-hand string operand (path segment).</param>
    /// <returns>A new <see cref="FilePath"/> representing the concatenated path.</returns>
    public static FilePath operator /(FilePath left, string right)
    {
        ArgumentNullException.ThrowIfNull(right);
        return SysPath.IsPathFullyQualified(right)
            ? throw new InvalidOperationException("Cannot concatenate an absolute path.")
            : new FilePath(SysPath.Combine(left.Path, right));
    }

    /// <summary>
    /// Creates a new normalized version of the path.
    /// This method performs lexical normalization based on the rules from C++17's std::filesystem::path::lexically_normal.
    /// </summary>
    /// <returns>A new <see cref="FilePath"/> instance with the normalized path.</returns>
    public FilePath Normalize()
    {
        return _normalized
            ? this
            : new FilePath(Path);
    }

    private string NormalizePath()
    {
        if (_normalized)
        {
            //will never be null
            return _path!;
        }

        _normalized = true;

        // 1. If the path is empty, stop.
        var pathAsString = _path;
        if (string.IsNullOrEmpty(pathAsString))
        {
            _path = EmptyPathString;
            return _path;
        }

        // This implementation covers rules 2 through 7.
        var components = pathAsString.Split('/');
        var stack = new List<string>();
        var isAbsolute = pathAsString.StartsWith('/');

        foreach (var component in components)
        {
            // Rule 2 (partially): Handles multiple slashes by producing empty components.
            // Rule 4: Remove each dot.
            if (component == "." || string.IsNullOrEmpty(component))
            {
                continue;
            }

            if (component == "..")
            {
                // Rule 5: Remove each non-dot-dot filename... followed by a dot-dot.
                if (stack.Count > 0 && stack.Last() != "..")
                {
                    stack.RemoveAt(stack.Count - 1);
                }
                // Rule 6: If there is root-directory, remove all dot-dots...
                else if (!isAbsolute)
                {
                    stack.Add("..");
                }
            }
            else
            {
                stack.Add(component);
            }
        }

        // If the path resolves to empty (e.g., "a/.."), its normal form is ".".
        if (stack.Count == 0)
        {
            _path = EmptyPathString;
            return _path;
        }

        var finalPath = string.Join("/", stack);

        // Rule 7: If the last filename is dot-dot, remove any trailing directory-separator.
        // We will also preserve a trailing slash if the original path had one, to indicate a directory.
        if (pathAsString.Length > 1 && pathAsString.EndsWith('/') && stack.Last() != "..")
        {
            finalPath += "/";
        }

        if (isAbsolute)
        {
            finalPath = "/" + finalPath;
        }

        _path = finalPath;
        return _path;
    }

    /// <summary>Enumerates the files and subdirectories in this directory, returning each as a <see cref="FilePath"/>.</summary>
    public IEnumerable<FilePath> EnumerateDirectoryEntries()
    {
        return Directory.EnumerateFileSystemEntries(Path).Select(entry => (FilePath) entry);
    }

    /// <summary>
    /// Determines whether the specified <see cref="FilePath"/> is equal to the current object.
    /// Equality is based on the raw, un-normalized path string.
    /// </summary>
    public bool Equals(FilePath other) => _path == other._path;

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    public override bool Equals(object? obj) => obj is FilePath other && Equals(other);

    /// <summary>
    /// Serves as the default hash function.
    /// </summary>
    public override int GetHashCode() => Path.GetHashCode();

    /// <summary>
    /// Determines whether two specified <see cref="FilePath"/> objects have the same value.
    /// </summary>
    public static bool operator ==(FilePath left, FilePath right) => left.Equals(right);

    /// <summary>
    /// Determines whether two specified <see cref="FilePath"/> objects have different values.
    /// </summary>
    public static bool operator !=(FilePath left, FilePath right) => !left.Equals(right);
}
