using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Swyft.ServoProgrammer.ViewModels;

namespace Swyft.ServoProgrammer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    private static readonly Brush ConnectedBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x84));
    private static readonly Brush DisconnectedBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        _viewModel.LogEntries.CollectionChanged += OnLogChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateConnectionDot();

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.InitializeAsync();
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;

        // Defer the auto-scroll: calling ScrollIntoView synchronously inside the CollectionChanged
        // handler corrupts the ListBox item generator when entries arrive in quick succession
        // ("ItemsControl is inconsistent with its items source"). Running it at a lower priority lets
        // the generator finish processing the Add first.
        Dispatcher.BeginInvoke(
            () =>
            {
                if (LogList.Items.Count > 0)
                    LogList.ScrollIntoView(LogList.Items[^1]);
            },
            DispatcherPriority.ContextIdle);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsConnected))
            UpdateConnectionDot();
    }

    private void UpdateConnectionDot()
        => ConnDot.Fill = _viewModel.IsConnected ? ConnectedBrush : DisconnectedBrush;

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
