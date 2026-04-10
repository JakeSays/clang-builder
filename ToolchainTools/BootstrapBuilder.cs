namespace Std.BuildTools.Clang;

public class BootstrapBuilder
{
    private readonly string _llvmVersion;
    private readonly string _llvmSrcDir;
    private readonly string _buildDir;
    private readonly string _installDir;
    private readonly string _hostSysroot;
    private readonly int _jobs;
    private readonly bool _forceReconfigure;

    public BootstrapBuilder(
        string llvmVersion,
        string llvmSrcDir,
        string buildDir,
        string installDir,
        string hostSysroot,
        int jobs,
        bool forceReconfigure)
    {
        _llvmVersion = llvmVersion;
        _llvmSrcDir = llvmSrcDir;
        _buildDir = buildDir;
        _installDir = installDir;
        _hostSysroot = hostSysroot;
        _jobs = jobs;
        _forceReconfigure = forceReconfigure;
    }

    public async Task<bool> Build()
    {
        var hostBuildDir = Path.Combine(_buildDir, "host");
        Directory.CreateDirectory(hostBuildDir);

        // Find bootstrap compiler on host
        var bootstrapCc = FileUtils.FindExecutable("clang-20", "clang", "gcc");
        var bootstrapCxx = FileUtils.FindExecutable("clang++-20", "clang++", "g++");
        var bootstrapLd = FileUtils.FindExecutable("lld-20", "ld.lld", "ld");

        if (bootstrapCc == null || bootstrapCxx == null || bootstrapLd == null)
        {
            Log.Error("ERROR: Could not find a suitable bootstrap compiler (clang or gcc) on the PATH.");
            return false;
        }

        Log.Info(LogColor.Green, $"Using bootstrap C compiler: {bootstrapCc}");
        Log.Info(LogColor.Green, $"Using bootstrap C++ compiler: {bootstrapCxx}");

        // Configure
        var cmakeArgs = GetCmakeArgs(hostBuildDir, bootstrapCc, bootstrapCxx, bootstrapLd);
        if (!await Configurator.Configure(hostBuildDir, _forceReconfigure, cmakeArgs.Build(), "bootstrap Clang"))
        {
            return false;
        }

        // Build
        var buildArgs = new ArgBuilder()
            .DashDash("build", hostBuildDir, Quoted.Yes)
            .DashDash("parallel", _jobs.ToString());
        var buildExitCode = await ProcessRunner.Run("cmake", buildArgs.Build());
        // The script continues on build errors, so we will too.
        if (buildExitCode != 0)
        {
            Log.Warning($"WARNING: Build finished with exit code {buildExitCode}. Continuing to install.");
        }

        // Install
        var installArgs = new ArgBuilder().DashDash("install", hostBuildDir, Quoted.Yes);
        var installExitCode = await ProcessRunner.Run("cmake", installArgs.Build());
        if (installExitCode != 0)
        {
            return false;
        }

        // Post-install steps from script
        PatchLlvmConfig();
        CreateSymlinks();

        return true;
    }

