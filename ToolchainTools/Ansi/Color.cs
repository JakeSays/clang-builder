using System.Runtime.InteropServices;


namespace Std.BuildTools.Clang.Ansi;

/// <summary>
/// Represents a color.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly partial struct Color : IEquatable<Color>
{
    private const byte LastColorNumber = 0xFD;
    private const byte DefaultIndicator = 0xFE;
    private const byte TrueColorIndicator = 0xFF;

    /// <summary>
    /// Gets the default color.
    /// </summary>
    public static Color Default { get; }

    static Color()
    {
        Default = new Color(DefaultIndicator, 0, 0, 0);
    }

    private readonly byte _number;
    private readonly byte _red;
    private readonly byte _green;
    private readonly byte _blue;

    /// <summary>
    /// Gets the number of the color, if any.
    /// </summary>
    internal byte Number => _number;

    public bool HasNumber => _number <= LastColorNumber;

    /// <summary>
    /// Gets the red component.
    /// </summary>
    public byte R => _red;

    /// <summary>
    /// Gets the green component.
    /// </summary>
    public byte G => _green;

    /// <summary>
    /// Gets the blue component.
    /// </summary>
    public byte B => _blue;

    /// <summary>
    /// Gets a value indicating whether or not this is the default color.
    /// </summary>
    internal bool IsDefault => Number == DefaultIndicator;

    public bool HasName => Number <= LastColorNumber;

    /// <summary>
    /// Initializes a new instance of the <see cref="Color"/> struct.
    /// </summary>
    /// <param name="red">The red component.</param>
    /// <param name="green">The green component.</param>
    /// <param name="blue">The blue component.</param>
    public Color(byte red, byte green, byte blue)
    {
        _red = red;
        _green = green;
        _blue = blue;
        _number = TrueColorIndicator;
    }

    internal Color(byte number, byte red, byte green, byte blue)
    {
        _red = red;
        _green = green;
        _blue = blue;
        _number = number;
    }

    /// <summary>
    /// Blends two colors.
    /// </summary>
    /// <param name="other">The other color.</param>
    /// <param name="factor">The blend factor.</param>
    /// <returns>The resulting color.</returns>
    public Color Blend(Color other, float factor)
    {
        // https://github.com/willmcgugan/rich/blob/f092b1d04252e6f6812021c0f415dd1d7be6a16a/rich/color.py#L494
        return new Color(
            (byte)(_red + ((other._red - _red) * factor)),
            (byte)(_green + ((other._green - _green) * factor)),
            (byte)(_blue + ((other._blue - _blue) * factor)));
    }

    /// <summary>
    /// Gets the hexadecimal representation of the color.
    /// </summary>
    /// <returns>The hexadecimal representation of the color.</returns>
    public string ToHex() => $"{_red:X2}{_green:X2}{_blue:X2}";

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = (int)2166136261;
            hash = (hash * 16777619) ^ _red.GetHashCode();
            hash = (hash * 16777619) ^ _green.GetHashCode();
            hash = (hash * 16777619) ^ _blue.GetHashCode();
            return hash;
        }
    }

    public override bool Equals(object? obj)
    {
        return obj is Color color && Equals(color);
    }

    public bool Equals(Color other)
    {
        return (IsDefault && other.IsDefault) ||
            (IsDefault == other.IsDefault
                && _red == other._red
                && _green == other._green
                && _blue == other._blue);
    }

    /// <summary>
    /// Checks if two <see cref="Color"/> instances are equal.
    /// </summary>
    /// <param name="left">The first color instance to compare.</param>
    /// <param name="right">The second color instance to compare.</param>
    /// <returns><c>true</c> if the two colors are equal, otherwise <c>false</c>.</returns>
    public static bool operator ==(Color left, Color right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Checks if two <see cref="Color"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first color instance to compare.</param>
    /// <param name="right">The second color instance to compare.</param>
    /// <returns><c>true</c> if the two colors are not equal, otherwise <c>false</c>.</returns>
    public static bool operator !=(Color left, Color right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Converts a <see cref="int"/> to a <see cref="Color"/>.
    /// </summary>
    /// <param name="number">The color number to convert.</param>
    public static explicit operator Color(int number)
    {
        return FromNumber(number);
    }

    /// <summary>
    /// Converts a <see cref="ConsoleColor"/> to a <see cref="Color"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    public static implicit operator Color(ConsoleColor color)
    {
        return FromConsoleColor(color);
    }

    /// <summary>
    /// Converts a <see cref="Color"/> to a <see cref="ConsoleColor"/>.
    /// </summary>
    /// <param name="color">The console color to convert.</param>
    public static implicit operator ConsoleColor(Color color)
    {
        return ToConsoleColor(color);
    }

    /// <summary>
    /// Converts a <see cref="Color"/> to a <see cref="ConsoleColor"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>A <see cref="ConsoleColor"/> representing the <see cref="Color"/>.</returns>
    public static ConsoleColor ToConsoleColor(Color color)
    {
        if (color.IsDefault)
        {
            return ConsoleColor.White;
        }

        return color.Number switch
        {
            0 => ConsoleColor.Black,
            1 => ConsoleColor.DarkRed,
            2 => ConsoleColor.DarkGreen,
            3 => ConsoleColor.DarkYellow,
            4 => ConsoleColor.DarkBlue,
            5 => ConsoleColor.DarkMagenta,
            6 => ConsoleColor.DarkCyan,
            7 => ConsoleColor.Gray,
            8 => ConsoleColor.DarkGray,
            9 => ConsoleColor.Red,
            10 => ConsoleColor.Green,
            11 => ConsoleColor.Yellow,
            12 => ConsoleColor.Blue,
            13 => ConsoleColor.Magenta,
            14 => ConsoleColor.Cyan,
            15 => ConsoleColor.White,
            _ => throw new InvalidOperationException("Cannot convert color to console color."),
        };
    }

    /// <summary>
    /// Converts a color number into a <see cref="Color"/>.
    /// </summary>
    /// <param name="number">The color number.</param>
    /// <returns>The color representing the specified color number.</returns>
    public static Color FromNumber(int number)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(number);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, LastColorNumber);

        return ColorList.Colors[number];
    }

    /// <summary>
    /// Creates a color from a hexadecimal string representation.
    /// </summary>
    /// <param name="hex">The hexadecimal string representation of the color.</param>
    /// <returns>The color created from the hexadecimal string.</returns>
    public static Color FromHex(ReadOnlySpan<char> hex)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(hex.Length, 0, nameof(hex));

        var index = 0;
        var length = hex.Length;

        if (hex[0] == '#')
        {
            length -= 1;
            index += 1;
        }

        if (length != 6 &&
            length != 3)
        {
            throw new ArgumentOutOfRangeException(nameof(hex));
        }

        byte r;
        byte g;
        byte b;

        // 3 digit hex codes are expanded to 6 digits
        // by doubling each digit, conform to CSS color codes

        if (length == 3)
        {
            r = Nibble(hex[index++]);
            g = Nibble(hex[index++]);
            b = Nibble(hex[index]);
            r = (byte) (r << 4 | r);
            g = (byte) (g << 4 | g);
            b = (byte) (b << 4 | b);
        }
        else
        {
            r = (byte) (Nibble(hex[index++]) << 4 | Nibble(hex[index++]));
            g = (byte) (Nibble(hex[index++]) << 4 | Nibble(hex[index++]));
            b = (byte) (Nibble(hex[index++]) << 4 | Nibble(hex[index]));
        }

        return new Color(r, g, b);

        static byte Nibble(char c) =>
            c switch
            {
                >= '0' and <= '9' => (byte) (c - '0'),
                >= 'a' and <= 'f' => (byte) (10 + (c - 'a')),
                >= 'A' and <= 'F' => (byte) (10 + (c - 'A')),
                _ => throw new ArgumentOutOfRangeException(nameof(c))
            };
    }

    /// <summary>
    /// Tries to convert a hexadecimal color code to a <see cref="Color"/> object.
    /// </summary>
    /// <param name="hex">The hexadecimal color code.</param>
    /// <param name="color">When this method returns, contains the <see cref="Color"/> equivalent of the hexadecimal color code,
    /// if the conversion succeeded, or <see cref="Color.Default"/> if the conversion failed.</param>
    /// <returns><c>true</c> if the conversion succeeded; otherwise, <c>false</c>.</returns>
    public static bool TryFromHex(string hex, out Color color)
    {
        try
        {
            color = FromHex(hex);
            return true;
        }
        catch
        {
            color = Default;
            return false;
        }
    }

    /// <summary>
    /// Converts a <see cref="ConsoleColor"/> to a <see cref="Color"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>A <see cref="Color"/> representing the <see cref="ConsoleColor"/>.</returns>
    public static Color FromConsoleColor(ConsoleColor color)
    {
        return color switch
        {
            ConsoleColor.Black => Black,
            ConsoleColor.Blue => Blue,
            ConsoleColor.Cyan => Aqua,
            ConsoleColor.DarkBlue => Navy,
            ConsoleColor.DarkCyan => Teal,
            ConsoleColor.DarkGray => Gray,
            ConsoleColor.DarkGreen => Green,
            ConsoleColor.DarkMagenta => Purple,
            ConsoleColor.DarkRed => Maroon,
            ConsoleColor.DarkYellow => Olive,
            ConsoleColor.Gray => Silver,
            ConsoleColor.Green => Lime,
            ConsoleColor.Magenta => Fuchsia,
            ConsoleColor.Red => Red,
            ConsoleColor.White => White,
            ConsoleColor.Yellow => Yellow,
            _ => Default,
        };
    }

    /// <summary>
    /// Converts the color to a markup string.
    /// </summary>
    /// <returns>A <see cref="string"/> representing the color as markup.</returns>
    public string ToMarkup()
    {
        return IsDefault
            ? "default"
            : Name;
    }

    public override string ToString()
    {
        return IsDefault
            ? "default"
            : $"{Name} (RGB={_red},{_green},{_blue})";
    }
}
