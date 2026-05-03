using System.Text;
using System.Text.RegularExpressions;

namespace Std.BuildTools.Clang;

public class Stage1HostBuilder
{
    private readonly BuildConfiguration _config;
    private readonly FilePath _hostBuildDir;
    private readonly FilePath _bootstrapCc;
    private readonly FilePath _bootstrapCxx;
    private readonly FilePath _bootstrapLd;

    public Stage1HostBuilder(BuildConfiguration config)
    {
        _config = config;
        _hostBuildDir = _config.BuildDir / "host";

        var bootstrapBin = _config.BootstrapClangDir / "bin";
        _bootstrapCc = bootstrapBin / "clang";
        _bootstrapCxx = bootstrapBin / "clang++";
        _bootstrapLd = bootstrapBin / "ld.lld";
    }

    public async Task<bool> Build()
    {
        Log.Info("--- Building Stage 1 Host Toolchain ---");
        Directory.CreateDirectory(_hostBuildDir);

        if (!await CheckPrerequisites())
        {
            return false;
        }

        // Patches and module generation are now handled in ToolchainBuildPrepper.
        if (!await Configure(_config.CmakeModulesDir))
        {
            return false;
        }

        if (!await BuildAndInstall())
        {
            return false;
        }

        var (clangVerExitCode, clangVersion) = await ProcessRunner.GetOutput(
            _config.InstallDir / "bin" / "llvm-config", "--version");
        if (clangVerExitCode != 0)
        {
            Log.Error("ERROR: Failed to get clang version from llvm-config.");
            return false;
        }
        _config.ClangVersion = clangVersion;

        Log.Info(LogColor.Green, "Stage 1 host build complete.");
        return true;
    }

    private async Task<bool> CheckPrerequisites()
    {
        if (!_bootstrapCc.Exists)
        {
            Log.Error($"ERROR: Bootstrap Clang not found at {_bootstrapCc}");
            return false;
        }

        if (!Directory.Exists(_config.HostSysroot / "usr" / "lib") ||
            !(_config.HostSysroot / "usr" / "include" / "stdlib.h").Exists)
        {
            Log.Error($"ERROR: Host sysroot at {_config.HostSysroot} appears to be invalid.");
            return false;
        }

        if (!(_config.HostSysroot / "lib" / "ld-musl-x86_64.so.1").Exists &&
            !(_config.HostSysroot / "usr" / "lib" / "ld-musl-x86_64.so.1").Exists)
        {
            Log.Error("ERROR: Host sysroot is not musl-based. Only musl sysroots are supported.");
            return false;
        }

        return true;
    }

