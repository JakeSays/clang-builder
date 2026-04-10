namespace Std.BuildTools.Clang;

public abstract class BuildPrepper
{
    protected readonly FileDownloader? Downloader;
    protected readonly FilePath WorkDir;
    protected readonly FilePath PrebuiltsSourceDir;
    protected readonly FilePath PrebuiltsOutputDir;
    protected readonly bool KeepWorkDir;


    protected BuildPrepper(
        FileDownloader? downloader,
        FilePath workDir,
        FilePath prebuiltsSourceDir,
        FilePath prebuiltsOutputDir,
        bool keepWorkDir)
    {
        Downloader = downloader;
        WorkDir = workDir;
        PrebuiltsSourceDir = prebuiltsSourceDir;
        PrebuiltsOutputDir = prebuiltsOutputDir;
        KeepWorkDir = keepWorkDir;
    }

    protected bool CheckHostDependencies()
    {
        Log.Info("Checking host dependencies...");
        var missingTools = new List<string>();
        var requiredTools = new[] { "cmake", "ninja", "git", "python3", "patchelf" };

        foreach (var tool in requiredTools)
        {
            if (FileUtils.FindExecutable(tool) == null)
            {
                missingTools.Add(tool);
            }
        }

        if (missingTools.Any())
        {
            Log.Error("ERROR: Missing required host dependencies:");
            foreach (var tool in missingTools)
            {
                Log.Info($"  - {tool}");
            }
            Log.Info("Please install them using your system's package manager (e.g., 'sudo apt-get install cmake ninja-build git python3 patchelf').");
            return false;
        }

        Log.Info(LogColor.Green, "All host dependencies found.");
        return true;
    }

    protected async Task<bool> FetchLlvmSource(string llvmVersion, FilePath srcDir)
    {
        Log.Info($"--- Fetching LLVM {llvmVersion} source ---");

        if (StampFile.IsPresent(srcDir, "downloaded"))
        {
            Log.Info($"Source already present at {srcDir} — skipping download.");
            return true;
        }

        FileUtils.DeleteDirectory(srcDir);
        Directory.CreateDirectory(srcDir);

        Log.Info($"Downloading LLVM {llvmVersion} source...");
        var tarballUrl = $"https://github.com/llvm/llvm-project/releases/download/llvmorg-{llvmVersion}/llvm-project-{llvmVersion}.src.tar.xz";
        var tarballPath = WorkDir / $"llvm-project-{llvmVersion}.src.tar.xz";

        await Downloader!.DownloadFile(tarballUrl, tarballPath, "LLVM Source");
        if (!await FileUtils.ExtractArchive(tarballPath, srcDir, "LLVM Source"))
        {
            return false;
        }

        if (!KeepWorkDir)
        {
            File.Delete(tarballPath);
        }

        StampFile.Create(srcDir, "downloaded");
        Log.Info($"Source ready at {srcDir}");

        return true;
    }
}
