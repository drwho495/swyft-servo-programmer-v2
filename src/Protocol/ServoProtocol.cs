using Swyft.ServoProgrammer.Models;

namespace Swyft.ServoProgrammer.Protocol;

/// <summary>
/// Implements the 20-byte single-wire UART frame format used by the servo.
///
/// Frame layout (all frames are exactly <see cref="FrameLength"/> bytes):
///   [0]      Header,  always 0x55
///   [1..17]  17-byte payload
///   [18]     Checksum = (sum of bytes 1..17) AND 0xFF
///   [19]     Footer,  always 0xAA
///
/// Payload layout:
///   [1]      Torque proportion           (0-255)
///   [2]      Integral proportion         (0-255)
///   [3]      Flags byte (see Flag* constants)
///   [4]      Angle proportion - max side (0-255)
///   [5]      Sensitivity                 (0-255)
///   [6]      Integral speed              (0-255)
///   [7,8]    Brake proportion            (uint16, little-endian)
///   [9,10]   Min pulse width             (uint16, little-endian)
///   [11,12]  Max pulse width             (uint16, little-endian)
///   [13,14]  Max duty cycle              (uint16, little-endian)
///   [15]     Angle proportion - min side (0-255)
///   [16,17]  Center / median             (uint16, little-endian)
/// </summary>
public static class ServoProtocol
{
    public const int FrameLength = 20;
    public const byte Header = 0x55;
    public const byte Footer = 0xAA;

    // Flags packed into payload byte [3]
    public const byte FlagDirectionReverse = 0x01; // bit 0: CW/CCW
    public const byte FlagWriteRequest = 0x02;     // bit 1: set by host on every write
    public const byte FlagWriteAck = 0x04;         // bit 2: set by servo to acknowledge a write
    public const byte FlagReadValid = 0x08;        // bit 3: set by servo when returning valid read data
    public const byte FlagStallProtection = 0x10;  // bit 4: stall protection enabled
    public const byte FlagSoftStart = 0x20;        // bit 5: soft-start enabled

    /// <summary>Builds the 20-byte frame that writes the given parameters to the servo.</summary>
    public static byte[] BuildWriteFrame(ServoParameters p)
    {
        ArgumentNullException.ThrowIfNull(p);

        var b = new byte[FrameLength];
        b[0] = Header;
        b[1] = ToByte(p.Torque);
        b[2] = ToByte(p.IntegralProportion);

        byte flags = FlagWriteRequest; // bit 1 is always asserted on a host write
        if (p.DirectionReverse) flags |= FlagDirectionReverse;
        if (p.StallProtection) flags |= FlagStallProtection;
        if (p.SoftStart) flags |= FlagSoftStart;
        b[3] = flags;

        b[4] = ToByte(p.BigAngle);
        b[5] = ToByte(p.Sensitivity);
        b[6] = ToByte(p.IntegralSpeed);
        WriteUInt16(b, 7, p.Brake);
        WriteUInt16(b, 9, p.MinPulse);
        WriteUInt16(b, 11, p.MaxPulse);
        WriteUInt16(b, 13, p.MaxDuty);
        b[15] = ToByte(p.SmallAngle);
        WriteUInt16(b, 16, p.Center);

        b[18] = ComputeChecksum(b);
        b[19] = Footer;
        return b;
    }

    /// <summary>
    /// Builds the read-request frame. Matches the original tool exactly: header + footer with an
    /// all-zero payload and a zero checksum byte. The servo replies with a full populated frame.
    /// </summary>
    public static byte[] BuildReadFrame()
    {
        var b = new byte[FrameLength];
        b[0] = Header;
        b[19] = Footer;
        return b;
    }

    /// <summary>Computes the payload checksum (sum of bytes 1..17, truncated to a byte).</summary>
    public static byte ComputeChecksum(byte[] frame)
    {
        int sum = 0;
        for (int i = 1; i < 18; i++) sum += frame[i];
        return (byte)sum;
    }

    /// <summary>True when the frame has a valid header, footer and checksum.</summary>
    public static bool IsValidFrame(byte[] frame)
    {
        if (frame is null || frame.Length < FrameLength) return false;
        if (frame[0] != Header || frame[19] != Footer) return false;
        return frame[18] == ComputeChecksum(frame);
    }

    /// <summary>True when a reply confirms a successful write (valid frame with the write-ack bit set).</summary>
    public static bool IsWriteAck(byte[] frame)
        => IsValidFrame(frame) && (frame[3] & FlagWriteAck) == FlagWriteAck;

    /// <summary>
    /// Attempts to decode a read reply into parameters.
    /// Requires a valid frame with the read-valid bit set.
    /// </summary>
    public static bool TryParseReadReply(byte[] frame, out ServoParameters parameters)
    {
        parameters = new ServoParameters();
        if (!IsValidFrame(frame)) return false;
        if ((frame[3] & FlagReadValid) != FlagReadValid) return false;

        parameters = Decode(frame);
        return true;
    }

    /// <summary>Decodes payload bytes into a parameter set (no validation beyond field extraction).</summary>
    public static ServoParameters Decode(byte[] frame)
    {
        return new ServoParameters
        {
            Torque = frame[1],
            IntegralProportion = frame[2],
            DirectionReverse = (frame[3] & FlagDirectionReverse) != 0,
            StallProtection = (frame[3] & FlagStallProtection) != 0,
            SoftStart = (frame[3] & FlagSoftStart) != 0,
            BigAngle = frame[4],
            Sensitivity = frame[5],
            IntegralSpeed = frame[6],
            Brake = ReadUInt16(frame, 7),
            MinPulse = ReadUInt16(frame, 9),
            MaxPulse = ReadUInt16(frame, 11),
            MaxDuty = ReadUInt16(frame, 13),
            SmallAngle = frame[15],
            Center = ReadUInt16(frame, 16)
        };
    }

    private static byte ToByte(int value) => (byte)Math.Clamp(value, 0, 255);

    private static void WriteUInt16(byte[] buffer, int index, int value)
    {
        int v = Math.Clamp(value, 0, ushort.MaxValue);
        buffer[index] = (byte)(v & 0xFF);
        buffer[index + 1] = (byte)((v >> 8) & 0xFF);
    }

    private static int ReadUInt16(byte[] buffer, int index)
        => buffer[index] | (buffer[index + 1] << 8);
}
