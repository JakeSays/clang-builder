namespace Std.BuildTools.Clang;

public class LldbServerBuilder
{
    private readonly BuildConfiguration _config;
    private readonly Target _target;

    private readonly string _hostClang;
    private readonly string _hostClangxx;
    private readonly string _hostAr;
    private readonly string _hostNm;
    private readonly string _hostRanlib;
    private readonly string _hostTblgen;
    private readonly string _hostClangTblgen;
    private readonly string _hostLldbTblgen;
    private static readonly string[] ZLibPaths = ["usr/lib/libz.a", "lib/libz.a"];

    public LldbServerBuilder(BuildConfiguration config, Target target)
    {
        _config = config;
        _target = target;

        var hostBin = _config.InstallDir / "bin";
        _hostClang = hostBin / "clang";
        _hostClangxx = hostBin / "clang++";
        _hostAr = hostBin / "llvm-ar";
        _hostNm = hostBin / "llvm-nm";
        _hostRanlib = hostBin / "llvm-ranlib";
        _hostTblgen = hostBin / "llvm-tblgen";
        _hostClangTblgen = hostBin / "clang-tblgen";
        _hostLldbTblgen = hostBin / "lldb-tblgen";
    }

    public async Task<bool> Build()
    {
        var buildSuffix = _target.IsMusl
            ? "-musl"
            : "";
        Log.Info($"--- lldb-server for {_target.Triple}{buildSuffix} ---");

        var buildDir = _config.BuildDir / $"lldb-server-{_target.ArchName}{buildSuffix}";
        Directory.CreateDirectory(buildDir);

        var configureArgs = await GetCmakeArgs(buildDir);
        if (configureArgs == null)
        {
            return false;
        }

        if (!await Configurator.Configure(buildDir, _config.ForceReconfigure, configureArgs.Build(), $"lldb-server for {_target.Triple}"))
        {
            return false;
        }

        Log.Info($"Building lldb-server for {_target.Triple} ({_config.Jobs} jobs)...");
        var buildArgs = new ArgBuilder()
            .DashDash("build", buildDir, Quoted.Yes)
            .DashDash("parallel", _config.Jobs.ToString())
            .DashDash("target", "lldb-server");

        if (await ProcessRunner.Run("cmake", buildArgs.Build()) != 0)
        {
            Log.Warning("ERROR: lldb-server build finished with errors");
            return false;
        }

        var serverBin = buildDir / "bin" / "lldb-server";
        if (!serverBin.Exists)
        {
            Log.Error($"ERROR: lldb-server binary not found after build at {serverBin}");
            return false;
        }

        var destDir = _config.InstallDir / "bin" / $"{_target.ArchName}-linux";
        Directory.CreateDirectory(destDir);
        var destPath = destDir / "lldb-server";

        File.Copy(serverBin, destPath, true);
        await ProcessRunner.Run(_config.InstallDir / "bin" / "llvm-strip", destPath.AsQuotedPath());

        Log.Info($"lldb-server for {_target.Triple} installed at {destPath}");
        return true;
    }

