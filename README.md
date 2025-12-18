# SimRate Sharp

A C# WPF application that monitors and displays the current simulation rate in Microsoft Flight Simulator 2024 using the SimConnect SDK.

## Features

- Real-time sim rate display (updates every 500ms)
- Auto-reconnect when MSFS starts/stops
- Clean, minimal UI showing current sim rate (e.g., "1.00x", "2.00x")
- Connection status indicator

## Requirements

- .NET 10.0 or later
- Microsoft Flight Simulator 2024 SDK
- Windows (WPF application)

## Building

```bash
dotnet build
```

## Running

```bash
dotnet run --project SimRateSharp
```

Or build and run the executable from `SimRateSharp\bin\Debug\net10.0-windows\SimRateSharp.exe`

## Implementation

The application uses:
- **SimConnectManager**: Handles SimConnect integration, auto-reconnect, and data polling
- **MainWindow**: Displays the sim rate with connection status
- **SimConnect SDK**: Reads the `SIMULATION RATE` SimVar

See [IMPLEMENTATION_NOTES.md](IMPLEMENTATION_NOTES.md) for detailed implementation documentation.

## SDK Configuration

The project references the SimConnect SDK at:
```
C:\MSFS 2024 SDK\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll
```

If your SDK is installed elsewhere, update the `HintPath` in [SimRateSharp.csproj](SimRateSharp/SimRateSharp.csproj#L14).

## Usage

1. Launch the application
2. If MSFS is not running, you'll see "Disconnected - Reconnecting..."
3. Start MSFS - the app will automatically connect
4. The sim rate will be displayed in large text (e.g., "1.00x")
5. Connection status is shown at the top (Green = Connected, Red = Disconnected)

The application will automatically reconnect if MSFS closes and restarts.