    private ArgBuilder GetCmakeArgs(
        string hostBuildDir,
        string cc,
        string cxx,
        string ld)
    {
        const string hostTriple = "x86_64-linux-musl";
        var flagsBuilder = new ArgBuilder();

        // CMAKE_C_FLAGS
        flagsBuilder.Target(hostTriple).Sysroot(_hostSysroot).ColorAlways().Pic();
        var cFlags = flagsBuilder.Build();

        // CMAKE_CXX_FLAGS
        flagsBuilder.Target(hostTriple).Sysroot(_hostSysroot).ColorAlways().Pic()
            .StdLib("libc++");
        var cxxFlags = flagsBuilder.Build();

        // CMAKE_EXE_LINKER_FLAGS, CMAKE_SHARED_LINKER_FLAGS,
        // CMAKE_MODULE_LINKER_FLAGS
        var ldFlags = new ArgBuilder()
            .Dash("static") // This is the crucial flag to force a static link.
            .UseLd("lld")
            .Target(hostTriple)
            .Sysroot(_hostSysroot)
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

        var runtimesCmakeArgs = new ArgBuilder()
            .CmakeBuildRelease()
            .Define("CMAKE_C_FLAGS", "-fdiagnostics-color=always")
            .Define("CMAKE_CXX_FLAGS", "-fdiagnostics-color=always")
            .Define("Python3_EXECUTABLE", FileUtils.FindExecutable("python3") ?? "python3")
            .CmakeOn("COMPILER_RT_BUILD_BUILTINS")
            .CmakeOff("COMPILER_RT_BUILD_SANITIZERS")
            .CmakeOff("COMPILER_RT_BUILD_XRAY")
            .CmakeOff("COMPILER_RT_BUILD_LIBFUZZER")
            .CmakeOff("COMPILER_RT_BUILD_PROFILE")
            .CmakeOff("COMPILER_RT_BUILD_MEMPROF")
            .CmakeOff("COMPILER_RT_BUILD_ORC")
            .CmakeOn("COMPILER_RT_DEFAULT_TARGET_ONLY")
            .CmakeOn("TEST_COMPILE_ONLY")
            .Build(';');

        return new ArgBuilder()
            .NinjaGenerator()
            .CmakeSourceDir(Path.Combine(_llvmSrcDir, "llvm")) // -S
            .CmakeBinaryDir(hostBuildDir) // -B
            .CmakeBuildRelease() // -DCMAKE_BUILD_TYPE=Release
            .Define("CMAKE_INSTALL_PREFIX", _installDir)
            .Define("LLVM_ENABLE_PROJECTS", "clang;lld")
            .Define("LLVM_ENABLE_RUNTIMES", "compiler-rt")
            .CmakeOn("COMPILER_RT_BUILD_BUILTINS")
            .CmakeOff("COMPILER_RT_BUILD_SANITIZERS")
            .CmakeOff("COMPILER_RT_BUILD_XRAY")
            .CmakeOff("COMPILER_RT_BUILD_LIBFUZZER")
            .CmakeOff("COMPILER_RT_BUILD_PROFILE")
            .CmakeOff("COMPILER_RT_BUILD_MEMPROF")
            .CmakeOff("COMPILER_RT_BUILD_ORC")
            .CmakeOn("COMPILER_RT_DEFAULT_TARGET_ONLY")
            .CmakeOn("TEST_COMPILE_ONLY")
            .Define("RUNTIMES_CMAKE_ARGS", runtimesCmakeArgs)
            .Define("LLVM_TARGETS_TO_BUILD", "X86")
            .Define("LLVM_HOST_TRIPLE", hostTriple)
            .Define("LLVM_DEFAULT_TARGET_TRIPLE", hostTriple)
            .DefineQuoted("CMAKE_C_COMPILER", cc)
            .DefineQuoted("CMAKE_CXX_COMPILER", cxx)
            .DefineQuoted("CMAKE_LINKER", ld)
            .DefineQuoted("CMAKE_SYSROOT", _hostSysroot)
            .DefineQuoted("CMAKE_C_FLAGS", cFlags)
            .DefineQuoted("CMAKE_CXX_FLAGS", cxxFlags)
            .DefineQuoted("CMAKE_EXE_LINKER_FLAGS", ldFlags)
            .DefineQuoted("CMAKE_SHARED_LINKER_FLAGS", ldFlags)
            .DefineQuoted("CMAKE_MODULE_LINKER_FLAGS", ldFlags)
            .CmakeOn("CMAKE_SKIP_INSTALL_RPATH")
            .CmakeOn("CMAKE_SKIP_RPATH")
            .Define("CMAKE_CXX_STANDARD", "17")
            .CmakeOn("LLVM_BUILD_TOOLS")
            .CmakeOn("LLVM_INCLUDE_TOOLS")
            .CmakeOn("CLANG_BUILD_TOOLS")
            .CmakeOff("CLANG_BUILD_EXAMPLES")
            .CmakeOff("CLANG_PLUGIN_SUPPORT")
            .CmakeOff("CLANG_ENABLE_STATIC_ANALYZER")
            .CmakeOff("CLANG_ENABLE_ARCMT")
            .CmakeOff("CLANG_BUILD_SHARED_LIBS")
            .CmakeOff("CLANG_TOOL_LIBCLANG_BUILD")
            .CmakeOff("CLANG_TOOL_CLANG_CHECK_BUILD")
            .CmakeOff("CLANG_TOOL_CLANG_DIFF_BUILD")
            .CmakeOff("CLANG_TOOL_CLANG_EXTDEF_MAPPING_BUILD")
            .CmakeOff("CLANG_TOOL_CLANG_FORMAT_BUILD")
            .CmakeOff("CLANG_TOOL_CLANG_FUZZER_BUILD")
            .CmakeOff("CLANG_TOOL_CLANG_IMPORT_TEST_BUILD")
            .CmakeOff("CLANG_TOOL_CLANG_INSTALLAPI_BUILD")
            .CmakeOff("CLANG_TOOL_CLANG_LINKER_WRAPPER_BUILD")
            .CmakeOff("CLANG_TOOL_CLANG_NVLINK_WRAPPER_BUILD")
            .CmakeOff("CLANG_TOOL_CLANG_OFFLOAD_BUNDLER_BUILD")
            .CmakeOff("CLANG_TOOL_CLANG_OFFLOAD_PACKAGER_BUILD")
            .CmakeOff("CLANG_TOOL_CLANG_REFACTOR_BUILD")
            .CmakeOff("CLANG_TOOL_CLANG_RENAME_BUILD")
            .CmakeOff("CLANG_TOOL_CLANG_REPL_BUILD")
            .CmakeOff("CLANG_TOOL_CLANG_SCAN_DEPS_BUILD")
            .CmakeOff("CLANG_TOOL_CLANG_SHLIB_BUILD")
            .CmakeOff("CLANG_TOOL_CLANG_SYCL_LINKER_BUILD")
            .CmakeOff("CLANG_TOOL_APINOTES_TEST_BUILD")
            .CmakeOff("CLANG_TOOL_CIR_LSP_SERVER_BUILD")
            .CmakeOff("CLANG_TOOL_CIR_OPT_BUILD")
            .CmakeOff("CLANG_TOOL_CIR_TRANSLATE_BUILD")
            .CmakeOff("CLANG_TOOL_C_INDEX_TEST_BUILD")
            .CmakeOff("CLANG_TOOL_DIAGTOOL_BUILD")
            .CmakeOff("CLANG_TOOL_OFFLOAD_ARCH_BUILD")
            .CmakeOff("CLANG_TOOL_SCAN_BUILD_BUILD")
            .CmakeOff("CLANG_TOOL_SCAN_BUILD_PY_BUILD")
            .CmakeOff("CLANG_TOOL_SCAN_VIEW_BUILD")
            .CmakeOff("LLVM_TOOL_BUGPOINT_PASSES_BUILD")
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
            .CmakeOff("LLVM_ENABLE_LTO")
            .CmakeOff("LLVM_BUILD_LLVM_C_DYLIB")
            .CmakeOff("LLVM_TOOL_LTO_BUILD")
            .CmakeOff("LLVM_TOOL_LLVM_LTO_BUILD")
            .CmakeOff("LLVM_TOOL_LLVM_LTO2_BUILD")
            .CmakeOff("CLANG_BUILD_SHARED_LIBS")
            .CmakeOff("CLANG_TOOL_LIBCLANG_BUILD")
            .CmakeOn("LLVM_ENABLE_RTTI")
            .CmakeOn("LLVM_ENABLE_EH")
            .CmakeOff("LLVM_ENABLE_ASSERTIONS")
            .DefineQuoted("Python3_EXECUTABLE", FileUtils.FindExecutable("python3") ?? "python3")
            .Define("Python3_FIND_STRATEGY", "LOCATION")
            .Define("ZLIB_LIBRARY", Path.Combine(_hostSysroot, "usr", "lib", "libz.a"))
            .Define("ZLIB_INCLUDE_DIR", Path.Combine(_hostSysroot, "usr", "include"))
            .CmakeOff("LLVM_ENABLE_ZSTD")
            .CmakeOff("LLVM_ENABLE_LIBXML2")
            .CmakeOff("LLVM_ENABLE_TERMINFO")
            .CmakeOn("LLVM_LIBSTDCXX_MIN")
            .CmakeOn("LLVM_LIBSTDCXX_SOFT_ERROR")
            .CmakeOn("LLVM_TEMPORARILY_ALLOW_OLD_TOOLCHAIN");
    }