    private async Task<ArgBuilder?> GetCmakeArgs(string buildDir)
    {
        var archLower = _target.Arch.Name;

        var rtBuiltinsDir = Directory.EnumerateFiles(_config.ClangResourceDir, "libclang_rt.builtins*.a", SearchOption.AllDirectories)
            .FirstOrDefault(p => p.Contains(archLower) && p.Contains("musl") == _target.IsMusl)?
            .Apply(Path.GetDirectoryName);

        if (rtBuiltinsDir == null)
        {
            Log.Error($"ERROR: compiler-rt builtins not found for {_target.Triple} under {_config.ClangResourceDir}");
            return null;
        }

        var normalizedTriple = _config.NormalizedTriples[_target.CmakeTriple];
        var cxxLibDir = _target.IsMusl
            ? _config.InstallDir / "lib-musl" / "lib" / normalizedTriple
            : _config.InstallDir / "lib" / normalizedTriple;

        var zlib = ZLibPaths
            .Select(p => _target.Sysroot / p)
            .FirstOrDefault(p => p.Exists);

        var cFlags = new ArgBuilder()
            .Target(_target.Triple)
            .Text(_target.ExtraFlags)
            .ColorAlways();

        // Musl libc++ headers live under lib-musl/; glibc under the main install prefix.
        var libCxxIncludePrefix = _target.IsMusl
            ? _config.InstallDir / "lib-musl" / "include"
            : _config.InstallDir / "include";

        var cxxFlags = new ArgBuilder()
            .Target(_target.Triple)
            .Text(_target.ExtraFlags)
            .ColorAlways()
            // -nostdinc++ blocks clang from implicitly adding the glibc include/c++/v1 path
            // when -stdlib=libc++ is present. Without it, #include_next <math.h> inside musl
            // libc++'s math.h picks up the glibc wrapper instead of the sysroot's math.h,
            // breaking FP_NAN, ::memcpy, etc.
            // -stdlib=libc++ is also included so that HandleLLVMStdlib.cmake's
            // check_cxx_compiler_flag("-stdlib=libc++") passes (clang warns "unused" but exits 0).
            .NoStdIncCxx()
            .StdLib("libc++")
            .Text("-Wno-unused-command-line-argument")
            // Triple-specific dir first so __config_site is found before the generic headers.
            .ISystem(libCxxIncludePrefix / normalizedTriple / "c++" / "v1")
            .ISystem(libCxxIncludePrefix / "c++" / "v1")
            // musl has no rune table; libc++ falls back to its built-in one with this define.
            .DefineIf(_target.IsMusl, "_LIBCPP_PROVIDES_DEFAULT_RUNE_TABLE");

        var ldFlags = new ArgBuilder()
            .Target(_target.Triple)
            .Text(_target.ExtraFlags)
            .Sysroot(_target.Sysroot)
            .LibPath(_target.Sysroot / "lib")
            .LibPath(_target.Sysroot / "usr" / "lib")
            .UseLd("lld")
            .Dash("static")
            .RtLib("compiler-rt")
            .UnwindLib("none")
            .LibPath(cxxLibDir)
            .LibPath(rtBuiltinsDir)
            .StdLib("libc++")
            .Lib("c++")
            .Lib("c++abi")
            .Lib("unwind")
            .Wl("--exclude-libs", "libunwind.a")
            .Lib("pthread")
            .Lib("dl");

        var ldFlagsStr = ldFlags.Build();
        var args = new ArgBuilder()
            .NinjaGenerator()
            .CmakeSourceDir(_config.SourceDir / "llvm")
            .CmakeBinaryDir(buildDir)
            .CmakeBuildRelease()
            .Define("CMAKE_C_COMPILER", _hostClang)
            .Define("CMAKE_CXX_COMPILER", _hostClangxx)
            .Define("CMAKE_AR", _hostAr)
            .Define("CMAKE_NM", _hostNm)
            .Define("CMAKE_RANLIB", _hostRanlib)
            .Define("CMAKE_SYSROOT_COMPILE", _target.Sysroot)
            .Define("CMAKE_SYSROOT_LINK", "") // Let linker flags handle it
            .DefineQuoted("CMAKE_C_FLAGS_INIT", cFlags.Build())
            .DefineQuoted("CMAKE_CXX_FLAGS_INIT", cxxFlags.Build())
            .DefineQuoted("CMAKE_EXE_LINKER_FLAGS", ldFlagsStr)
            .DefineQuoted("CMAKE_SHARED_LINKER_FLAGS", ldFlagsStr)
            .DefineQuoted("CMAKE_MODULE_LINKER_FLAGS", ldFlagsStr)
            .CmakeOn("CMAKE_C_COMPILER_WORKS")
            .CmakeOn("CMAKE_CXX_COMPILER_WORKS")
            .Define("CMAKE_C_COMPILER_TARGET", _target.Triple)
            .Define("CMAKE_CXX_COMPILER_TARGET", _target.Triple)
            .Define("CMAKE_ASM_COMPILER_TARGET", _target.Triple)
            .CmakeOn("CMAKE_CROSSCOMPILING")
            .Define("CMAKE_SYSTEM_NAME", "Linux")
            .Define("CMAKE_SYSTEM_PROCESSOR", _target.ArchName)
            .Define("CMAKE_TRY_COMPILE_TARGET_TYPE", "STATIC_LIBRARY")
            .Define("CMAKE_HAVE_LIBC_PTHREAD", "1")
            .Define("CMAKE_THREAD_LIBS_INIT", "-lpthread")
            .Define("CMAKE_HAVE_PTHREADS_CREATE", "")
            .Define("LLVM_TARGETS_TO_BUILD", _target.CmakeArch)
            .Define("LLVM_ENABLE_PROJECTS", "lldb")
            .Define("LLVM_HOST_TRIPLE", "x86_64-linux-gnu")
            .Define("LLVM_DEFAULT_TARGET_TRIPLE", _target.Triple)
            .Define("LLVM_TABLEGEN", _hostTblgen)
            .Define("CLANG_TABLEGEN", _hostClangTblgen)
            .Define("LLDB_TABLEGEN", _hostLldbTblgen)
            .CmakeOff("LLVM_BUILD_LLVM_DYLIB")
            .CmakeOff("LLVM_LINK_LLVM_DYLIB")
            .CmakeOn("LLVM_BUILD_TOOLS")
            .CmakeOn("LLVM_INCLUDE_TOOLS")
            .CmakeOff("LLVM_BUILD_EXAMPLES")
            .CmakeOff("LLVM_INCLUDE_EXAMPLES")
            .CmakeOff("LLVM_BUILD_TESTS")
            .CmakeOff("LLVM_INCLUDE_TESTS")
            .CmakeOff("LLVM_BUILD_BENCHMARKS")
            .CmakeOff("LLVM_INCLUDE_BENCHMARKS")
            .CmakeOff("LLVM_BUILD_DOCS")
            .CmakeOff("LLVM_INCLUDE_DOCS")
            .CmakeOn("LLVM_ENABLE_RTTI")
            .CmakeOn("LLVM_ENABLE_EH")
            .CmakeOn("LLVM_ENABLE_LIBCXX")
            // check_linker_flag("-stdlib=libc++") passes the flag to llvm-ar when
            // CMAKE_TRY_COMPILE_TARGET_TYPE=STATIC_LIBRARY, which llvm-ar rejects.
            // Pre-seed so HandleLLVMStdlib skips the check and adds -stdlib=libc++ directly.
            .CmakeOn("CXX_LINKER_SUPPORTS_STDLIB")
            // Cross-compilation: can't run test binaries on the host. RISC-V with the A
            // extension (included in riscv64-linux-gnu default) generates inline atomics
            // for all sizes, so libatomic is not required.
            .CmakeOn("HAVE_CXX_ATOMICS_WITHOUT_LIB")
            .CmakeOn("HAVE_CXX_ATOMICS64_WITHOUT_LIB")
            .Define("LLVM_ENABLE_ZLIB", !zlib.IsEmpty ? "FORCE_ON" : "OFF")
            .CmakeOff("LLVM_ENABLE_ZSTD")
            .CmakeOff("LLVM_ENABLE_TERMINFO")
            .CmakeOff("LLVM_ENABLE_LIBXML2")
            .CmakeOff("LLDB_BUILD_FRAMEWORK")
            .CmakeOff("LLDB_ENABLE_PYTHON")
            .CmakeOff("LLDB_ENABLE_LUA")
            .CmakeOff("LLDB_ENABLE_LIBEDIT")
            .CmakeOff("LLDB_ENABLE_CURSES")
            .CmakeOff("LLDB_ENABLE_LIBXML2")
            .CmakeOff("LLDB_INCLUDE_TESTS")
            .Define("HAVE_LIBCOMPRESSION", "OFF");

        if (!zlib.IsEmpty)
        {
            args.Define("ZLIB_ROOT:PATH", _target.Sysroot / "usr");
            args.Define("ZLIB_LIBRARY:FILEPATH", zlib);
            args.Define("ZLIB_INCLUDE_DIR:PATH", _target.Sysroot / "usr" / "include");
        }

        return args;
    }
}
