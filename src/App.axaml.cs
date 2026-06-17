using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace Swyft.ServoProgrammer;

public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SWYFT Servo Programmer V2", "error.log");

    public App()
    {
        // Keep the app alive when something unexpected happens (e.g. a flaky COM port) instead of
        // crashing to the desktop. Errors are logged and, for UI-thread failures, surfaced to the user.
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        => WriteLog("Domain", e.ExceptionObject as Exception);

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteLog("Task", e.Exception);
        e.SetObserved();
    }

    private static int _logCount;
    private const int MaxLogEntries = 200;

    private static void WriteLog(string source, Exception? ex)
    {
        if (ex is null) return;
        // Cap how many entries we ever write so a recurring exception can't balloon the file.
        if (Interlocked.Increment(ref _logCount) > MaxLogEntries) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ({source}) {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Never let logging itself throw.
        }
    }
}
