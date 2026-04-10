using System.Text;

namespace Std.BuildTools.Clang;

public class CompilerRtBuilder
{
    private readonly BuildConfiguration _config;
    private readonly Target _target;

    private readonly string _hostClang;
    private readonly string _hostClangxx;
    private readonly string _hostAr;
    private readonly string _hostNm;
    private readonly string _hostRanlib;

    public CompilerRtBuilder(
        BuildConfiguration config,
        Target target)
    {
        _config = config;
        _target = target;

        _hostClang = _config.InstallDir / "bin" / "clang";
        _hostClangxx = _config.InstallDir / "bin" / "clang++";
        _hostAr = _config.InstallDir / "bin" / "llvm-ar";
        _hostNm = _config.InstallDir / "bin" / "llvm-nm";
        _hostRanlib =_config.InstallDir / "bin" / "llvm-ranlib";
    }

    public async Task<bool> Build(CompilerRtMode mode)
    {
        Log.Info($"--- compiler-rt for {_target.Triple} (mode: {mode}) ---");


        if (mode is CompilerRtMode.All or CompilerRtMode.Builtins)
        {
            if (!await BuildBuiltins(isPic: false))
            {
                return false;
            }

            if (!await BuildBuiltins(isPic: true))
            {
                return false;
            }
        }

        if (mode is CompilerRtMode.All or CompilerRtMode.Sanitizers)
        {
            if (!await BuildSanitizers())
            {
                return false;
            }
        }

        Log.Info($"compiler-rt for {_target.Triple} complete.");
        return true;
    }

