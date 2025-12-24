# SimRate Sharp

An overlay for monitoring and controlling simulation rate in **Microsoft Flight Simulator 2024**, with advanced engine protection features.

## Features

### Core Features
- **Clean Overlay** - Minimal, semi-transparent design that integrates seamlessly with MSFS 2024
- **Real-time Monitoring** - Displays flight data with configurable polling rates (250ms - 2000ms)
- **One-Click Reset** - Instantly return to 1x simulation rate with integrated button
- **Joystick Button Support** - Configure any button on your HOTAS/flight stick to trigger 1x reset
- **Multi-Device Support** - Select which joystick device to use
- **Customizable Display** - Show/hide individual panels, adjust opacity and position
- **Settings Persistence** - All preferences saved automatically
- **Auto-reconnect** - Automatically connects/reconnects when MSFS starts/stops

### Flight Data Display
- **Sim Rate** - Current simulation rate with integrated reset button
- **Ground Speed** - Speed in knots
- **AGL (Above Ground Level)** - Altitude above terrain in feet
- **Glide Slope** - Descent angle in degrees
- **Wind** - Wind speed (knots) and relative direction with visual arrow indicator
- **Torque** - Multi-engine torque monitoring with visual bar gauges (optional)

### Advanced: Overtorque Protection System (Optional)
- **Multi-Engine Support** - Monitors up to 4 engines independently
- **Visual Indicators** - Vertical bar gauges with color-coded warnings (green → yellow → red)
- **Intelligent Throttle Limiting** - Automatically reduces throttle when engines exceed torque limits
- **Proportional Response** - Reduction severity scales with overtorque amount
- **Per-Engine Control** - Only intervenes on engines exceeding limits
- **Iterative Correction** - Multiple adjustments with configurable stabilization time
- **Audio Alerts** - Claxon-style warning tone on intervention
- **Fully Configurable** - All parameters adjustable via context menu
- **Zero Overhead When Disabled** - No performance impact when feature is off

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
2. A minimal HUD overlay will appear showing flight data (by default: sim rate, ground speed, AGL, glide slope, and wind)
3. If MSFS is not running, no data will display
4. Start MSFS - the app will automatically connect and display live data
5. Click the **[1x]** button integrated with the sim rate display to instantly reset to 1x

The application will automatically reconnect if MSFS closes and restarts.

### Customization (Right-Click Menu)

Right-click anywhere on the overlay to access settings:

**Display Settings:**
- **Display** - Show/hide individual panels (Sim Rate, Ground Speed, AGL, Glide Slope, Wind)
- **Opacity** - Adjust transparency (20%, 40%, 60%, 80%, 100%)
- **Polling Rate** - Data update frequency (250ms - 2000ms)

**Joystick Configuration:**
- **Joystick Device** - Select which joystick/HOTAS device to use for button mapping
- **Configure Joystick Button** - Set a button to trigger 1x sim rate reset

**Overtorque Protection (Advanced):**
- **Enable Torque Limiter** - Activate engine protection system
- **Show Torque Display** - Display vertical bar gauges for engine torque
- **Max Torque %** - Set maximum torque limit (default: 100%)
- **Warning Threshold %** - When bars turn yellow (default: 90%)
- **Reduction Aggression** - How aggressively to reduce throttle (default: 2.5x)
- **Minimum Throttle %** - Safety floor to prevent stalling (default: 40%)
- **Intervention Cooldown** - Time between corrections (default: 2000ms)
- **Interventions** - View count of protective actions taken

**Other:**
- **About** - View version info and GitHub repository link
- **Exit** - Close the application

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

### Overtorque Protection System

The optional Overtorque Protection (OTP) system helps prevent expensive engine damage during high-workload flight operations.

**Use Cases:**
- Firefighting operations (e.g., CL-415 water bombing)
- Turboprop aircraft with PT6A or similar engines
- Training flights where students may over-stress engines
- Any situation where precise engine management is challenging

**How It Works:**
1. System monitors torque percentage for each engine (1-4 engines supported)
2. Visual bars display real-time torque levels with color coding:
   - 🟢 Green (0-90%): Normal operation
   - 🟡 Yellow (90-100%): Warning - approaching limit
   - 🔴 Red (>100%): Overtorque - intervention active
3. When torque exceeds configured limit, system automatically reduces throttle
4. Throttle reduction is proportional to overtorque severity
5. System makes iterative corrections until torque returns to safe levels
6. Audio alert (claxon tone) sounds on each intervention

**Configuration Tips:**
- **For normal operations**: Max Torque 100%, Aggression 2.5x, Cooldown 2000ms
- **For aggressive flying**: Max Torque 105%, Aggression 2.0x, Cooldown 3000ms
- **For training**: Max Torque 95%, Aggression 3.0x, Cooldown 1000ms
- **For emergencies**: Disable OTP temporarily to allow full engine authority

**Important Notes:**
- System uses `SetDataOnSimObject` to override hardware throttle input
- Each engine is monitored and controlled independently
- Throttle will never reduce below configured minimum (default 40%)
- Feature has zero overhead when disabled
- All settings are saved and restored between sessions

### Settings Persistence

All settings are automatically saved to:
```
%APPDATA%\SimRateSharp\settings.json
```

This includes:
- Window position
- Opacity
- Polling rate
- Display visibility settings (which panels are shown/hidden)
- Selected joystick device
- Configured button number
- Torque limiter configuration (all parameters)
