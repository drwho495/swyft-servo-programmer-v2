using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Swyft.ServoProgrammer.Services;
using Swyft.ServoProgrammer.ViewModels;

namespace Swyft.ServoProgrammer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private ListBox? _logList;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = _viewModel;

        _logList = this.Get<ListBox>("LogList");

        _viewModel.LogEntries.CollectionChanged += OnLogChanged;

        // Hide the driver button on Linux (drivers are kernel-built)
        var driverButton = this.Get<Button>("DriverButton");
        driverButton.IsVisible = DriverInstaller.IsDriverInstallationRequired;

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, System.EventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.InitializeAsync();
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || _logList is null) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_logList!.Items.Count > 0)
                _logList.ScrollIntoView(_logList.Items[^1]!);
        }, Avalonia.Threading.DispatcherPriority.ContextIdle);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
