using System.IO;
using System.Text.RegularExpressions;

namespace Std.BuildTools.Clang;

public abstract class ToolchainPackager
{
    protected void CreateConvenienceSymlinks(string rootInstallDir)
    {
        Log.Info("Creating convenience symlinks for compilers...");
        var binDir = Path.Combine(rootInstallDir, "bin");

        // Ensure binDir exists before trying to create symlinks
        if (!Directory.Exists(binDir))
        {
            Log.Warning($"WARNING: Bin directory not found at '{binDir}'. Skipping symlink creation.");
            return;
        }

        var triples = new[]
        {
            "x86_64-linux-gnu", "arm-linux-gnueabihf", "aarch64-linux-gnu", "riscv64-linux-gnu", "i686-linux-gnu"
        };
        var tools = new[] { "clang", "clang++", "clang-cpp" };

        foreach (var triple in triples)
        {
            foreach (var tool in tools)
            {
                var targetPath = Path.Combine(binDir, tool);
                var linkPath = Path.Combine(binDir, $"{triple}-{tool}");

                // Only create symlink if the target exists
                if (!File.Exists(targetPath))
                {
                    continue;
                }

                try
                {
                    if (File.Exists(linkPath) ||
                        Directory.Exists(linkPath))
                    {
                        File.Delete(linkPath);
                    }
                    // Relative target — both link and target live in bin/, so the symlink
                    // resolves correctly regardless of where the toolchain is extracted.
                    File.CreateSymbolicLink(linkPath, tool);
                }
                catch (Exception e) { Log.Warning($"Warning: Failed to create symlink '{linkPath}': {e.Message}"); }
            }
        }
        Log.Info("Created cross-compiler symlinks.");
    }

    protected bool PatchLlvmConfig(string installDir, string toolchainName)
    {
        Log.Info($"Patching {toolchainName} LLVMConfig.cmake...");
        var llvmConfigPath = Path.Combine(installDir, "lib", "cmake", "llvm", "LLVMConfig.cmake");

        if (!File.Exists(llvmConfigPath))
        {
            Log.Warning($"WARNING: {toolchainName} LLVMConfig.cmake not found at '{llvmConfigPath}'. Skipping patch.");
            return true;
        }

        var content = File.ReadAllText(llvmConfigPath);

        // 1. Remove the ZLIB_ROOT clear so cross builds can set it.
        var result = content.Patch("  set(ZLIB_ROOT )", "");
        if (!result)
        {
            Log.Error($"ERROR: Failed to patch ZLIB_ROOT in {toolchainName} LLVMConfig.cmake.");
            return false;
        }
        content = (string)result;

        // 2. Guard find_package(ZLIB) so it skips when ZLIB_FOUND is pre-set.
        result = content.Patch("  find_package(ZLIB)", "  if(NOT ZLIB_FOUND)\n    find_package(ZLIB)\n  endif()");
        if (!result)
        {
            Log.Error($"ERROR: Failed to patch find_package(ZLIB) in {toolchainName} LLVMConfig.cmake.");
            return false;
        }
        content = (string)result;

        // 3. Guard LLVM_ENABLE_ZLIB so it can be overridden from the command line.
        result = content.Patch("set(LLVM_ENABLE_ZLIB 1)", "if(NOT DEFINED LLVM_ENABLE_ZLIB)\n  set(LLVM_ENABLE_ZLIB 1)\nendif()");
        if (!result)
        {
            Log.Error($"ERROR: Failed to patch LLVM_ENABLE_ZLIB in {toolchainName} LLVMConfig.cmake.");
            return false;
        }
        content = (string)result;

        // 4. Fix hardcoded build-tree path for llvm-lit. This requires a regex replacement.
        var newContent = Regex.Replace(content, @"^\s*set\(LLVM_DEFAULT_EXTERNAL_LIT .*\)", "  set(LLVM_DEFAULT_EXTERNAL_LIT \"${CMAKE_CURRENT_LIST_DIR}/../../../bin/llvm-lit\")", RegexOptions.Multiline);
        if (content == newContent)
        {
            Log.Error($"ERROR: Failed to patch LLVM_DEFAULT_EXTERNAL_LIT in {toolchainName} LLVMConfig.cmake.");
            return false;
        }
        content = newContent;

        File.WriteAllText(llvmConfigPath, content);
        Log.Info($"  Patched: {llvmConfigPath}");
        return true;
    }
}
