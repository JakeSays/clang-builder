namespace Std.BuildTools.Clang;

public class LibCxxBuilder
{
    private readonly BuildConfiguration _config;
    private readonly Target _target;

    private readonly FilePath _hostClang;
    private readonly FilePath _hostClangxx;
    private readonly FilePath _hostAr;
    private readonly FilePath _hostNm;
    private readonly FilePath _hostRanlib;
    private readonly FilePath _installPrefix;

    public LibCxxBuilder(BuildConfiguration config, Target target)
    {
        _config = config;
        _target = target;

        var hostBin = config.InstallDir / "bin";
        _hostClang = hostBin / "clang";
        _hostClangxx = hostBin / "clang++";
        _hostAr = hostBin / "llvm-ar";
        _hostNm = hostBin / "llvm-nm";
        _hostRanlib = hostBin / "llvm-ranlib";

        _installPrefix = target.IsMusl
            ? config.InstallDir / "lib-musl"
            : config.InstallDir;
    }

    public async Task<bool> Build()
    {
        var buildSuffix = _target.IsMusl ? "-musl" : "";
        Log.Info($"--- libc++ for {_target.Triple}{buildSuffix} ---");

        if (!await BuildLibs(isPic: false))
        {
            return false;
        }

        if (!await BuildLibs(isPic: true))
        {
            return false;
        }

        Log.Info($"libc++ for {_target.Triple}{buildSuffix} complete.");
        return true;
    }

