namespace Swyft.ServoProgrammer.Models;

/// <summary>
/// Plain data container for the full set of servo parameters exchanged with the device.
/// Field meanings and value ranges were recovered from the original GX programmer firmware protocol.
/// </summary>
public sealed class ServoParameters
{
    // 8-bit values (0-255)
    public int Torque { get; set; }             // Torque proportion
    public int IntegralProportion { get; set; } // Integral (I) proportion
    public int BigAngle { get; set; }           // Angle proportion toward the maximum pulse width
    public int SmallAngle { get; set; }         // Angle proportion toward the minimum pulse width
    public int Sensitivity { get; set; }        // Dead-band / sensitivity, in microseconds
    public int IntegralSpeed { get; set; }      // Integral speed, in units of 3 ms

    // 16-bit values (little-endian on the wire)
    public int Brake { get; set; }              // Brake proportion (0-65535)
    public int MinPulse { get; set; }           // Minimum pulse width, microseconds
    public int MaxPulse { get; set; }           // Maximum pulse width, microseconds
    public int MaxDuty { get; set; }            // Maximum duty cycle (0-399)
    public int Center { get; set; }             // Center / median position, microseconds (500-2500)

    // Flags (packed into a single byte on the wire)
    public bool DirectionReverse { get; set; }  // CW (false) / CCW (true)
    public bool StallProtection { get; set; }   // Stall (locked-rotor) protection enabled
    public bool SoftStart { get; set; }         // Soft-start enabled

    public ServoParameters Clone() => (ServoParameters)MemberwiseClone();
}
