using Spectre.Console;

namespace Std.BuildTools.Clang;

public static class Log
{
    private static StreamWriter? _writer;
    private static readonly Lock _lock = new();

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
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]");
    }

    public static void Warning(string message)
    {
        Write(message);
        if (IsRedirected)
        {
            Console.WriteLine(message);
            return;
        }
        AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(message)}[/]");
    }

    public static void Info(string message)
    {
        Write(message);
        if (IsRedirected)
        {
            Console.WriteLine(message);
            return;
        }
        AnsiConsole.WriteLine(message);
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
        var colorName = ColorToMarkup(color);
        AnsiConsole.MarkupLine($"[{colorName}]{Markup.Escape(message)}[/]");
    }

    private static string ColorToMarkup(LogColor color) => color switch
    {
        LogColor.Black          => "black",
        LogColor.Blue           => "blue",
        LogColor.Cyan           => "cyan",
        LogColor.DarkBlue       => "darkblue",
        LogColor.DarkCyan       => "darkcyan",
        LogColor.DarkGray       => "darkgray",
        LogColor.DarkGreen      => "darkgreen",
        LogColor.DarkMagenta    => "darkmagenta",
        LogColor.DarkOrange     => "darkorange",
        LogColor.DarkRed        => "darkred",
        LogColor.DarkYellow     => "darkyellow",
        LogColor.DeepSkyBlue    => "deepskyblue",
        LogColor.Fuchsia        => "fuchsia",
        LogColor.Gold           => "gold",
        LogColor.Gray           => "gray",
        LogColor.Green          => "green",
        LogColor.HotPink        => "hotpink",
        LogColor.IndianRed      => "indianred",
        LogColor.Khaki          => "khaki",
        LogColor.Lime           => "lime",
        LogColor.Magenta        => "magenta",
        LogColor.Maroon         => "maroon",
        LogColor.MediumPurple   => "mediumpurple",
        LogColor.MediumTurquoise=> "mediumturquoise",
        LogColor.Navy           => "navy",
        LogColor.Olive          => "olive",
        LogColor.Orange         => "orange",
        LogColor.OrangeRed      => "orangered",
        LogColor.Orchid         => "orchid",
        LogColor.Pink           => "pink",
        LogColor.Plum           => "plum",
        LogColor.Purple         => "purple",
        LogColor.Red            => "red",
        LogColor.RoyalBlue      => "royalblue",
        LogColor.SaddleBrown    => "saddlebrown",
        LogColor.Salmon         => "salmon",
        LogColor.SeaGreen       => "seagreen",
        LogColor.Silver         => "silver",
        LogColor.SkyBlue        => "skyblue",
        LogColor.SlateBlue      => "slateblue",
        LogColor.SpringGreen    => "springgreen",
        LogColor.SteelBlue      => "steelblue",
        LogColor.Tan            => "tan",
        LogColor.Teal           => "teal",
        LogColor.Thistle        => "thistle",
        LogColor.Tomato         => "tomato",
        LogColor.Turquoise      => "turquoise",
        LogColor.Violet         => "violet",
        LogColor.Wheat          => "wheat",
        LogColor.White          => "white",
        LogColor.Yellow         => "yellow",
        LogColor.YellowGreen    => "yellowgreen",
        _ => throw new ArgumentOutOfRangeException(nameof(color), color, null)
    };
}
