namespace Swyft.ServoProgrammer.Abstractions;

/// <summary>
/// Represents information about an available serial port on the system.
/// </summary>
/// <param name="Name">The port name (e.g., "COM3" on Windows, "/dev/ttyUSB0" on Linux).</param>
/// <param name="Description">Human-friendly description of the device (e.g., "Silicon Labs CP210x ... (COM3)").</param>
public sealed record SerialPortInfo(string Name, string Description);

/// <summary>
/// Platform-specific provider for serial port discovery and access.
/// </summary>
public interface ISerialPortProvider
{
    /// <summary>
    /// Returns the names of all available serial ports on the system.
    /// </summary>
    string[] GetPortNames();

    /// <summary>
    /// Returns detailed information about available serial ports, including device descriptions.
    /// </summary>
    IReadOnlyList<SerialPortInfo> DescribePorts();
}
