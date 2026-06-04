using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace Swyft.ServoProgrammer.ViewModels;

public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>A single timestamped line in the activity log.</summary>
public sealed class LogEntry
{
    public LogEntry(LogLevel level, string message)
    {
        Level = level;
        Message = message;
        Timestamp = DateTime.Now;
    }

    public LogLevel Level { get; }
    public string Message { get; }
    public DateTime Timestamp { get; }

    public string TimeText => Timestamp.ToString("HH:mm:ss");

    public Brush Color => Level switch
    {
        LogLevel.Success => new SolidColorBrush(MediaColor.FromRgb(0x1B, 0x9E, 0x4B)),
        LogLevel.Warning => new SolidColorBrush(MediaColor.FromRgb(0xC8, 0x7A, 0x00)),
        LogLevel.Error => new SolidColorBrush(MediaColor.FromRgb(0xD0, 0x2B, 0x2B)),
        _ => new SolidColorBrush(MediaColor.FromRgb(0x33, 0x44, 0x55))
    };
}
