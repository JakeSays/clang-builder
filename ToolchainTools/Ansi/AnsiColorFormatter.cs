using System.Diagnostics;
using System.Text;


namespace Std.BuildTools.Clang.Ansi;

internal static partial class AnsiColorFormatter
{
    [ThreadStatic]
    private static StringBuilder? _builder;

    private static StringBuilder Builder => _builder ??= new StringBuilder(512);

    private const string Csi = "\e[";

    private static bool CanOutputColor => !Console.IsOutputRedirected || Debugger.IsAttached;

    private enum State
    {
        NotInElement,
        InElement
    }

    public static string FormatText(string text, bool escape = false)
    {
        if (escape)
        {
            text = EscapeText(text);
        }

        return !FormatText(text, out var result)
            ? text
            : result!;
    }

    public static ParseResult FormatText(ReadOnlySpan<char> text, out string? result)
    {
        Builder.Clear();
        var parseResult = FormatText(text, Builder);
        if (parseResult)
        {
            result = Builder.ToString();
            return true;
        }

        result = null;
        return parseResult;
    }

    public static ParseResult FormatText(ReadOnlySpan<char> text, StringBuilder builder)
    {
        var length = text.Length;
        var index = 0;
        var state = State.NotInElement;

        while (index < length)
        {
            var chr = text[index++];

            switch (chr)
            {
                case '<':
                {
                    if (state == State.InElement)
                    {
                        builder.Append(chr);
                        continue;
                    }
                    if (NextIs(text, '<'))
                    {
                        builder.Append(chr);
                        index++;
                        continue;
                    }

                    if (!ParseColor(text[index..], out var fgColor, out var colorNameLength))
                    {
                        return false;
                    }

                    Color? bgColor = null;

                    if (index + colorNameLength >= length)
                    {
                        return index;
                    }

                    index += colorNameLength;
                    if (text[index] == ':')
                    {
                        index++;
                        if (!ParseColor(text[index..], out var color, out colorNameLength))
                        {
                            return index;
                        }

                        bgColor = color;
                        if (index + colorNameLength >= length)
                        {
                            return index;
                        }
                        index += colorNameLength;
                    }

                    if (index >= length || text[index] != ',')
                    {
                        return index;
                    }

                    index++;

                    if (CanOutputColor)
                    {
                        AppendColors(fgColor, bgColor, builder);
                    }

                    state = State.InElement;
                    break;
                }
                case '>':
                {
                    if (state == State.NotInElement)
                    {
                        builder.Append(chr);
                        continue;
                    }

                    if (NextIs(text, '>'))
                    {
                        builder.Append(chr);
                        index++;
                        continue;
                    }

                    state = State.NotInElement;

                    if (CanOutputColor)
                    {
                        AppendTerminator(builder);
                    }

                    break;
                }
                default:
                    builder.Append(chr);
                    break;
            }
        }

        if (state != State.InElement)
        {
            return true;
        }

        if (CanOutputColor)
        {
            AppendTerminator(builder);
        }

        return index;

        bool NextIs(ReadOnlySpan<char> txt, char c)
        {
            if (index >= length)
            {
                return false;
            }

            return txt[index] == c;
        }

        static void AppendTerminator(StringBuilder builder)
        {
            builder.Append(Csi);
            builder.Append("0m");
        }
    }

    public static string EscapeText(string text) => EscapeText(text.AsSpan());

    public static string EscapeText(ReadOnlySpan<char> text)
    {
        var length = text.Length;
        var index = 0;
        Builder.Clear();

        while (index < length)
        {
            var chr = text[index++];
            Builder.Append(chr);

            switch (chr)
            {
                case '<':
                {
                    Builder.Append('<');
                    break;
                }
                case '>':
                {
                    Builder.Append('>');
                    break;
                }
            }
        }

        return Builder.ToString();
    }

    private static void AppendColors(Color fgColor, Color? bgColor, StringBuilder builder)
    {
        const byte foregroundSelector = 38;
        const byte backgroundSelector = 34;
        const byte indexedSelector = 5;
        const byte trueColorSelector = 2;

        builder.Append(Csi);

        AppendColor(fgColor, true, builder);

        if (bgColor != null)
        {
            AppendColor(bgColor.Value, false, builder);
        }

        builder.Append('m');

        static void AppendColor(Color color, bool foreground, StringBuilder builder)
        {
            builder.Append(
                foreground
                    ? foregroundSelector
                    : backgroundSelector);
            builder.Append(';');
            if (color.HasNumber)
            {
                builder.Append(indexedSelector);
                builder.Append(';');
                builder.Append(color.Number);
                return;
            }

            builder.Append(trueColorSelector);
            builder.Append(';');
            builder.Append(color.R);
            builder.Append(';');
            builder.Append(color.G);
            builder.Append(';');
            builder.Append(color.G);
        }
    }
}
