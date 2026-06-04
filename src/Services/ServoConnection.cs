using System.Diagnostics;
using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;
using Swyft.ServoProgrammer.Models;
using Swyft.ServoProgrammer.Protocol;

namespace Swyft.ServoProgrammer.Services;

/// <summary>A COM port together with its Windows friendly name (e.g. "Silicon Labs CP210x ... (COM3)").</summary>
public sealed record SerialPortInfo(string Name, string Description);

/// <summary>
/// Thin wrapper around <see cref="SerialPort"/> implementing the request/response
/// transaction used by the servo over a single-wire half-duplex UART link.
/// </summary>
public sealed class ServoConnection : IDisposable
{
    private readonly object _gate = new();
    private SerialPort? _port;

    public bool IsOpen
    {
        get { lock (_gate) { return _port is { IsOpen: true }; } }
    }

    public string? PortName
    {
        get { lock (_gate) { return _port?.PortName; } }
    }

    public static string[] GetPortNames() => SerialPort.GetPortNames();

    /// <summary>
    /// Returns the available COM ports with their Windows friendly names, queried via WMI. Unlike
    /// <see cref="GetPortNames"/>, this lets us recognise USB-to-UART adapters by name without having
    /// to open each port (opening/closing arbitrary ports is unreliable with System.IO.Ports).
    /// </summary>
    public static IReadOnlyList<SerialPortInfo> DescribePorts()
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

    public void Open(string portName, SerialSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);
        ArgumentNullException.ThrowIfNull(settings);

        lock (_gate)
        {
            Close_NoLock();

            var port = new SerialPort(portName, settings.BaudRate, settings.Parity, settings.DataBits, settings.StopBits)
            {
                Handshake = Handshake.None,
                ReadTimeout = settings.ResponseTimeoutMs,
                WriteTimeout = 1000
            };
            port.Open();
            port.DiscardInBuffer();
            port.DiscardOutBuffer();
            _port = port;
        }
    }

    public void Close()
    {
        lock (_gate) { Close_NoLock(); }
    }

    private void Close_NoLock()
    {
        if (_port is null) return;
        try
        {
            if (_port.IsOpen) _port.Close();
        }
        catch
        {
            // Ignore errors while closing a possibly-unplugged adapter.
        }
        finally
        {
            _port.Dispose();
            _port = null;
        }
    }

    /// <summary>
    /// Sends a frame and reads back exactly <see cref="ServoProtocol.FrameLength"/> reply bytes.
    /// Runs on a background thread so the UI stays responsive.
    /// </summary>
    public Task<byte[]> TransactAsync(byte[] frame, SerialSettings settings, CancellationToken cancellationToken = default)
        => Task.Run(() => Transact(frame, settings, cancellationToken), cancellationToken);

    /// <summary>Synchronous request/response transaction. Call from a background thread.</summary>
    public byte[] Transact(byte[] frame, SerialSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(settings);

        lock (_gate)
        {
            if (_port is not { IsOpen: true })
                throw new InvalidOperationException("The serial port is not open.");

            _port.DiscardInBuffer();
            _port.DiscardOutBuffer();
            _port.Write(frame, 0, frame.Length);

            // On a shared single wire our own transmission is echoed back. Give it time to
            // arrive, then clear it so we only read the device's reply.
            if (settings.SingleWireEcho)
            {
                Thread.Sleep(Math.Max(0, settings.TurnaroundDelayMs));
                _port.DiscardInBuffer();
            }

            return ReadExact(_port, ServoProtocol.FrameLength, settings.ResponseTimeoutMs, cancellationToken);
        }
    }

    private static byte[] ReadExact(SerialPort port, int count, int timeoutMs, CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        int read = 0;
        var sw = Stopwatch.StartNew();

        while (read < count)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int remaining = timeoutMs - (int)sw.ElapsedMilliseconds;
            if (remaining <= 0)
                throw new TimeoutException($"Timed out waiting for a {count}-byte reply (received {read}).");

            port.ReadTimeout = remaining;
            try
            {
                int n = port.Read(buffer, read, count - read);
                if (n > 0) read += n;
            }
            catch (TimeoutException)
            {
                throw new TimeoutException($"Timed out waiting for a {count}-byte reply (received {read}).");
            }
        }

        return buffer;
    }

    public void Dispose() => Close();
}
