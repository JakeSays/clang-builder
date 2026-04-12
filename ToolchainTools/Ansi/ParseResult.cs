namespace Std.BuildTools.Clang.Ansi;

internal readonly struct ParseResult
{
    public readonly int ErrorPosition;
    public readonly bool Success;

    private ParseResult(bool success)
    {
        ErrorPosition = -1;
        Success = success;
    }

    private ParseResult(int errorPosition)
    {
        ErrorPosition = errorPosition;
        Success = false;
    }

    public static implicit operator bool(ParseResult result) => result.Success;
    public static implicit operator ParseResult(bool  success) => new (success);
    public static implicit operator ParseResult(int errorPosition) => new (errorPosition);

    public void Deconstruct(out int errorPosition, out bool success)
    {
        errorPosition = ErrorPosition;
        success = Success;
    }
}