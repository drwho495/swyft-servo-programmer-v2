using System.IO.Ports;
using Swyft.ServoProgrammer.Abstractions;

namespace Swyft.ServoProgrammer.Services;

/// <summary>
/// Linux-specific implementation using /sys/class/tty and udev-like discovery for device descriptions.
/// </summary>
public sealed class LinuxSerialPortProvider : ISerialPortProvider
{
    public string[] GetPortNames() => SerialPort.GetPortNames();

    public IReadOnlyList<SerialPortInfo> DescribePorts()
    {
        var ports = SerialPort.GetPortNames();
        var result = new List<SerialPortInfo>();

        foreach (var port in ports)
        {
            var description = GetLinuxPortDescription(port);
            result.Add(new SerialPortInfo(port, description));
        }

        return result;
    }

    private static string GetLinuxPortDescription(string portName)
    {
        try
        {
            // Try to read device info from sysfs
            var ttyName = new FileInfo(portName).Name; // e.g., "ttyUSB0" from "/dev/ttyUSB0"
            var deviceLink = $"/sys/class/tty/{ttyName}/device";

            if (Directory.Exists(deviceLink))
            {
                // Try to read the product string
                var devProduct = Path.Combine(deviceLink, "product");
                if (File.Exists(devProduct))
                {
                    var product = File.ReadAllText(devProduct).Trim();
                    if (!string.IsNullOrEmpty(product))
                        return $"{product} ({portName})";
                }

                // Try to read idVendor:idProduct from the device
                var idPath = Path.Combine(deviceLink, "idVendor");
                var idProdPath = Path.Combine(deviceLink, "idProduct");
                if (File.Exists(idPath) && File.Exists(idProdPath))
                {
                    var vendor = File.ReadAllText(idPath).Trim();
                    var product = File.ReadAllText(idProdPath).Trim();
                    return $"USB Device {vendor}:{product} ({portName})";
                }

                // Check if it's a USB device by looking at the parent
                var usbInterface = Directory.GetParent(deviceLink)?.FullName;
                if (usbInterface != null && usbInterface.Contains("usb"))
                {
                    return $"USB Serial Device ({portName})";
                }
            }

            // Fallback: try to determine type from the port name
            if (portName.Contains("ttyUSB"))
                return $"USB Serial ({portName})";
            if (portName.Contains("ttyACM"))
                return $"USB ACM Device ({portName})";
            if (portName.Contains("ttyS"))
                return $"Serial Port ({portName})";
            if (portName.Contains("ttyAMA"))
                return $"ARM UART ({portName})";

            return portName;
        }
        catch
        {
            return portName;
        }
    }
}
