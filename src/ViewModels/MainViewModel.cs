using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Swyft.ServoProgrammer.Infrastructure;
using Swyft.ServoProgrammer.Models;
using Swyft.ServoProgrammer.Protocol;
using Swyft.ServoProgrammer.Services;

namespace Swyft.ServoProgrammer.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly ServoConnection _connection = new();
    private readonly Dictionary<ParameterKey, ParameterViewModel> _byKey = new();

    private string? _selectedPort;
    private int _baudRate = 115200;
    private bool _isConnected;
    private bool _directionReverse;
    private bool _stallProtection;
    private bool _softStart;
    private bool _continuousRotation;
    private int _totalRangeDegrees = 270;
    private string _statusMessage = "Ready.";
    private LogLevel _statusLevel = LogLevel.Info;

    // Travel-range conversion (per-side value V <-> total degrees), derived from the
    // calibration points 130 -> 320 deg and 111 -> 270 deg:  degrees = (50V - 420) / 19.
    public const int MaxTravelDegrees = 320;
    private const byte ContinuousRotationValue = 255;

    public MainViewModel()
    {
        Parameters = BuildParameters();
        foreach (var p in Parameters)
        {
            _byKey[p.Key] = p;
            p.PropertyChanged += OnParameterChanged;
        }

        RefreshPortsCommand = new RelayCommand(RefreshPorts);
        ConnectCommand = new AsyncRelayCommand(ToggleConnectionAsync, () => IsConnected || !string.IsNullOrWhiteSpace(SelectedPort));
        ReadCommand = new AsyncRelayCommand(ReadAsync, CanCommunicate);
        WriteCommand = new AsyncRelayCommand(WriteAsync, () => CanCommunicate() && AllParametersValid);
        ShowGuideCommand = new RelayCommand(ShowGuide);
        SetDefaultsCommand = new RelayCommand(SetDefaults);
        OpenDriverFolderCommand = new RelayCommand(OpenDriverFolder);

        RefreshPorts();
        Log(LogLevel.Info, "Welcome to SWYFT Servo Programmer V2. Detecting the servo programmer\u2026");
    }

    public ObservableCollection<ParameterViewModel> Parameters { get; }
    public ObservableCollection<string> Ports { get; } = new();
    public ObservableCollection<LogEntry> LogEntries { get; } = new();

    public int[] AvailableBaudRates { get; } = { 9600, 19200, 38400, 57600, 115200, 230400 };

    public RelayCommand RefreshPortsCommand { get; }
    public AsyncRelayCommand ConnectCommand { get; }
    public AsyncRelayCommand ReadCommand { get; }
    public AsyncRelayCommand WriteCommand { get; }
    public RelayCommand ShowGuideCommand { get; }
    public RelayCommand SetDefaultsCommand { get; }
    public RelayCommand OpenDriverFolderCommand { get; }

    public string? SelectedPort
    {
        get => _selectedPort;
        set
        {
            if (SetProperty(ref _selectedPort, value))
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    public int BaudRate
    {
        get => _baudRate;
        set => SetProperty(ref _baudRate, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (SetProperty(ref _isConnected, value))
            {
                OnPropertyChanged(nameof(ConnectButtonText));
                OnPropertyChanged(nameof(ConnectionStateText));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool DirectionReverse
    {
        get => _directionReverse;
        set => SetProperty(ref _directionReverse, value);
    }

    public bool StallProtection
    {
        get => _stallProtection;
        set => SetProperty(ref _stallProtection, value);
    }

    public bool SoftStart
    {
        get => _softStart;
        set => SetProperty(ref _softStart, value);
    }

    /// <summary>Total mechanical travel in degrees (both sides combined). Mapped to the per-side range value.</summary>
    public int TotalRangeDegrees
    {
        get => _totalRangeDegrees;
        set => SetProperty(ref _totalRangeDegrees, Math.Clamp(value, 0, MaxTravelDegrees));
    }

    /// <summary>When enabled, forces both Left and Right range to 255 for full-speed continuous rotation.</summary>
    public bool ContinuousRotation
    {
        get => _continuousRotation;
        set
        {
            if (SetProperty(ref _continuousRotation, value))
                OnPropertyChanged(nameof(IsRangeEditable));
        }
    }

    /// <summary>The Total Range control is only editable when continuous rotation is off.</summary>
    public bool IsRangeEditable => !_continuousRotation;

    public string ConnectButtonText => IsConnected ? "Disconnect" : "Connect";

    public string ConnectionStateText => IsConnected
        ? $"Connected \u2022 {_connection.PortName}"
        : "Disconnected";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public LogLevel StatusLevel
    {
        get => _statusLevel;
        private set => SetProperty(ref _statusLevel, value);
    }

    public bool AllParametersValid => Parameters.All(p => p.IsValid);

    private void OnParameterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ParameterViewModel.Value) or nameof(ParameterViewModel.IsValid))
        {
            OnPropertyChanged(nameof(AllParametersValid));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    private void RefreshPorts()
    {
        var current = SelectedPort;
        Ports.Clear();
        foreach (var name in ServoConnection.GetPortNames().OrderBy(NaturalPortKey))
            Ports.Add(name);

        if (current is not null && Ports.Contains(current))
            SelectedPort = current;
        else if (Ports.Count > 0)
            SelectedPort = Ports[0];
        else
            SelectedPort = null;

        if (Ports.Count > 0)
        {
            Log(LogLevel.Info, $"Found {Ports.Count} serial port(s).");
        }
        else
        {
            Log(LogLevel.Warning, "No serial ports found. If your programmer isn't listed, click \"Install USB driver\".");
        }
    }

    private async Task ToggleConnectionAsync()
    {
        // Open/close run on a background thread: SerialPort.Open() can block on some adapters and
        // SerialPort.Close() is known to stall, either of which would freeze the UI if run inline.
        if (IsConnected)
        {
            await Task.Run(() => _connection.Close());
            IsConnected = false;
            Log(LogLevel.Info, "Disconnected.");
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedPort))
        {
            Log(LogLevel.Warning, "Select a COM port first.");
            return;
        }

        var port = SelectedPort;
        var settings = BuildSettings();
        try
        {
            await Task.Run(() => _connection.Open(port, settings));
            IsConnected = true;
            Log(LogLevel.Success, $"Connected to {port} at {BaudRate} baud (8-N-2).");
        }
        catch (Exception ex)
        {
            IsConnected = false;
            Log(LogLevel.Error, $"Failed to open {port}: {ex.Message}");
        }
    }

    /// <summary>Read/Flash work whenever a port is open or selectable (the port auto-opens on demand).</summary>
    private bool CanCommunicate() => IsConnected || !string.IsNullOrWhiteSpace(SelectedPort);

    /// <summary>Opens the selected port if it isn't already open. Returns false (and logs) on failure.</summary>
    private async Task<bool> EnsureConnectedAsync()
    {
        if (IsConnected) return true;

        if (string.IsNullOrWhiteSpace(SelectedPort))
        {
            Log(LogLevel.Warning, "Select a COM port first.");
            return false;
        }

        var port = SelectedPort;
        var settings = BuildSettings();
        try
        {
            await Task.Run(() => _connection.Open(port, settings));
            IsConnected = true;
            Log(LogLevel.Success, $"Connected to {port} at {BaudRate} baud (8-N-2).");
            return true;
        }
        catch (Exception ex)
        {
            IsConnected = false;
            Log(LogLevel.Error, $"Failed to open {port}: {ex.Message}");
            return false;
        }
    }

    private async Task ReadAsync()
    {
        if (!await EnsureConnectedAsync()) return;
        try
        {
            Log(LogLevel.Info, "Reading parameters from servo\u2026");
            var settings = BuildSettings();
            var reply = await _connection.TransactAsync(ServoProtocol.BuildReadFrame(), settings);

            if (ServoProtocol.TryParseReadReply(reply, out var parameters))
            {
                ApplyParameters(parameters);
                Log(LogLevel.Success, "Read successful. Parameters loaded from servo.");
            }
            else
            {
                Log(LogLevel.Error, "Read failed: the reply was invalid or incomplete. Check wiring and try again.");
            }
        }
        catch (TimeoutException)
        {
            Log(LogLevel.Error, "Read timed out. No reply from the servo \u2014 check the connection and power.");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Read error: {ex.Message}");
        }
    }

    private async Task WriteAsync()
    {
        if (!AllParametersValid)
        {
            Log(LogLevel.Warning, "One or more parameters are out of range. Fix the highlighted fields before writing.");
            return;
        }

        if (!await EnsureConnectedAsync()) return;

        try
        {
            Log(LogLevel.Info, "Writing parameters to servo\u2026");
            var settings = BuildSettings();
            var frame = ServoProtocol.BuildWriteFrame(CollectParameters());
            var reply = await _connection.TransactAsync(frame, settings);

            if (ServoProtocol.IsWriteAck(reply))
                Log(LogLevel.Success, "Write successful. The servo acknowledged the new parameters.");
            else
                Log(LogLevel.Error, "Write failed: no valid acknowledgement from the servo.");
        }
        catch (TimeoutException)
        {
            Log(LogLevel.Error, "Write timed out. No acknowledgement from the servo \u2014 check the connection.");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Write error: {ex.Message}");
        }
    }

    /// <summary>Run once at startup to find and connect to the servo programmer automatically.</summary>
    public Task InitializeAsync() => AutoDetectAsync();

    private async Task AutoDetectAsync()
    {
        // Identify the adapter by its Windows device name rather than opening every port. Blindly
        // opening and closing arbitrary COM ports is unreliable with System.IO.Ports (closing a port
        // can crash the process via an internal thread race), so we pick the most likely USB-to-UART
        // port and then verify with the normal Read, which never closes the port on failure.
        var ports = await Task.Run(ServoConnection.DescribePorts);
        if (ports.Count == 0)
        {
            Log(LogLevel.Warning, "No serial ports found. If your programmer isn't listed, click \"Install USB driver\".");
            return;
        }

        var best = ports
            .OrderByDescending(p => AdapterScore(p.Description))
            .ThenBy(p => NaturalPortKey(p.Name))
            .First();

        SelectedPort = best.Name;
        Log(LogLevel.Info, $"Auto-detect selected {best.Name} \u2014 {best.Description}. Verifying\u2026");

        await ReadAsync();
    }

    /// <summary>Ranks a COM port by how likely it is to be the servo's USB-to-UART adapter.</summary>
    private static int AdapterScore(string description)
    {
        var d = description.ToLowerInvariant();
        if (d.Contains("cp210") || d.Contains("silicon labs")) return 100; // the bundled adapter
        if (d.Contains("usb") && (d.Contains("uart") || d.Contains("serial"))) return 80;
        if (d.Contains("ch340") || d.Contains("ch341") || d.Contains("ftdi") || d.Contains("ft232")) return 70;
        if (d.Contains("usb")) return 50;
        if (d.Contains("bluetooth")) return -100; // avoid Bluetooth virtual ports
        return 0;
    }

    private SerialSettings BuildSettings() => new()
    {
        BaudRate = BaudRate,
        // The servo uses a single-wire half-duplex link, so our own transmission is echoed back on
        // the shared wire and must be discarded before reading the reply. This is always on.
        SingleWireEcho = true
    };

    /// <summary>Per-side range value (Left = Right) currently selected.</summary>
    private int RangeValue => ContinuousRotation ? ContinuousRotationValue : DegreesToRangeValue(TotalRangeDegrees);

    public static int DegreesToRangeValue(int degrees)
    {
        degrees = Math.Clamp(degrees, 0, MaxTravelDegrees);
        return Math.Clamp((int)Math.Round((19.0 * degrees + 420.0) / 50.0), 0, 255);
    }

    public static int RangeValueToDegrees(int value)
        => Math.Clamp((int)Math.Round((50.0 * value - 420.0) / 19.0), 0, MaxTravelDegrees);

    private ServoParameters CollectParameters()
    {
        int range = RangeValue;
        return new ServoParameters
        {
            Torque = _byKey[ParameterKey.P].Value,
            IntegralProportion = _byKey[ParameterKey.I].Value,
            Brake = _byKey[ParameterKey.D].Value,
            IntegralSpeed = _byKey[ParameterKey.IntegralSpeed].Value,
            MinPulse = _byKey[ParameterKey.MinSignal].Value,
            MaxPulse = _byKey[ParameterKey.MaxSignal].Value,
            Center = _byKey[ParameterKey.MiddleSignal].Value,
            Sensitivity = _byKey[ParameterKey.DeadZone].Value,
            MaxDuty = _byKey[ParameterKey.MaxPower].Value,
            BigAngle = range,   // Left range
            SmallAngle = range, // Right range
            DirectionReverse = DirectionReverse,
            StallProtection = StallProtection,
            SoftStart = SoftStart
        };
    }

    private void ApplyParameters(ServoParameters p)
    {
        void Apply()
        {
            _byKey[ParameterKey.P].Value = p.Torque;
            _byKey[ParameterKey.I].Value = p.IntegralProportion;
            _byKey[ParameterKey.D].Value = p.Brake;
            _byKey[ParameterKey.IntegralSpeed].Value = p.IntegralSpeed;
            _byKey[ParameterKey.MinSignal].Value = p.MinPulse;
            _byKey[ParameterKey.MaxSignal].Value = p.MaxPulse;
            _byKey[ParameterKey.MiddleSignal].Value = p.Center;
            _byKey[ParameterKey.DeadZone].Value = p.Sensitivity;
            _byKey[ParameterKey.MaxPower].Value = p.MaxDuty;

            // Left/Right range -> Total Range or continuous rotation.
            if (p.BigAngle >= ContinuousRotationValue && p.SmallAngle >= ContinuousRotationValue)
            {
                ContinuousRotation = true;
            }
            else
            {
                ContinuousRotation = false;
                int avg = (int)Math.Round((p.BigAngle + p.SmallAngle) / 2.0);
                TotalRangeDegrees = RangeValueToDegrees(avg);
            }

            DirectionReverse = p.DirectionReverse;
            StallProtection = p.StallProtection;
            SoftStart = p.SoftStart;

            Log(LogLevel.Info,
                $"Applied values \u2192 P={p.Torque}, I={p.IntegralProportion}, D={p.Brake}, " +
                $"MinSignal={p.MinPulse}, MaxSignal={p.MaxPulse}, MiddleSignal={p.Center}, " +
                $"DeadZone={p.Sensitivity}, MaxPower={p.MaxDuty}, Range(L/R)={p.BigAngle}/{p.SmallAngle}, " +
                $"Dir={(p.DirectionReverse ? "Reverse" : "Normal")}, Stall={(p.StallProtection ? "On" : "Off")}, " +
                $"Ramp={(p.SoftStart ? "On" : "Off")}.");
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            Apply();
        else
            dispatcher.Invoke(Apply);
    }

    /// <summary>Recommended factory defaults for a standard (non-continuous) SWYFT servo.</summary>
    private void SetDefaults()
    {
        ApplyParameters(new ServoParameters
        {
            Torque = 19,             // P
            IntegralProportion = 13, // I
            Brake = 47,              // D
            IntegralSpeed = 20,
            MinPulse = 500,          // Min Signal
            MaxPulse = 2500,         // Max Signal
            Center = 1500,           // Middle Signal
            Sensitivity = 5,         // Dead Zone
            MaxDuty = 400,           // Max Power
            BigAngle = 111,          // Left range  -> Total Range 270 deg
            SmallAngle = 111,        // Right range
            DirectionReverse = true, // Direction 1
            StallProtection = true,
            SoftStart = true         // Ramp mode
        });
        Log(LogLevel.Info, "Loaded default parameters. Click \"Flash Servo\" to write them to the servo.");
    }

    private static void ShowGuide()
    {
        var window = new Swyft.ServoProgrammer.GuideWindow { Owner = Application.Current?.MainWindow };
        window.ShowDialog();
    }

    private void OpenDriverFolder()
    {
        try
        {
            var folder = DriverInstaller.DriverFolder;
            if (!Directory.Exists(folder))
            {
                Log(LogLevel.Error, $"Driver folder not found at \"{folder}\".");
                return;
            }

            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
            Log(LogLevel.Info,
                "Opened the CP210x driver folder. To install: right-click \"silabser.inf\" and choose Install, " +
                "or run the included CP210x installer (.exe), then reconnect the programmer.");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Could not open the driver folder: {ex.Message}");
        }
    }

    private void Log(LogLevel level, string message)
    {
        void Append()
        {
            LogEntries.Add(new LogEntry(level, message));
            while (LogEntries.Count > 500) LogEntries.RemoveAt(0);
            StatusMessage = message;
            StatusLevel = level;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            Append();
        else
            dispatcher.Invoke(Append);
    }

    private static string NaturalPortKey(string port)
        => int.TryParse(new string(port.Where(char.IsDigit).ToArray()), out var n)
            ? n.ToString("D5")
            : port;

    private static ObservableCollection<ParameterViewModel> BuildParameters() => new()
    {
        new(ParameterKey.P, "P Coefficient", "",
            "Proportional: how strongly the servo reacts to the error between its current and target position. Higher P = faster response, but can cause overshoot or oscillation.",
            0, 255, 128),
        new(ParameterKey.I, "I Coefficient", "",
            "Integral: corrects accumulated error over time, removing steady-state offset. Higher I = removes offset faster, but can reduce stability.",
            0, 255, 0),
        new(ParameterKey.D, "D Coefficient", "",
            "Derivative: dampens the response to reduce overshoot and oscillation. Higher D = smoother response, but can slow the servo down.",
            0, 65535, 0),
        new(ParameterKey.IntegralSpeed, "Integral speed", "\u00d73 ms",
            "Integration interval. Higher values integrate more slowly.",
            0, 255, 10),
        new(ParameterKey.MinSignal, "Min Signal", "\u00b5s",
            "PWM signal value corresponding to the servo's leftmost position.",
            500, 2500, 500),
        new(ParameterKey.MaxSignal, "Max Signal", "\u00b5s",
            "PWM signal value corresponding to the servo's rightmost position.",
            500, 2500, 2500),
        new(ParameterKey.MiddleSignal, "Middle Signal", "\u00b5s",
            "PWM signal value for the servo's center position. Recommended: 1500.",
            500, 2500, 1500),
        new(ParameterKey.DeadZone, "Dead Zone", "\u00b5s",
            "How close the servo must be to its target before it stops correcting. Smaller = higher precision but may add jitter.",
            0, 255, 5),
        new(ParameterKey.MaxPower, "Max Power", "",
            "Maximum power output to the servo motor (0\u2013500, where 500 is full power).",
            0, 500, 500)
    };

    public void Dispose() => _connection.Dispose();
}
