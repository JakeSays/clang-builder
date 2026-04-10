using System.Text;
// ReSharper disable InconsistentNaming

namespace Std.BuildTools.Clang;

internal enum ArgType
{
    Dash,
    DashDash,
    Text
}

public enum Quoted
{
    Yes,
    No
}

public enum Separator
{
    Equal,
    Space,
    None
}

internal static class ArgBuilderExtensions
{
    public static bool ToBool(this Quoted quoted) => quoted == Quoted.Yes;
}

internal sealed class Arg
{
    public ArgType Type { get; }
    public string Name { get; }
    public string? Value { get; }
    public Quoted Quoted { get; }
    public Separator Separator { get; }

    public Arg(
        ArgType type,
        string name,
        string? value,
        Quoted quoted = Quoted.No,
        Separator separator = Separator.Space)
    {
        Name = name;
        Type = type;
        Value = value;
        Quoted = quoted;
        Separator = separator;
    }
}

public sealed class ArgBuilder
{
    private readonly List<Arg> _args = new(100);
    private readonly StringBuilder _builder = new(4_096);
    private ArgBuilder? _wlBuilder = null;

    public void Reset()
    {
        _args.Clear();
        _builder.Clear();
    }

    public ArgBuilder Text(string value, Quoted quoted = Quoted.No)
    {
        if (string.IsNullOrEmpty(value))
        {
            return this;
        }

        if (quoted == Quoted.Yes)
        {
            value = value.Quoted();
        }
        _args.Add(new Arg(ArgType.Text, value, null));
        return this;
    }
    public ArgBuilder TextIf(bool condition, string value, Quoted quoted = Quoted.No)
        => condition ? Text(value, quoted) : this;

    public ArgBuilder Define(string name, string? value = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _args.Add(
            new Arg(
                ArgType.Dash, $"D{name}", value, Quoted.No, value != null
                    ? Separator.Equal
                    : Separator.Space));
        return this;
    }
    public ArgBuilder DefineIf(bool condition, string name, string? value = null)
        => condition ? Define(name, value) : this;