    private void PatchLlvmConfig()
    {
        var llvmConfigPath = Path.Combine(_installDir, "lib", "cmake", "llvm", "LLVMConfig.cmake");
        if (!File.Exists(llvmConfigPath))
        {
            return;
        }

        var content = File.ReadAllText(llvmConfigPath);
        content = content.Replace("  set(ZLIB_ROOT )", "");
        content = content.Replace("  find_package(ZLIB)",
            "  if(NOT ZLIB_FOUND)\n    find_package(ZLIB)\n  endif()");
        content = content.Replace("set(LLVM_ENABLE_ZLIB 1)",
            "if(NOT DEFINED LLVM_ENABLE_ZLIB)\n  set(LLVM_ENABLE_ZLIB 1)\nendif()");
        File.WriteAllText(llvmConfigPath, content);
        Log.Info(LogColor.Green, "Patched LLVMConfig.cmake");
    }

    private void CreateSymlinks()
    {
        var binDir = Path.Combine(_installDir, "bin");
        foreach (var tool in new[] { "clang", "clang++", "clang-cpp" })
        {
            var target = Path.Combine(binDir, tool);
            if (File.Exists(target))
            {
                var link = Path.Combine(binDir, $"x86_64-linux-gnu-{tool}");
                if (File.Exists(link))
                {
                    File.Delete(link);
                }
                File.CreateSymbolicLink(link, target);
            }
        }
    }
}