    private async Task<bool> Configure(string cmakeModulesDir)
    {
        var exeLdFlags = new ArgBuilder()
            .UseLd("lld")
            .Dash("static")
            .RtLib("compiler-rt")
            .UnwindLib("none")
            .NoStdLibCxx()
            .Wl("-Bstatic") // Start static linking context
            .Lib("c++")
            .Lib("c++abi")
            .Lib("unwind")
            .Lib("z").Lib("c") // Link libz.a and libc.a
            .Wl("-Bstatic") // Ensure static linking context is maintained
            .Build();

        var soLdFlags = new ArgBuilder()
            .UseLd("lld")
            .RtLib("compiler-rt")
            .UnwindLib("none")
            .NoStdLibCxx()
            .Wl("-z,notext")
            .Build();

        var pythonInfo = _config.Python!;

        var libDir = _config.HostSysroot / "usr" / "lib";
        var incDir = _config.HostSysroot / "usr" / "include";

        var args = new ArgBuilder()
            .NinjaGenerator()
            .CmakeSourceDir(_config.SourceDir / "llvm")
            .CmakeBinaryDir(_hostBuildDir)
            .CmakeBuildRelease()
            .Define("CMAKE_INSTALL_PREFIX", _config.InstallDir)
            .Define("CMAKE_SYSTEM_NAME", "Linux")
            .CmakeOn("CMAKE_BUILD_WITH_INSTALL_RPATH")
            .Define("LLVM_ENABLE_PROJECTS", "clang;clang-tools-extra;lld;lldb")
            .Define("LLVM_ENABLE_RUNTIMES", "")
            .Define("LLVM_HOST_TRIPLE", "x86_64-linux-musl")
            .Define("LLVM_DEFAULT_TARGET_TRIPLE", "x86_64-linux-gnu")
            .Define("LLVM_TARGETS_TO_BUILD", "X86;ARM;AArch64;RISCV")
            .DefineQuoted("CMAKE_C_COMPILER", _bootstrapCc)
            .DefineQuoted("CMAKE_CXX_COMPILER", _bootstrapCxx)
            .DefineQuoted("CMAKE_LINKER", _bootstrapLd)
            .DefineQuoted("CMAKE_C_FLAGS", "-fdiagnostics-color=always -fPIC")
            .DefineQuoted("CMAKE_CXX_FLAGS", "-fdiagnostics-color=always -fPIC -stdlib=libc++")
            .Define("CMAKE_C_STANDARD", "17")
            .Define("CMAKE_CXX_STANDARD", "17")
            .DefineQuoted("CMAKE_EXE_LINKER_FLAGS", exeLdFlags)
            .DefineQuoted("CMAKE_SHARED_LINKER_FLAGS", soLdFlags)
            .DefineQuoted("CMAKE_MODULE_LINKER_FLAGS", soLdFlags)
            .Define("CMAKE_SYSROOT", _config.HostSysroot)
            .Define("Python3_FIND_STRATEGY", "LOCATION")
            .Define("Python3_INCLUDE_DIR", pythonInfo.IncludeDir)
            .Define("Python3_LIBRARY", pythonInfo.LibraryPath)
            .Define("Python3_EXECUTABLE", "python3") // A reasonable default for the build host
            .CmakeOn("LLVM_LIBSTDCXX_MIN")
            .CmakeOn("LLVM_LIBSTDCXX_SOFT_ERROR")
            .CmakeOn("LLVM_TEMPORARILY_ALLOW_OLD_TOOLCHAIN")
            .Define("ZLIB_INCLUDE_DIR", _config.HostSysroot / "usr" / "include")
            .Define("ZLIB_LIBRARY", _config.HostSysroot / "usr" / "lib" / "libz.a")
            .Define("CMAKE_MODULE_PATH", cmakeModulesDir)
            .Define("LIBXML2_LIBRARY", libDir / "libxml2.a")
            .Define("LibXml2_LIBRARY", libDir / "libxml2.a")
            .Define("LLVM_ENABLE_LIBXML2", "FORCE_ON")
            .Define("LibEdit_INCLUDE_DIRS", incDir)
            .Define("LibEdit_LIBRARIES", $"{libDir / "libedit.a"};{libDir / "libncurses.a"}")
            .Define("LibXml2_INCLUDE_DIR", incDir / "libxml2")
            .Define("CURSES_INCLUDE_DIRS", incDir)
            .Define("CURSES_INCLUDE_PATH", incDir)
            .Define("CURSES_LIBRARY", libDir / "libncurses.a")
            .Define("CURSES_NCURSES_LIBRARY", libDir / "libncurses.a")
            .Define("CURSES_LIBRARIES", libDir / "libncurses.a")
            .Define("PANEL_LIBRARIES", libDir / "libpanel.a")
            .Define("CURSES_FORM_LIBRARY", libDir / "libform.a")
            .Define("LIBLZMA_LIBRARY", libDir / "liblzma.a")
            .CmakeOn("LLVM_ENABLE_LLD")
            .Define("CMAKE_TRY_COMPILE_TARGET_TYPE", "STATIC_LIBRARY")
            .CmakeOn("HAVE_CXX_ATOMICS_WITHOUT_LIB")
            .CmakeOn("HAVE_CXX_ATOMICS64_WITHOUT_LIB")
            .CmakeOn("CXX_SUPPORTS_FUNWIND_TABLES_FLAG")
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
            .CmakeOff("LLVM_BUILD_LLVM_DYLIB")
            .CmakeOff("LLVM_LINK_LLVM_DYLIB")
            .CmakeOff("CLANG_LINK_CLANG_DYLIB")
            // Skip installing static archives, dev headers, and cmake configs.
            // This toolchain ships clang/lld/lldb for end-user compilation, not
            // for building tooling that links against LLVM/Clang as a library.
            .CmakeOn("LLVM_INSTALL_TOOLCHAIN_ONLY")
            .CmakeOff("LLVM_TOOL_LTO_BUILD")
            .CmakeOff("CLANG_TOOL_LIBCLANG_BUILD")
            .CmakeOff("CLANG_TOOL_CLANG_SHLIB_BUILD")
            .CmakeOn("CLANG_ENABLE_STATIC_ANALYZER")
            .CmakeOn("CLANG_ENABLE_ARCMT")
            .CmakeOn("LLVM_ENABLE_RTTI")
            .CmakeOn("LLVM_ENABLE_EH")
            .CmakeOff("LLVM_ENABLE_ASSERTIONS")
            .CmakeOn("LLDB_ENABLE_PYTHON")
            .Define("LLDB_PYTHON_RELATIVE_PATH", $"lib/python{pythonInfo.Version}/site-packages")
            .Define("LLDB_PYTHON_EXE_RELATIVE_PATH", "bin/python3")
            .Define("LLDB_PYTHON_EXT_SUFFIX", $".cpython-{pythonInfo.Version.Replace(".", "")}-x86_64-linux-musl.so")
            .CmakeOff("LLDB_ENABLE_LUA")
            .CmakeOn("LLDB_ENABLE_LIBEDIT")
            .CmakeOn("LLDB_ENABLE_CURSES")
            .CmakeOn("LLDB_ENABLE_LIBXML2")
            .CmakeOff("LLDB_ENABLE_FBSDVMCORE")
            .CmakeOff("LLDB_USE_SYSTEM_DEBUGSERVER")
            .CmakeOff("HAVE_LIBCOMPRESSION")
            .CmakeOn("LLVM_ENABLE_ZLIB")
            .Define("LLVM_ENABLE_ZSTD", "FORCE_ON")
            .Define("zstd_LIBRARY", libDir / "libzstd.a")
            .Define("zstd_INCLUDE_DIR", incDir)
            .CmakeOff("LLDB_BUILD_FRAMEWORK")
            .CmakeOff("LLDB_INCLUDE_TESTS")
            .CmakeOff("LLDB_BUILD_SHARED_LIB")
            .CmakeOff("LLDB_TOOL_LLDB_SERVER_BUILD");

        return await Configurator.Configure(_hostBuildDir, _config.ForceReconfigure, args.Build(), "host Clang/LLD/LLDB");
    }

    private async Task<bool> BuildAndInstall()
    {
        Log.Info($"Building host Clang/LLD/LLDB ({_config.Jobs} jobs)...");
        var buildArgs = new ArgBuilder()
            .DashDash("build", _hostBuildDir, Quoted.Yes)
            .DashDash("parallel", _config.Jobs.ToString());

        if (await ProcessRunner.Run("cmake", buildArgs.Build()) != 0)
        {
            Log.Warning("WARNING: build finished with errors -- continuing to install");
        }

        Log.Info($"Installing to {_config.InstallDir}...");
        var installArgs = new ArgBuilder()
            .DashDash("install", _hostBuildDir, Quoted.Yes);
        if (await ProcessRunner.Run("cmake", installArgs.Build()) != 0)
        {
            Log.Error("ERROR: Install step failed.");
            return false;
        }

        return true;
    }
 }
