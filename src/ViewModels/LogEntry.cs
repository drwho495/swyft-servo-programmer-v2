using Avalonia.Media;

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

    public SolidColorBrush Color => Level switch
    {
        LogLevel.Success => SolidColorBrush.Parse("#1B9E4B"),
        LogLevel.Warning => SolidColorBrush.Parse("#C87A00"),
        LogLevel.Error => SolidColorBrush.Parse("#D02B2B"),
        _ => SolidColorBrush.Parse("#334455")
    };
}
