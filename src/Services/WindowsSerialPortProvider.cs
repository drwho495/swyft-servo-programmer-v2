#if WINDOWS
using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;
using Swyft.ServoProgrammer.Abstractions;

namespace Swyft.ServoProgrammer.Services;

/// <summary>
/// Windows-specific implementation using WMI for device descriptions.
/// </summary>
public sealed class WindowsSerialPortProvider : ISerialPortProvider
{
    public string[] GetPortNames() => SerialPort.GetPortNames();

    public IReadOnlyList<SerialPortInfo> DescribePorts()
    {
        var available = new HashSet<string>(SerialPort.GetPortNames(), StringComparer.OrdinalIgnoreCase);
        var result = new List<SerialPortInfo>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%(COM%)'");
            foreach (var device in searcher.Get())
            {
                var name = device["Name"]?.ToString();
                if (string.IsNullOrWhiteSpace(name)) continue;

                var match = Regex.Match(name, @"\((COM\d+)\)");
                if (!match.Success) continue;

                var port = match.Groups[1].Value;
                available.Remove(port);
                result.Add(new SerialPortInfo(port, name));
            }
        }
        catch
        {
            // WMI can be unavailable or slow on some systems; fall back to bare port names below.
        }

        // Include any ports WMI didn't describe so nothing is hidden from the user.
        foreach (var port in available)
            result.Add(new SerialPortInfo(port, port));

        return result;
    }
}

#endif