    private async Task<bool> BuildBuiltins(bool isPic)
    {
        var passName = isPic
            ? "builtins-pic"
            : "builtins";
        Log.Info(LogColor.Green, $"==> Pass: {passName}");

        var muslSuffix = _target.IsMusl ? "-musl" : "";
        var buildDir = _config.BuildDir / $"compiler-rt-{_target.ArchName}{muslSuffix}-{passName}";
        Directory.CreateDirectory(buildDir);

//        var normalizedTriple = _config.NormalizedTriples[_target.CmakeTriple];

        var flags = new ArgBuilder();
        flags
            .Target(_target.Triple)
            .Text(_target.ExtraFlags)
            .ColorAlways();
        if (isPic)
        {
            flags.Pic();
        }

        var cFlags = flags.Build();

        flags
            .Target(_target.Triple)
            .Text(_target.ExtraFlags)
            .Sysroot(_target.Sysroot)
            .LibPath(_target.Sysroot / "usr" / "lib" / _target.GnuTriple)
            .LibPath(_target.Sysroot / "lib")
            .LibPath(_target.Sysroot / "usr" / "lib")
            .UseLd("lld");

        var ldFlags = flags.Build();

        var configureArgs = GetCommonCmakeArgs(buildDir, cFlags, ldFlags);
        configureArgs
            .CmakeOn("COMPILER_RT_BUILD_BUILTINS")
            .CmakeOff("COMPILER_RT_BUILD_SANITIZERS")
            .CmakeOff("COMPILER_RT_BUILD_XRAY")
            .CmakeOff("COMPILER_RT_BUILD_LIBFUZZER")
            .CmakeOff("COMPILER_RT_BUILD_PROFILE")
            .CmakeOff("COMPILER_RT_BUILD_ORC")
            .CmakeOff("COMPILER_RT_BUILD_MEMPROF")
            .CmakeOff("COMPILER_RT_BUILD_CTX_PROFILE");

        if (!await ConfigureAndBuild(buildDir, passName, configureArgs))
        {
            return false;
        }

        var normalizedTriple = _config.NormalizedTriples[_target.CmakeTriple];

        if (isPic)
        {
            // Install PIC builtins alongside the non-PIC ones with a _pic suffix.
            // The path matches where cmake installed the non-PIC build via
            // LLVM_ENABLE_PER_TARGET_RUNTIME_DIR + COMPILER_RT_INSTALL_PATH.
            var picDestDir = _config.ClangResourceDir / "lib" / normalizedTriple;

            if (!Directory.Exists(picDestDir))
            {
                Log.Error($"ERROR: builtins install dir not found: {picDestDir}");
                return false;
            }

            foreach (var file in Directory.EnumerateFiles(buildDir, "libclang_rt.builtins*.a", SearchOption.AllDirectories))
            {
                var baseName = Path.GetFileNameWithoutExtension(file);
                var destPath = picDestDir / $"{baseName}_pic.a";
                File.Copy(file, destPath, true);
                Log.Info($"  installed: {destPath}");
            }
        }
        else
        {
            // Standard install for non-PIC
            var installArgs = new ArgBuilder()
                .DashDash("install", buildDir, Quoted.Yes);
            if (await ProcessRunner.Run("cmake", installArgs.Build()) != 0)
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> BuildSanitizers()
    {
        var passName = "sanitizers";
        Log.Info(LogColor.Green, $"==> Pass: {passName}");

        var muslSuffix = _target.IsMusl ? "-musl" : "";
        var buildDir = _config.BuildDir / $"compiler-rt-{_target.ArchName}{muslSuffix}";
        Directory.CreateDirectory(buildDir);

        var normalizedTriple = _config.NormalizedTriples[_target.CmakeTriple];

        var builtinsLib = Directory.EnumerateFiles(_config.ClangResourceDir, "libclang_rt.builtins*.a", SearchOption.AllDirectories)
            .FirstOrDefault(p => p.Contains(_target.ArchName) && p.Contains("musl") == _target.IsMusl && !p.Contains("_pic."));

        if (builtinsLib == null)
        {
            Log.Error($"ERROR: could not find installed libclang_rt.builtins.a under {_config.ClangResourceDir}");
            return false;
        }
        Log.Info($"Using builtins: {builtinsLib}");

        // Musl libc++ installs to a separate prefix to avoid overwriting glibc headers.
        var libCxxPrefix = _target.IsMusl
            ? _config.InstallDir / "lib-musl"
            : _config.InstallDir;
        var cxxLibDir = libCxxPrefix / "lib" / normalizedTriple;

        // Use our installed libc++ headers directly — cross sysroots have no C++ headers,
        // and using --gcc-toolchain causes cmake to inject -lstdc++. This matches the
        // approach in build-compiler-rt.sh.
        var flags = new ArgBuilder()
            .Target(_target.Triple)
            .Text(_target.ExtraFlags)
            .ColorAlways()
            .ISystem(libCxxPrefix / "include" / normalizedTriple / "c++" / "v1")
            .ISystem(libCxxPrefix / "include" / "c++" / "v1")
            // musl doesn't expose stat64/off64_t etc. without this; compiler-rt's
            // sanitizer_linux.cpp uses struct stat64 unconditionally.
            .DefineIf(_target.IsMusl, "_LARGEFILE64_SOURCE")
            // musl has no rune table; libc++ falls back to its built-in one with this define.
            .DefineIf(_target.IsMusl, "_LIBCPP_PROVIDES_DEFAULT_RUNE_TABLE");

        var cFlags = flags.Build();

        flags
            .Target(_target.Triple)
            .Text(_target.ExtraFlags)
            .Sysroot(_target.Sysroot)
            .LibPath(_target.Sysroot / "usr" / "lib" / _target.GnuTriple)
            .LibPath(_target.Sysroot / "lib")
            .LibPath(_target.Sysroot / "usr" / "lib")
            .UseLd("lld")
            .RtLib("compiler-rt")
            .UnwindLib("none")
            .LibPath(Path.GetDirectoryName(builtinsLib)!);

        if (_target.IsMusl)
        {
            flags.LibPath(cxxLibDir)
                .StdLib("libc++");
        }
        flags.NoStdLibCxx();

        var ldFlags = flags.Build();

        var configureArgs = GetCommonCmakeArgs(buildDir, cFlags, ldFlags);
        configureArgs
            .CmakeOff("COMPILER_RT_BUILD_BUILTINS")
            .CmakeOn("COMPILER_RT_BUILD_SANITIZERS")
            .CmakeOn("COMPILER_RT_USE_BUILTINS_LIBRARY")
            .Define("SANITIZER_CXX_ABI", "libc++")
            .Define("SANITIZER_CXX_ABI_LIBRARY", "c++")
            .CmakeOn("LLVM_ENABLE_LIBCXX")
            // Clear implicit C++ link libs so cmake doesn't inject -lstdc++
            .Define("CMAKE_CXX_IMPLICIT_LINK_LIBRARIES", "")
            .Define("CMAKE_CXX_IMPLICIT_LINK_DIRECTORIES", "")
            .CmakeOff("COMPILER_RT_BUILD_LIBFUZZER")
            .CmakeOn("COMPILER_RT_BUILD_PROFILE")
            .CmakeOff("COMPILER_RT_BUILD_ORC")
            // GWP-ASan uses execinfo.h (backtrace()) which is a glibc extension not in musl
            .CmakeSwitch("COMPILER_RT_BUILD_GWP_ASAN", !_target.IsMusl);

        if (!await ConfigureAndBuild(buildDir, passName, configureArgs))
        {
            return false;
        }

        var installArgs = new ArgBuilder()
            .DashDash("install", buildDir, Quoted.Yes);
        return await ProcessRunner.Run("cmake", installArgs.Build()) == 0;
    }

    private async Task<bool> ConfigureAndBuild(FilePath buildDir, string passName, ArgBuilder configureArgs)
    {
        if (!await Configurator.Configure(buildDir, _config.ForceReconfigure, configureArgs.Build(), $"compiler-rt {passName} for {_target.Triple}"))
        {
            return false;
        }

        Log.Info($"Building compiler-rt {passName} for {_target.Triple} ({_config.Jobs} jobs)...");
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

    private ArgBuilder GetCommonCmakeArgs(string buildDir, string cFlags, string ldFlags)
    {
        return new ArgBuilder()
            .NinjaGenerator()
            .CmakeSourceDir(_config.SourceDir / "compiler-rt")
            .CmakeBinaryDir(buildDir)
            .CmakeBuildRelease()
            .Define("CMAKE_INSTALL_PREFIX", _config.InstallDir)
            .Define("CMAKE_C_COMPILER", _hostClang)
            .Define("CMAKE_CXX_COMPILER", _hostClangxx)
            .Define("CMAKE_AR", _hostAr)
            .Define("CMAKE_NM", _hostNm)
            .Define("CMAKE_RANLIB", _hostRanlib)
            .Define("CMAKE_SYSTEM_NAME", "Linux")
            .Define("CMAKE_SYSTEM_PROCESSOR", _target.CmakeArch)
            .Define("CMAKE_SYSROOT_COMPILE", _target.Sysroot)
            .DefineQuoted("CMAKE_C_FLAGS_INIT", cFlags)
            .DefineQuoted("CMAKE_CXX_FLAGS_INIT", cFlags)
            .DefineQuoted("CMAKE_EXE_LINKER_FLAGS_INIT", ldFlags)
            .DefineQuoted("CMAKE_SHARED_LINKER_FLAGS_INIT", ldFlags)
            .DefineQuoted("CMAKE_MODULE_LINKER_FLAGS_INIT", ldFlags)
            .CmakeOn("CMAKE_C_COMPILER_WORKS")
            .CmakeOn("CMAKE_CXX_COMPILER_WORKS")
            .Define("CMAKE_TRY_COMPILE_TARGET_TYPE", "STATIC_LIBRARY")
            .CmakeOn("TEST_COMPILE_ONLY")
            .Define("CMAKE_C_COMPILER_TARGET", _target.CmakeTriple)
            .Define("CMAKE_CXX_COMPILER_TARGET", _target.CmakeTriple)
            .Define("CMAKE_ASM_COMPILER_TARGET", _target.CmakeTriple)
            .CmakeOn("COMPILER_RT_DEFAULT_TARGET_ONLY")
            .Define("COMPILER_RT_SUPPORTED_ARCH", _target.ArchName)
            .CmakeOff("COMPILER_RT_INCLUDE_TESTS")
            .Define("COMPILER_RT_TEST_COMPILER", _hostClang)
            .Define("LLVM_DIR", _config.InstallDir / "lib" / "cmake" / "llvm")
            .Define("LLVM_CONFIG_PATH", _config.InstallDir / "bin" / "llvm-config")
            .CmakeOn("LLVM_ENABLE_PER_TARGET_RUNTIME_DIR")
            .Define("COMPILER_RT_INSTALL_PATH:PATH", _config.ClangResourceDir)
            .CmakeOff("LLVM_ENABLE_ZLIB")
            .CmakeOff("LLVM_ENABLE_ZSTD")
            .CmakeOff("LLVM_ENABLE_TERMINFO");
    }
}