    public ArgBuilder DefineQuoted(string name, string value, Separator separator = Separator.Equal)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(value);
        _args.Add(new Arg(ArgType.Dash, $"D{name}", value, Quoted.Yes, separator));
        return this;
    }
    public ArgBuilder DefineQuotedIf(bool condition, string name, string value, Separator separator = Separator.Equal)
        => condition ? DefineQuoted(name, value, separator) : this;

    public ArgBuilder Target(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        _args.Add(new Arg(ArgType.DashDash, "target", value, separator: Separator.Equal));
        return this;
    }
    public ArgBuilder TargetIf(bool condition, string value)
        => condition ? Target(value) : this;

    public ArgBuilder GccToolchain(string path)
    {
        _args.Add(new Arg(ArgType.DashDash, "gcc-toolchain", path, separator: Separator.Equal));
        return this;
    }
    public ArgBuilder GccToolchainIf(bool condition, string path)
        => condition ? GccToolchain(path) : this;

    public ArgBuilder Sysroot(string value, Quoted quoted = Quoted.No)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        _args.Add(new Arg(ArgType.DashDash, "sysroot", value, quoted, Separator.Equal));
        return this;
    }
    public ArgBuilder SysrootIf(bool condition, string value, Quoted quoted = Quoted.No)
        => condition ? Sysroot(value, quoted) : this;

    public ArgBuilder ISystem(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        _args.Add(new Arg(ArgType.Dash, "isystem", path));
        return this;
    }
    public ArgBuilder ISystemIf(bool condition, string path)
        => condition ? ISystem(path) : this;

    public ArgBuilder Include(string path, Quoted quoted = Quoted.No)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        _args.Add(new Arg(ArgType.Dash, "I", path, quoted, Separator.None));
        return this;
    }
    public ArgBuilder IncludeIf(bool condition, string path, Quoted quoted = Quoted.No)
        => condition ? Include(path, quoted) : this;

    public ArgBuilder UseLd(string linker)
    {
        ArgumentException.ThrowIfNullOrEmpty(linker);
        _args.Add(new Arg(ArgType.Dash, "fuse-ld", linker, Quoted.No, Separator.Equal));
        return this;
    }
    public ArgBuilder UseLdIf(bool condition, string linker)
        => condition ? UseLd(linker) : this;

    public ArgBuilder ColorAlways()
    {
        _args.Add(new Arg(ArgType.Dash, "fdiagnostics-color", "always", Quoted.No, Separator.Equal));
        return this;
    }
    public ArgBuilder ColorAlwaysIf(bool condition)
        => condition ? ColorAlways() : this;

    public ArgBuilder CmakeSwitch(string name, bool isOn)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _args.Add(new Arg(
            ArgType.Dash,
            $"D{name}",
            isOn ? "ON" : "OFF",
            separator: Separator.Equal));
        return this;
    }
    public ArgBuilder CmakeSwitchIf(bool condition, string name, bool isOn)
        => condition ? CmakeSwitch(name, isOn) : this;

    public ArgBuilder CmakeOn(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _args.Add(new Arg(ArgType.Dash, $"D{name}", "ON", separator: Separator.Equal));
        return this;
    }
    public ArgBuilder CmakeOnIf(bool condition, string name)
        => condition ? CmakeOn(name) : this;

    public ArgBuilder CmakeOff(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _args.Add(new Arg(ArgType.Dash, $"D{name}", "OFF", separator: Separator.Equal));
        return this;
    }
    public ArgBuilder CmakeOffIf(bool condition, string name)
        => condition ? CmakeOff(name) : this;

    public ArgBuilder Pic()
    {
        _args.Add(new Arg(ArgType.Dash, "fPIC", null));
        return this;
    }
    public ArgBuilder PicIf(bool condition)
        => condition ? Pic() : this;

    public ArgBuilder StdLib(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        _args.Add(new Arg(ArgType.Dash, "stdlib", value, separator: Separator.Equal));
        return this;
    }
    public ArgBuilder StdLibIf(bool condition, string value)
        => condition ? StdLib(value) : this;

    public ArgBuilder RtLib(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        _args.Add(new Arg(ArgType.Dash, "rtlib", value, separator: Separator.Equal));
        return this;
    }
    public ArgBuilder RtLibIf(bool condition, string value)
        => condition ? RtLib(value) : this;

    public ArgBuilder NoStdLibCxx()
    {
        _args.Add(new Arg(ArgType.Dash, "nostdlib++", null));
        return this;
    }
    public ArgBuilder NoStdLibCxxIf(bool condition)
        => condition ? NoStdLibCxx() : this;

    public ArgBuilder NoStdIncCxx()
    {
        _args.Add(new Arg(ArgType.Dash, "nostdinc++", null));
        return this;
    }
    public ArgBuilder NoStdIncCxxIf(bool condition)
        => condition ? NoStdIncCxx() : this;

    public ArgBuilder NoStartFiles()
    {
        _args.Add(new Arg(ArgType.Dash, "nostartfiles", null));
        return this;
    }
    public ArgBuilder NoStartFilesIf(bool condition)
        => condition ? NoStartFiles() : this;

    public ArgBuilder UnwindLib(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        _args.Add(new Arg(ArgType.Dash, "unwindlib", value, separator: Separator.Equal));
        return this;
    }
    public ArgBuilder UnwindLibIf(bool condition, string value)
        => condition ? UnwindLib(value) : this;

    public ArgBuilder Wl(params ReadOnlySpan<string> values)
    {
        var value = $"-Wl,{string.Join(',', values)}";
        _args.Add(new Arg(ArgType.Text, value, null));
        return this;
    }

    public ArgBuilder WlIf(bool condition, params ReadOnlySpan<string> values)
        => condition ? Wl(values) : this;

    public ArgBuilder Wl(Action<ArgBuilder> wlBuilder)
    {
        _wlBuilder ??= new ArgBuilder();
        wlBuilder(_wlBuilder);
        var value = $"-Wl,{_wlBuilder.Build(',')}";
        _args.Add(new Arg(ArgType.Text, value, null));
        return this;
    }

    public ArgBuilder Lib(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        _args.Add(new Arg(ArgType.Dash, "l", value, separator: Separator.None));
        return this;
    }
    public ArgBuilder LibIf(bool condition, string value)
        => condition ? Lib(value) : this;

    public ArgBuilder LibPath(string path, Quoted quoted = Quoted.No)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (quoted.ToBool())
        {
            path = path.Quoted();
        }

        _args.Add(new Arg(ArgType.Dash, "L", path, quoted, Separator.None));
        return this;
    }
    public ArgBuilder LibPathIf(bool condition, string path, Quoted quoted = Quoted.No)
        => condition ? LibPath(path, quoted) : this;

    public ArgBuilder NinjaGenerator()
    {
        _args.Add(new Arg(ArgType.Dash, "G", "Ninja"));
        return this;
    }
    public ArgBuilder NinjaGeneratorIf(bool condition)
        => condition ? NinjaGenerator() : this;

    public ArgBuilder CmakeSourceDir(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        _args.Add(new Arg(ArgType.Dash, "S", path, Quoted.Yes));
        return this;
    }
    public ArgBuilder CmakeSourceDirIf(bool condition, string path)
        => condition ? CmakeSourceDir(path) : this;

    public ArgBuilder CmakeBinaryDir(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        _args.Add(new Arg(ArgType.Dash, "B", path, Quoted.Yes));
        return this;
    }
    public ArgBuilder CmakeBinaryDirIf(bool condition, string path)
        => condition ? CmakeBinaryDir(path) : this;

    public ArgBuilder CmakeBuildRelease()
    {
        Define("CMAKE_BUILD_TYPE", "Release");
        return this;
    }
    public ArgBuilder CmakeBuildReleaseIf(bool condition)
        => condition ? CmakeBuildRelease() : this;

    public ArgBuilder CmakeBuildDebug()
    {
        Define("CMAKE_BUILD_TYPE", "Debug");
        return this;
    }
    public ArgBuilder CmakeBuildDebugIf(bool condition)
        => condition ? CmakeBuildDebug() : this;

    public ArgBuilder Dash(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _args.Add(new Arg(ArgType.Dash, name, null));
        return this;
    }
    public ArgBuilder DashIf(bool condition, string name)
        => condition ? Dash(name) : this;

    public ArgBuilder Dash(
        string name,
        string value,
        Quoted quoted = Quoted.No,
        Separator separator = Separator.Space)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(value);
        _args.Add(new Arg(ArgType.Dash, name, value, quoted, separator));
        return this;
    }
    public ArgBuilder DashIf(bool condition, string name, string value, Quoted quoted = Quoted.No, Separator separator = Separator.Space)
        => condition ? Dash(name, value, quoted, separator) : this;

    public ArgBuilder DashAssigned(string name, string value, Quoted quoted = Quoted.No)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(value);
        _args.Add(new Arg(ArgType.Dash, name, value, quoted, Separator.Equal));
        return this;
    }
    public ArgBuilder DashAssignedIf(bool condition, string name, string value, Quoted quoted = Quoted.No)
        => condition ? DashAssigned(name, value, quoted) : this;

    public ArgBuilder DashDash(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _args.Add(new Arg(ArgType.DashDash, name, null));
        return this;
    }
    public ArgBuilder DashDashIf(bool condition, string name)
        => condition ? DashDash(name) : this;

    public ArgBuilder DashDash(
        string name,
        string value,
        Quoted quoted = Quoted.No,
        Separator separator = Separator.Space)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(value);
        _args.Add(new Arg(ArgType.DashDash, name, value, quoted, separator));
        return this;
    }
    public ArgBuilder DashDashIf(bool condition, string name, string value, Quoted quoted = Quoted.No, Separator separator = Separator.Space)
        => condition ? DashDash(name, value, quoted, separator) : this;

    public ArgBuilder DashDashAssigned(string name, string value, Quoted quoted = Quoted.No)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(value);
        _args.Add(new Arg(ArgType.DashDash, name, value, quoted, Separator.Equal));
        return this;
    }
    public ArgBuilder DashDashAssignedIf(bool condition, string name, string value, Quoted quoted = Quoted.No)
        => condition ? DashDashAssigned(name, value, quoted) : this;

    public string Build(char separator = ' ', bool reset = true)
    {
        _builder.Clear();
        Build(_builder, separator, false);
        var result = _builder.ToString();
        if (reset)
        {
            Reset();
        }

        return result;
    }

    public void Build(StringBuilder builder, char separator = ' ', bool reset = false)
    {
        foreach (var arg in _args)
        {
            if (builder.Length > 0)
            {
                builder.Append(separator);
            }

            switch (arg.Type)
            {
                case ArgType.Dash:
                    AppendArg(arg, "-", builder);
                    break;
                case ArgType.DashDash:
                    AppendArg(arg, "--", builder);
                    break;
                case ArgType.Text:
                    builder.Append(arg.Quoted.ToBool() ? arg.Name.Quoted() : arg.Name);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        if (reset)
        {
            Reset();
        }

        static void AppendArg(Arg arg, string prefix, StringBuilder builder)
        {
            builder.Append(prefix);
            builder.Append(arg.Name);
            if (arg.Value == null)
            {
                return;
            }

            switch (arg.Separator)
            {
                case Separator.Equal:
                    builder.Append('=');
                    break;
                case Separator.Space:
                    builder.Append(' ');
                    break;
                case Separator.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            builder.Append(arg.Quoted.ToBool() ? arg.Value.Quoted() : arg.Value);
        }
    }
}
