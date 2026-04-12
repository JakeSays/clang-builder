using Std.BuildTools.Clang.Ansi;

namespace Std.BuildTools.Clang;

public static class Log
{
    private static StreamWriter? _writer;
    private static readonly Lock _lock = new();

    public static string EscapeText(string text) => AnsiColorFormatter.EscapeText(text);
    private static string Format(string text) => AnsiColorFormatter.FormatText(text);

    public static void Initialize(FilePath workDir)
    {
        var logPath = workDir / "build-log.txt";
        lock (_lock)
        {
            _writer?.Dispose();
            Directory.CreateDirectory(workDir);
            File.Delete(logPath);
            _writer = new StreamWriter(logPath, append: false, System.Text.Encoding.UTF8) { AutoFlush = true };
        }
    }

    private static void Write(string line)
    {
        lock (_lock)
        {
            _writer?.WriteLine(line);
        }
    }

    private static bool IsRedirected => Console.IsOutputRedirected;

    public static void Error(string message)
    {
        Write(message);
        if (IsRedirected)
        {
            Console.WriteLine(message);
            return;
        }

        Console.WriteLine(Format($"<red,{EscapeText(message)}>"));
    }

    public static void Warning(string message)
    {
        Write(message);
        if (IsRedirected)
        {
            Console.WriteLine(message);
            return;
        }
        Console.WriteLine(Format($"<yellow,{EscapeText(message)}>"));
    }

    public static void Info(string message)
    {
        Write(message);
        Console.WriteLine(message);
    }

    public static void BlankLine()
    {
        Write("");
        Console.WriteLine();
    }

    public static void Info(LogColor color, string message)
    {
        Write(message);
        if (IsRedirected)
        {
            Console.WriteLine(message);
            return;
        }

        Console.WriteLine(Format($"<{ColorToMarkup(color)},{EscapeText(message)}>"));
    }

    private static string ColorToMarkup(LogColor color) => color switch
    {
        LogColor.Black           => Color.Black.Name,
        LogColor.Blue            => Color.Blue.Name,
        LogColor.Cyan            => Color.Aqua.Name,
        LogColor.DarkBlue        => Color.DarkBlue.Name,
        LogColor.DarkCyan        => Color.DarkCyan.Name,
        LogColor.DarkGray        => Color.DarkGray.Name,
        LogColor.DarkGreen       => Color.DarkGreen.Name,
        LogColor.DarkMagenta     => Color.DarkMagenta.Name,
        LogColor.DarkOrange      => Color.DarkOrange.Name,
        LogColor.DarkRed         => Color.DarkRed.Name,
        LogColor.DarkYellow      => Color.DarkYellow.Name,
        LogColor.DeepSkyBlue     => Color.DeepSkyBlue1.Name,
        LogColor.Fuchsia         => Color.Fuchsia.Name,
        LogColor.Gold            => Color.Gold1.Name,
        LogColor.Gray            => Color.Gray.Name,
        LogColor.Green           => Color.Green.Name,
        LogColor.HotPink         => Color.HotPink.Name,
        LogColor.IndianRed       => Color.IndianRed.Name,
        LogColor.Khaki           => Color.Khaki1.Name,
        LogColor.Lime            => Color.Lime.Name,
        LogColor.Magenta         => Color.Fuchsia.Name,
        LogColor.Maroon          => Color.Maroon.Name,
        LogColor.MediumPurple    => Color.MediumPurple.Name,
        LogColor.MediumTurquoise => Color.MediumTurquoise.Name,
        LogColor.Navy            => Color.Navy.Name,
        LogColor.Olive           => Color.Olive.Name,
        LogColor.Orange          => Color.Orange1.Name,
        LogColor.OrangeRed       => Color.OrangeRed1.Name,
        LogColor.Orchid          => Color.Orchid.Name,
        LogColor.Pink            => Color.Pink1.Name,
        LogColor.Plum            => Color.Plum1.Name,
        LogColor.Purple          => Color.Purple.Name,
        LogColor.Red             => Color.Red.Name,
        LogColor.RoyalBlue       => Color.RoyalBlue1.Name,
        LogColor.SaddleBrown     => Color.DarkOrange3.Name,
        LogColor.Salmon          => Color.Salmon1.Name,
        LogColor.SeaGreen        => Color.SeaGreen1.Name,
        LogColor.Silver          => Color.Silver.Name,
        LogColor.SkyBlue         => Color.SkyBlue1.Name,
        LogColor.SlateBlue       => Color.SlateBlue1.Name,
        LogColor.SpringGreen     => Color.SpringGreen1.Name,
        LogColor.SteelBlue       => Color.SteelBlue.Name,
        LogColor.Tan             => Color.Tan.Name,
        LogColor.Teal            => Color.Teal.Name,
        LogColor.Thistle         => Color.Thistle1.Name,
        LogColor.Tomato          => Color.OrangeRed1.Name,
        LogColor.Turquoise       => Color.Turquoise2.Name,
        LogColor.Violet          => Color.Violet.Name,
        LogColor.Wheat           => Color.Wheat1.Name,
        LogColor.White           => Color.White.Name,
        LogColor.Yellow          => Color.Yellow.Name,
        LogColor.YellowGreen     => Color.GreenYellow.Name,
        _ => throw new ArgumentOutOfRangeException(nameof(color), color, null)
    };
}
