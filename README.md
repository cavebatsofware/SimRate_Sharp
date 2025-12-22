# SimRate Sharp

A modern HUD overlay for monitoring and controlling simulation rate in **Microsoft Flight Simulator 2024**.

## Features

- **Clean Overlay** - Minimal, semi-transparent design that integrates seamlessly with MSFS 2024
- **Real-time Monitoring** - Displays current simulation rate and ground speed (updates every 500ms)
- **One-Click Reset** - Instantly return to 1x simulation rate with on-screen button
- **Joystick Button Support** - Configure any button on your HOTAS/flight stick to trigger 1x reset
- **Multi-Device Support** - Select which joystick device to use
- **Customizable** - Adjust opacity, size, and position to your preference
- **Settings Persistence** - All preferences saved automatically
- **Auto-reconnect** - Automatically connects/reconnects when MSFS starts/stops

## Requirements

- .NET 10.0 or later
- Microsoft Flight Simulator 2024 SDK (to build from source)
- Windows (WPF application)

## Installation

### Option 1: Install from Installer (Recommended)

1. Download the latest `SimRateSharp_Setup_v1.0.0.exe` from the releases page
2. Run the installer
3. Choose installation options:
   - Desktop shortcut (optional)
   - Launch at Windows startup (optional)
4. Click Install

The installer will:
- Install SimRate Sharp to `C:\Program Files\SimRate Sharp`
- Create Start Menu shortcuts (including Debug Mode shortcut)
- Optionally create Desktop shortcut
- Optionally add to Windows Startup

### Option 2: Build from Source

1. Clone the repository
2. Ensure you have the MSFS 2024 SDK installed
3. Build the project:
```bash
dotnet build SimRateSharp\SimRateSharp.csproj -c Release
```
4. Run from `SimRateSharp\bin\Release\net10.0-windows\SimRateSharp.exe`

## Running

**Normal Mode:**
```bash
SimRateSharp.exe
```

**Debug Mode (with file logging):**
```bash
SimRateSharp.exe --debug
```

Debug logs are written to: `%APPDATA%\SimRateSharp\Logs\`

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

## Building the Installer

To create the installer package:

1. Install [Inno Setup](https://jrsoftware.org/isinfo.php) (6.0 or later)
2. Ensure you have the `app.ico` icon file in the root directory
3. Run the build script:
```bash
build_installer.bat
```

This will:
- Build the application in Release mode
- Create the installer using [installer.iss](installer.iss)
- Output the installer to `installer_output\SimRateSharp_Setup_v1.0.0.exe`

Alternatively, you can build manually:
```bash
dotnet build SimRateSharp\SimRateSharp.csproj -c Release
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
```

## Usage

### Basic Operation

1. Launch the application (normal or debug mode)
2. A minimal HUD overlay will appear showing:
   - ⚡ Current simulation rate (e.g., "1.00x")
   - 🛫 Ground speed in knots
   - [1x] button to instantly reset sim rate to 1x
3. If MSFS is not running no data will display
4. Start MSFS - the app will automatically connect and display live data

The application will automatically reconnect if MSFS closes and restarts.

### Customization (Right-Click Menu)

Right-click anywhere on the overlay to access settings:

- **Opacity** - Adjust transparency (25%, 50%, 75%, 100%)
- **Size** - Change overlay size (Small, Medium, Large)
- **Joystick Device** - Select which joystick/HOTAS device to use for button mapping
- **Configure Joystick Button** - Set a button to trigger 1x sim rate reset
- **About** - View version info and GitHub repository link

### Joystick Button Configuration

To configure a joystick button for instant 1x reset:

1. Right-click the overlay
2. Select your joystick device from **Joystick Device** submenu
3. Click **Configure Joystick Button**
4. Press any button on your selected device
5. You'll see "✓ Button X captured!" confirmation
6. Press that button anytime during flight to instantly reset sim rate to 1x

If your device is not displayed it is probably because it does not register as a
"Joystick" device. The application could support it but would need to be updated.
For example, I have a throttle quadrant that does not show up but I don't use it for
this anyway so I didn't add support for it.

**Note:** If you change joystick devices, you'll need to reconfigure the button mapping.

### Settings Persistence

All settings are automatically saved to:
```
%APPDATA%\SimRateSharp\settings.json
```

This includes:
- Window position
- Opacity
- Size
- Selected joystick device
- Configured button number
