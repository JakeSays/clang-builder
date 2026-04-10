namespace Std.BuildTools.Clang;

internal readonly ref struct PatchResult
{
    public readonly bool Success;
    public readonly string Text;

    public PatchResult(bool success, string text)
    {
        Success = success;
        Text = text;
    }

    public static implicit operator bool(PatchResult result) => result.Success;
    public static explicit operator string(PatchResult result) => result.Text;
}

internal static class StringExtensions
{
    extension(string? value)
    {
        public bool HasValue => !string.IsNullOrWhiteSpace(value);

        public bool HasNoValue => string.IsNullOrWhiteSpace(value);
    }

    extension(string s)
    {
        public string Quoted()
        {
            return $"\"{s}\"";
        }

        public PatchResult Patch(string textToReplace, string replacement)
        {
            ArgumentException.ThrowIfNullOrEmpty(s);
            ArgumentException.ThrowIfNullOrEmpty(textToReplace);
            ArgumentNullException.ThrowIfNull(replacement);

            return !s.Contains(textToReplace)
                ? new PatchResult(false, s)
                : new PatchResult(true, s.Replace(textToReplace, replacement));
        }
    }
}