    private async Task<bool> BuildLibs(bool isPic)
    {
        var passName = isPic ? "libcxx-pic" : "libcxx";
        var buildSuffix = _target.IsMusl ? "-musl" : "";
        var buildDirName = isPic
            ? $"libcxx-{_target.ArchName}{buildSuffix}-pic"
            : $"libcxx-{_target.ArchName}{buildSuffix}";
        var buildDir = _config.BuildDir / buildDirName;

        Log.Info(LogColor.Green, $"==> Pass: {(isPic ? "PIC" : "non-PIC")}");
        Directory.CreateDirectory(buildDir);

        var normalizedTriple = _config.NormalizedTriples[_target.CmakeTriple];
        var configureArgs = GetCmakeArgs(isPic, buildDir, normalizedTriple);
        if (configureArgs == null)
        {
            return false;
        }

        if (!await ConfigureAndBuild(buildDir, passName, configureArgs))
        {
            return false;
        }

        if (isPic)
        {
            // Install PIC libs to a temp prefix, then copy with _pic suffix
            var picTmpDir = buildDir / "pic-install";
            var installArgs = new ArgBuilder()
                .DashDash("install", buildDir, Quoted.Yes)
                .DashDash("prefix", picTmpDir, Quoted.Yes);
            if (await ProcessRunner.Run("cmake", installArgs.Build()) != 0)
            {
                return false;
            }

            var libDest = _installPrefix / "lib" / normalizedTriple;
            Directory.CreateDirectory(libDest);

            foreach (var file in Directory.EnumerateFiles(picTmpDir, "*.a", SearchOption.AllDirectories))
            {
                var baseName = Path.GetFileNameWithoutExtension(file);
                var destPath = libDest / $"{baseName}_pic.a";
                File.Copy(file, destPath, true);
                Log.Info($"  installed: {destPath}");
            }
        }
        else
        {
            var installArgs = new ArgBuilder().DashDash("install", buildDir, Quoted.Yes);
            if (await ProcessRunner.Run("cmake", installArgs.Build()) != 0)
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> ConfigureAndBuild(FilePath buildDir, string passName, string configureArgs)
    {
        if (!await Configurator.Configure(buildDir, _config.ForceReconfigure, configureArgs, $"{passName} for {_target.Triple}"))
        {
            return false;
        }

        Log.Info($"Building {passName} for {_target.Triple} ({_config.Jobs} jobs)...");
        var buildArgs = new ArgBuilder()
            .DashDash("build", buildDir, Quoted.Yes)
            .DashDash("parallel", _config.Jobs.ToString());

        var buildExitCode = await ProcessRunner.Run("cmake", buildArgs.Build());
        if (buildExitCode != 0)
        {
            Log.Error($"ERROR: {passName} build finished with errors (exit {buildExitCode})");
            return false;
        }

        return true;
    }

    private string? GetCmakeArgs(bool isPic, string buildDir, string normalizedTriple)
    {
        // For musl targets the compiler-rt builtins are installed under the musl-specific
        // normalized triple (e.g. aarch64-unknown-linux-musl), not the gnu one.
        var rtTriple = _target.IsMusl
            ? normalizedTriple.Replace("linux-gnu", "linux-musl")
            : normalizedTriple;
        var rtBuiltinsDir = _config.ClangResourceDir / "lib" / rtTriple;
        if (!rtBuiltinsDir.Exists)
        {
            Log.Error($"ERROR: compiler-rt builtins dir not found: {rtBuiltinsDir}");
            return null;
        }
        Log.Info($"Using compiler-rt builtins dir: {rtBuiltinsDir}");

        return _target.IsMusl
            ? GetMuslCmakeArgs(isPic, buildDir, normalizedTriple, rtBuiltinsDir)
            : GetGlibcCmakeArgs(isPic, buildDir, normalizedTriple, rtBuiltinsDir);
    }

    private string GetGlibcCmakeArgs(bool isPic, string buildDir, string normalizedTriple, FilePath rtBuiltinsDir)
    {
        var (cFlagsStr, sharedLdFlagsStr, exeLdFlagsStr) = BuildCompileAndLinkFlags(isPic, rtBuiltinsDir, needsRuneTable: false);

        var args = GetCommonCmakeArgs(isPic, buildDir, normalizedTriple, cFlagsStr, sharedLdFlagsStr, exeLdFlagsStr);
        args.CmakeOff("LIBCXX_HAS_MUSL_LIBC");

        return args.Build();
    }

    private string GetMuslCmakeArgs(bool isPic, string buildDir, string normalizedTriple, FilePath rtBuiltinsDir)
    {
        // LIBCXX_HAS_MUSL_LIBC=ON causes macro-redefinition warnings; pass
        // _LIBCPP_PROVIDES_DEFAULT_RUNE_TABLE directly in cFlags instead.
        var (cFlagsStr, sharedLdFlagsStr, exeLdFlagsStr) = BuildCompileAndLinkFlags(isPic, rtBuiltinsDir, needsRuneTable: true);

        var args = GetCommonCmakeArgs(isPic, buildDir, normalizedTriple, cFlagsStr, sharedLdFlagsStr, exeLdFlagsStr);
        args.CmakeOff("LIBCXX_HAS_MUSL_LIBC");

        return args.Build();
    }

    private (string cFlags, string sharedLdFlags, string exeLdFlags) BuildCompileAndLinkFlags(bool isPic, FilePath rtBuiltinsDir, bool needsRuneTable)
    {
        var cFlagsStr = new ArgBuilder()
            .Target(_target.Triple)
            .Text(_target.ExtraFlags)
            .ColorAlways()
            .Dash("funwind-tables")
            .Dash("Wno-nullability-completeness")
            .PicIf(isPic)
            .DefineIf(needsRuneTable, "_LIBCPP_PROVIDES_DEFAULT_RUNE_TABLE")
            .Build();

        // -rtlib=compiler-rt: replaces -lgcc (not present in musl sysroots) with
        //   compiler-rt builtins. The builtins are pure arithmetic intrinsics and
        //   do not reference malloc, so they are safe to link into shared libs.
        // Multiarch paths come FIRST so libc.so (linker script) is found before libc.a.
        var ldBuilder = new ArgBuilder()
            .Target(_target.Triple)
            .Text(_target.ExtraFlags)
            .Sysroot(_target.Sysroot)
            .LibPathIf(!_target.IsMusl, _target.Sysroot / "lib" / _target.GnuTriple)
            .LibPathIf(!_target.IsMusl, _target.Sysroot / "usr" / "lib" / _target.GnuTriple)
            .LibPath(_target.Sysroot / "lib")
            .LibPath(_target.Sysroot / "usr" / "lib")
            .UseLd("lld")
            .RtLib("compiler-rt")
            .UnwindLib("none")
            .LibPath(rtBuiltinsDir);

        var sharedLdFlagsStr = ldBuilder.Build();
        var exeLdFlagsStr = sharedLdFlagsStr;

        return (cFlagsStr, sharedLdFlagsStr, exeLdFlagsStr);
    }

    private ArgBuilder GetCommonCmakeArgs(
        bool isPic,
        string buildDir,
        string normalizedTriple,
        string cFlagsStr,
        string sharedLdFlagsStr,
        string exeLdFlagsStr)
    {
        var args = new ArgBuilder()
            .NinjaGenerator()
            .CmakeSourceDir(_config.SourceDir / "runtimes")
            .CmakeBinaryDir(buildDir)
            .CmakeBuildRelease()
            .Define("CMAKE_INSTALL_PREFIX", _installPrefix)
            .Define("CMAKE_C_COMPILER", _hostClang)
            .Define("CMAKE_CXX_COMPILER", _hostClangxx)
            .Define("CMAKE_AR", _hostAr)
            .Define("CMAKE_NM", _hostNm)
            .Define("CMAKE_RANLIB", _hostRanlib)
            .Define("CMAKE_SYSTEM_NAME", "Linux")
            .Define("CMAKE_SYSROOT_COMPILE", _target.Sysroot)
            .DefineQuoted("CMAKE_C_FLAGS_INIT", cFlagsStr)
            .DefineQuoted("CMAKE_CXX_FLAGS_INIT", cFlagsStr)
            .CmakeOn("CMAKE_C_COMPILER_WORKS")
            .CmakeOn("CMAKE_CXX_COMPILER_WORKS")
            .Define("CMAKE_C_COMPILER_TARGET", _target.CmakeTriple)
            .Define("CMAKE_CXX_COMPILER_TARGET", _target.CmakeTriple)
            .Define("CMAKE_ASM_COMPILER_TARGET", _target.CmakeTriple)
            .DefineQuoted("CMAKE_EXE_LINKER_FLAGS", exeLdFlagsStr)
            .DefineQuoted("CMAKE_SHARED_LINKER_FLAGS", sharedLdFlagsStr)
            .DefineQuoted("CMAKE_MODULE_LINKER_FLAGS", sharedLdFlagsStr)
            .Define("LLVM_ENABLE_RUNTIMES", "libunwind;libcxxabi;libcxx")
            .Define("LLVM_CONFIG_PATH", _config.InstallDir / "bin" / "llvm-config")
            .CmakeOn("LLVM_ENABLE_PER_TARGET_RUNTIME_DIR")
            .Define("LLVM_DEFAULT_TARGET_TRIPLE:STRING", normalizedTriple)
            .Define("LLVM_TARGET_TRIPLE:STRING", normalizedTriple)
            .Define("LIBUNWIND_TARGET_TRIPLE", normalizedTriple)
            .CmakeSwitch("LIBUNWIND_ENABLE_SHARED", !isPic)
            .CmakeOn("LIBUNWIND_ENABLE_STATIC")
            .CmakeOn("LIBUNWIND_USE_COMPILER_RT")
            .Define("LIBCXXABI_TARGET_TRIPLE", normalizedTriple)
            .CmakeSwitch("LIBCXXABI_ENABLE_SHARED", !isPic)
            .CmakeOn("LIBCXXABI_ENABLE_STATIC")
            .CmakeOn("LIBCXXABI_USE_COMPILER_RT")
            .CmakeOn("LIBCXXABI_USE_LLVM_UNWINDER")
            .CmakeOff("LIBCXXABI_ENABLE_STATIC_UNWINDER")
            .Define("LIBCXXABI_LIBUNWIND_INCLUDES", _config.SourceDir / "libunwind" / "include")
            .Define("LIBCXX_TARGET_TRIPLE", normalizedTriple)
            .CmakeSwitch("LIBCXX_ENABLE_SHARED", !isPic)
            .CmakeOn("LIBCXX_ENABLE_STATIC")
            .CmakeOn("LIBCXX_USE_COMPILER_RT")
            .CmakeOn("LIBCXX_ENABLE_LOCALIZATION")
            .CmakeOn("LIBCXX_ENABLE_UNICODE")
            .CmakeOn("LIBCXX_ENABLE_WIDE_CHARACTERS")
            .Define("LIBCXX_CXX_ABI", "libcxxabi")
            .Define("LIBCXX_CXX_ABI_INCLUDE_PATHS", _config.SourceDir / "libcxxabi" / "include")
            .Define("LIBCXX_ABI_VERSION", "1")
            .CmakeOff("LIBCXX_INCLUDE_TESTS")
            .CmakeOff("LIBCXXABI_INCLUDE_TESTS")
            .CmakeOff("LIBUNWIND_INCLUDE_TESTS");

        if (!isPic)
        {
            args.CmakeOn("CXX_SUPPORTS_FNO_EXCEPTIONS_FLAG")
                .CmakeOn("CXX_SUPPORTS_FUNWIND_TABLES_FLAG");
        }

        return args;
    }
}
