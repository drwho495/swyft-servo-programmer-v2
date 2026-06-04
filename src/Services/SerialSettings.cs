using System.IO.Ports;

namespace Swyft.ServoProgrammer.Services;

/// <summary>
/// Serial port configuration. Defaults match the original servo programmer: 115200 baud, 8 data bits,
/// no parity, two stop bits (8-N-2), half-duplex on a single signal wire.
/// </summary>
public sealed class SerialSettings
{
    public int BaudRate { get; set; } = 115200;
    public int DataBits { get; set; } = 8;
    public Parity Parity { get; set; } = Parity.None;
    public StopBits StopBits { get; set; } = StopBits.Two;

    /// <summary>
    /// When true, the bytes we transmit are echoed back on the shared single wire and must be
    /// discarded before reading the device reply. This matches the original tool's behaviour.
    /// </summary>
    public bool SingleWireEcho { get; set; } = true;

    /// <summary>Delay (ms) after transmit before clearing the echo and reading the reply.</summary>
    public int TurnaroundDelayMs { get; set; } = 80;

    /// <summary>Maximum time (ms) to wait for a complete reply frame.</summary>
    public int ResponseTimeoutMs { get; set; } = 600;
}
