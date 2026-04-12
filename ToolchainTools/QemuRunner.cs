namespace Std.BuildTools.Clang;

public static class QemuRunner
{
    private static readonly Dictionary<TargetArch, string> QemuBinaries = new();

    static QemuRunner()
    {
        // Pre-discover QEMU binaries on the system when the application starts.
        CheckQemuBinary(TargetArch.Armv7, "qemu-armhf");
        CheckQemuBinary(TargetArch.Aarch64, "qemu-aarch64");
        CheckQemuBinary(TargetArch.Riscv64, "qemu-riscv64");
        CheckQemuBinary(TargetArch.X86, "qemu-i386");
    }

    private static void CheckQemuBinary(TargetArch arch, string binaryName)
    {
        if (FileUtils.ExistsOnPath(binaryName))
        {
            QemuBinaries[arch] = binaryName;
        }
    }

    /// <summary>
    /// Checks if a QEMU user-mode emulator is available for the given architecture.
    /// </summary>
    public static bool IsSupported(TargetArch arch) => QemuBinaries.ContainsKey(arch);

    /// <summary>
    /// Runs an executable under the appropriate QEMU user-mode emulator.
    /// </summary>
    public static async Task<(int, string)> Run(TargetArch arch, string executablePath, string sysroot, string workingDir, string extraArgs = "")
    {
        if (!IsSupported(arch))
        {
            return (-1, $"QEMU for architecture '{arch}' is not installed or not found on PATH.");
        }

        var qemuBinary = QemuBinaries[arch];
        var qemuArgs = new ArgBuilder()
            .Dash("L", sysroot.Quoted()) // Point QEMU to the sysroot for dynamic libraries
            .Text(executablePath, Quoted.Yes)
            .TextIf(!string.IsNullOrEmpty(extraArgs), extraArgs)
            .Build();

        return await ProcessRunner.GetOutput(qemuBinary, qemuArgs, workingDir);
    }
}
