# SWYFT Servo Programmer V2

A modern Windows desktop utility from **SWYFT Robotics** for reading and writing RC servo
parameters over a single-wire half-duplex UART link (USB-to-UART adapter).

This is a clean-room rewrite of an older internal tool. The legacy app was a .NET Framework 4.0
WinForms program; its source was lost, so the wire protocol was recovered by decompiling the
shipped binary and re-implemented here on **.NET 8 / WPF** with a refreshed UI and several
usability improvements.

## Features

- **Automatic adapter detection on startup** — finds the USB-to-UART adapter by name (no button to press)
- **Read Servo** to pull current parameters and **Flash Servo** to write edited parameters back
- Every parameter editable via a validated **numeric field** with range checking
- **Total Range (0–320°)** control plus a **Continuous Rotation** toggle (handles the Left/Right math for you)
- Direction (CW/CCW), stall protection and **Ramp Mode** as toggle switches
- **Set defaults** button to restore SWYFT's recommended baseline values
- Built-in **guide** (Getting Started + setting explanations + wiring diagram)
- Bundled CP210x USB driver — a **USB driver** button opens the driver folder for manual install
- Timestamped activity log and live connection status

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download) (to build) or the .NET 8 Desktop Runtime (to run)
- A USB-to-UART adapter wired for single-wire half-duplex to the servo signal line

## Build & run

```powershell
cd src
dotnet build
dotnet run
```

To produce a self-contained single executable:

```powershell
cd src
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## Wiring

The servo communicates on a single signal wire (half-duplex). Tie the adapter's **TX and RX
together** to that wire (typically through the adapter's single-wire mode or a resistor/diode),
share **ground**, and power the servo from an appropriate supply. Because TX and RX are shared,
the bytes the PC sends are echoed back; the app discards that echo automatically.

## Serial / protocol summary

- **115200 baud, 8 data bits, no parity, 2 stop bits (8-N-2)**
- Every message is a fixed **20-byte frame**:

| Byte | Meaning |
|------|---------|
| `0` | Header `0x55` |
| `1..17` | Payload (see below) |
| `18` | Checksum = `(sum of bytes 1..17) & 0xFF` |
| `19` | Footer `0xAA` |

Payload (UI name → firmware field):

| Byte(s) | UI name | Firmware field | Encoding |
|---------|---------|----------------|----------|
| `1` | P coefficient | Torque proportion | byte |
| `2` | I coefficient | Integral proportion | byte |
| `3` | Flags | — | bit0 direction, bit1 write-request, bit2 write-ack, bit3 read-valid, bit4 stall, bit5 ramp |
| `4` | Left range | Angle proportion (max side) | byte |
| `5` | Dead Zone | Sensitivity | byte |
| `6` | Integral speed | Integral speed (×3 ms) | byte |
| `7,8` | D coefficient | Brake proportion | uint16 LE |
| `9,10` | Min Signal (µs) | Min pulse width | uint16 LE |
| `11,12` | Max Signal (µs) | Max pulse width | uint16 LE |
| `13,14` | Max Power | Max duty cycle | uint16 LE |
| `15` | Right range | Angle proportion (min side) | byte |
| `16,17` | Middle Signal (µs) | Center / median | uint16 LE |

### Travel range & continuous rotation

Left and Right range are **not edited directly**. The UI exposes a single **Total Range (0–320°)**
control; it is split evenly and written to both Left and Right range using the linear calibration
`degrees = (50·V − 420) / 19` (so 270° → 111 per side, 320° → 130 per side). Enabling
**Continuous rotation** forces both Left and Right range to **255** (full-speed continuous rotation).

- **Read:** host sends a frame with header/footer and an all-zero payload; the servo replies with a
  full frame (valid when the checksum matches and the read-valid bit `0x08` is set).
- **Write:** host sends the populated frame; the servo echoes a frame with the write-ack bit `0x04`
  set on success.

> Note: the slider ranges for pulse widths (500–2500 µs) reflect typical RC servo values. If your
> hardware uses a different range, adjust the limits in `ViewModels/MainViewModel.cs`
> (`BuildParameters`).

## Project layout

```
src/
  Models/        ServoParameters, ParameterKey
  Protocol/      ServoProtocol  (frame build/parse — pure, unit-testable)
  Services/      ServoConnection (serial I/O, WMI port detection), SerialSettings, DriverInstaller
  ViewModels/    MainViewModel, ParameterViewModel, LogEntry
  Infrastructure/ ObservableObject, RelayCommand, AsyncRelayCommand
  Themes/        Brand.xaml (SWYFT colors #01A1FF / white)
  Assets/        Logo, wordmark, app icon, wiring diagram
  Drivers/       Bundled CP210x USB-UART driver
  MainWindow.xaml / GuideWindow.xaml / App.xaml
```

## Download

Prebuilt Windows downloads are published on the [Releases](../../releases) page. The release
is a **self-contained** build — no .NET runtime install required, just unzip and run
`SwyftServoProgrammerV2.exe`.

## License

Released under the [MIT License](LICENSE). © 2026 SWYFT Robotics.
