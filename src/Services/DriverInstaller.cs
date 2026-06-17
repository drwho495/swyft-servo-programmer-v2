using System.IO;

namespace Swyft.ServoProgrammer.Services;

/// <summary>
/// Locates the bundled Silicon Labs CP210x USB-to-UART driver files (.inf + .cat + .sys) so the user
/// can install them manually (right-click the .inf and choose Install, or run the included installer).
/// Note: Driver installation is Windows-only. On Linux, the CP210x driver is built into the kernel (usbserial + cp210x modules).
/// </summary>
public static class DriverInstaller
{
    /// <summary>Whether driver installation is applicable on the current platform (Windows only).</summary>
    public static bool IsDriverInstallationRequired => OperatingSystem.IsWindows();

    /// <summary>Folder containing the bundled CP210x driver, next to the executable.</summary>
    public static string DriverFolder =>
        Path.Combine(AppContext.BaseDirectory, "Drivers", "CP210x");

    /// <summary>Path to the bundled driver INF.</summary>
    public static string InfPath => Path.Combine(DriverFolder, "silabser.inf");

    public static bool DriverFilesPresent => File.Exists(InfPath);
}
