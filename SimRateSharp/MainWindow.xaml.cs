/* SimRateSharp is a simple overlay application for MSFS to display
 * simulation rate and reset sim-rate via joystick button as well as displaying other vital data.
 *
 * Copyright (C) 2025 Grant DeFayette / CavebatSoftware LLC 
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, version 3 of the License.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace SimRateSharp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private SimConnectManager? _simConnectManager;
    private JoystickManager? _joystickManager;
    private TorqueLimiterManager? _torqueLimiter; // Only created when enabled - zero overhead
    private Settings _settings;
    private const int WM_USER_SIMCONNECT = 0x0402;

    // Cache previous values to avoid unnecessary UI updates
    private double _lastSimRate = -1;
    private double _lastGroundSpeed = -1;
    private double _lastAGL = -1;
    private double _lastGlideSlope = -999;
    private int _lastWindSpeed = -1;
    private int _lastWindAngle = -1;
    private double _lastWindArrowAngle = -999;
    // Note: _lastTorque, _lastTorquePercent, and _currentThrottlePosition removed
    // Torque display now uses vertical bars updated every frame, no caching needed

    public MainWindow()
    {
        InitializeComponent();

        // Load settings
        _settings = Settings.Load();
        ApplySettings();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void ApplySettings()
    {
        // Apply window position
        Left = _settings.WindowX;
        Top = _settings.WindowY;

        // Apply opacity
        MainBorder.Opacity = _settings.Opacity;

        // Apply display visibility
        ApplyDisplayVisibility();

        // Configure SimConnect polling for visible panels
        // Note: SimConnect might not be initialized yet at startup, but that's OK
        // UpdateDataDefinition stores the settings and will apply them when connection is established
        _simConnectManager?.UpdateDataDefinition(
            pollGroundSpeed: _settings.ShowGroundSpeed,
            pollWind: _settings.ShowWind,
            pollAGL: _settings.ShowAGL,
            pollGlideSlope: _settings.ShowGlideSlope
        );
    }

    private void ApplyDisplayVisibility()
    {
        SimRatePanel.Visibility = _settings.ShowSimRate ? Visibility.Visible : Visibility.Collapsed;
        GroundSpeedPanel.Visibility = _settings.ShowGroundSpeed ? Visibility.Visible : Visibility.Collapsed;
        AGLPanel.Visibility = _settings.ShowAGL ? Visibility.Visible : Visibility.Collapsed;
        GlideSlopePanel.Visibility = _settings.ShowGlideSlope ? Visibility.Visible : Visibility.Collapsed;
        WindPanel.Visibility = _settings.ShowWind ? Visibility.Visible : Visibility.Collapsed;
        TorquePanel.Visibility = _settings.ShowTorque && _torqueLimiter != null ? Visibility.Visible : Visibility.Collapsed;

        // Collect visible panels in order
        var visiblePanels = new List<string>();
        if (_settings.ShowSimRate) visiblePanels.Add("SimRate");
        if (_settings.ShowGroundSpeed) visiblePanels.Add("GroundSpeed");
        if (_settings.ShowAGL) visiblePanels.Add("AGL");
        if (_settings.ShowGlideSlope) visiblePanels.Add("GlideSlope");
        if (_settings.ShowWind) visiblePanels.Add("Wind");
        if (_settings.ShowTorque && _torqueLimiter != null) visiblePanels.Add("Torque");

        // No separator logic needed - each panel has consistent Margin="10,0"
        // WPF SizeToContent automatically adjusts window size
    }

    private void CreateContextMenu()
    {
        // Dispose old context menu to prevent lambda handler accumulation
        if (MainBorder.ContextMenu != null)
        {
            MainBorder.ContextMenu = null;
        }

        var contextMenu = new ContextMenu();

        // Display visibility submenu
        var displayMenuItem = new MenuItem { Header = "Display" };

        var simRateItem = new MenuItem { Header = "Sim Rate", IsCheckable = true, IsChecked = _settings.ShowSimRate };
        simRateItem.Click += (s, e) => ToggleDisplay("SimRate", simRateItem.IsChecked);
        displayMenuItem.Items.Add(simRateItem);

        var groundSpeedItem = new MenuItem { Header = "Ground Speed", IsCheckable = true, IsChecked = _settings.ShowGroundSpeed };
        groundSpeedItem.Click += (s, e) => ToggleDisplay("GroundSpeed", groundSpeedItem.IsChecked);
        displayMenuItem.Items.Add(groundSpeedItem);

        var aglItem = new MenuItem { Header = "AGL", IsCheckable = true, IsChecked = _settings.ShowAGL };
        aglItem.Click += (s, e) => ToggleDisplay("AGL", aglItem.IsChecked);
        displayMenuItem.Items.Add(aglItem);

        var glideSlopeItem = new MenuItem { Header = "Glide Slope", IsCheckable = true, IsChecked = _settings.ShowGlideSlope };
        glideSlopeItem.Click += (s, e) => ToggleDisplay("GlideSlope", glideSlopeItem.IsChecked);
        displayMenuItem.Items.Add(glideSlopeItem);

        var windItem = new MenuItem { Header = "Wind", IsCheckable = true, IsChecked = _settings.ShowWind };
        windItem.Click += (s, e) => ToggleDisplay("Wind", windItem.IsChecked);
        displayMenuItem.Items.Add(windItem);

        contextMenu.Items.Add(displayMenuItem);

        contextMenu.Items.Add(new Separator());

        // Opacity submenu
        var opacityMenuItem = new MenuItem { Header = "Opacity" };
        foreach (var opacity in new[] { 20, 40, 60, 80, 100 })
        {
            var item = new MenuItem { Header = $"{opacity}%" };
            item.Click += (s, e) => SetOpacity(opacity / 100.0);
            opacityMenuItem.Items.Add(item);
        }
        contextMenu.Items.Add(opacityMenuItem);

        // Polling rate submenu
        var pollingMenuItem = new MenuItem { Header = "Polling Rate" };
        var pollingRates = new[]
        {
            (250, "250ms (may impact performance)"),
            (500, "500ms (fast)"),
            (750, "750ms"),
            (1000, "1000ms"),
            (2000, "2000ms (slow)")
        };

        foreach (var (ms, label) in pollingRates)
        {
            var item = new MenuItem { Header = label };
            if (_settings.PollingRateMs == ms)
                item.IsChecked = true;
            item.Click += (s, e) => SetPollingRate(ms);
            pollingMenuItem.Items.Add(item);
        }
        contextMenu.Items.Add(pollingMenuItem);

        contextMenu.Items.Add(new Separator());

        // Joystick device selection
        var deviceMenuItem = new MenuItem { Header = "Joystick Device" };
        var devices = _joystickManager?.GetAvailableDevices() ?? new List<string>();

        if (devices.Count > 0)
        {
            for (int i = 0; i < devices.Count; i++)
            {
                var deviceIndex = i; // Capture for lambda
                var item = new MenuItem { Header = devices[i] };

                // Mark selected device with checkmark
                if (_settings.JoystickDeviceIndex == i)
                {
                    item.IsChecked = true;
                }

                item.Click += (s, e) => SelectJoystickDevice(deviceIndex);
                deviceMenuItem.Items.Add(item);
            }
        }
        else
        {
            var noDeviceItem = new MenuItem { Header = "No devices found", IsEnabled = false };
            deviceMenuItem.Items.Add(noDeviceItem);
        }
        contextMenu.Items.Add(deviceMenuItem);

        // Joystick button configuration
        var joystickMenuItem = new MenuItem();
        UpdateJoystickMenuText(joystickMenuItem);
        joystickMenuItem.Click += JoystickMenuItem_Click;
        contextMenu.Items.Add(joystickMenuItem);

        contextMenu.Items.Add(new Separator());

        // Torque Limiter submenu
        var torqueMenuItem = new MenuItem { Header = "Torque Limiter" };

        var enableTorqueItem = new MenuItem { Header = "Enable Torque Limiter", IsCheckable = true, IsChecked = _settings.TorqueLimiterEnabled };
        enableTorqueItem.Click += (s, e) =>
        {
            _settings.TorqueLimiterEnabled = enableTorqueItem.IsChecked;
            if (!_settings.Save())
            {
                Logger.WriteLine("[MainWindow] Warning: Failed to save torque limiter enabled setting");
            }

            if (enableTorqueItem.IsChecked)
            {
                EnableTorqueLimiter();
            }
            else
            {
                DisableTorqueLimiter();
            }

            // Recreate menu to update options
            CreateContextMenu();
        };
        torqueMenuItem.Items.Add(enableTorqueItem);

        // Only show these options if torque limiter is enabled
        if (_settings.TorqueLimiterEnabled)
        {
            var showTorqueItem = new MenuItem { Header = "Show Torque Display", IsCheckable = true, IsChecked = _settings.ShowTorque };
            showTorqueItem.Click += (s, e) => ToggleDisplay("Torque", showTorqueItem.IsChecked);
            torqueMenuItem.Items.Add(showTorqueItem);

            torqueMenuItem.Items.Add(new Separator());

            var configItem = new MenuItem { Header = $"Max Torque: {_settings.MaxTorquePercent:F0}% (click to adjust)" };
            configItem.Click += (s, e) =>
            {
                // Simple prompt for now - could create a dialog later
                var result = Microsoft.VisualBasic.Interaction.InputBox(
                    $"Enter maximum torque limit as percentage of aircraft's rated max:\n\nCurrent: {_settings.MaxTorquePercent:F0}%\n\nCommon values:\n• 100% = Rated maximum (redline)\n• 95% = Conservative limit\n• 90% = Safe continuous operation\n• 105% = Allow brief excursions above redline",
                    "Configure Max Torque %",
                    _settings.MaxTorquePercent.ToString()
                );

                if (double.TryParse(result, out double newMax) && newMax > 0 && newMax <= 120)
                {
                    _settings.MaxTorquePercent = newMax;
                    if (!_settings.Save())
                    {
                        Logger.WriteLine("[MainWindow] Warning: Failed to save max torque setting");
                    }
                    Logger.WriteLine($"[MainWindow] Max torque set to {newMax:F0}%");
                    CreateContextMenu();
                }
            };
            torqueMenuItem.Items.Add(configItem);

            // Warning Threshold
            var warningItem = new MenuItem { Header = $"Warning Threshold: {(_settings.TorqueWarningThreshold * 100):F0}% (click to adjust)" };
            warningItem.Click += (s, e) =>
            {
                var result = Microsoft.VisualBasic.Interaction.InputBox(
                    $"Enter warning threshold (yellow color) as percentage:\n\nCurrent: {(_settings.TorqueWarningThreshold * 100):F0}%\n\nThis is when bars turn yellow before hitting the red limit.\n\nTypical: 90-95%",
                    "Warning Threshold %",
                    (_settings.TorqueWarningThreshold * 100).ToString()
                );

                if (double.TryParse(result, out double newThreshold) && newThreshold > 0 && newThreshold <= 100)
                {
                    _settings.TorqueWarningThreshold = newThreshold / 100.0;
                    if (!_settings.Save())
                    {
                        Logger.WriteLine("[MainWindow] Warning: Failed to save warning threshold setting");
                    }
                    Logger.WriteLine($"[MainWindow] Warning threshold set to {newThreshold:F0}%");
                    CreateContextMenu();
                }
            };
            torqueMenuItem.Items.Add(warningItem);

            torqueMenuItem.Items.Add(new Separator());

            // Throttle Reduction Aggression
            var aggressionItem = new MenuItem { Header = $"Reduction Aggression: {_settings.ThrottleReductionAggression:F1}x (click to adjust)" };
            aggressionItem.Click += (s, e) =>
            {
                var result = Microsoft.VisualBasic.Interaction.InputBox(
                    $"Enter throttle reduction aggression multiplier:\n\nCurrent: {_settings.ThrottleReductionAggression:F1}x\n\nHow this works:\n• 8% overtorque × 2.5x = 20% throttle reduction\n• Higher = more aggressive cuts\n• Lower = gentler reductions\n\nTypical range: 1.5 - 4.0",
                    "Reduction Aggression",
                    _settings.ThrottleReductionAggression.ToString()
                );

                if (double.TryParse(result, out double newAggression) && newAggression > 0 && newAggression <= 10)
                {
                    _settings.ThrottleReductionAggression = newAggression;
                    if (!_settings.Save())
                    {
                        Logger.WriteLine("[MainWindow] Warning: Failed to save aggression setting");
                    }
                    Logger.WriteLine($"[MainWindow] Throttle reduction aggression set to {newAggression:F1}x");
                    CreateContextMenu();
                }
            };
            torqueMenuItem.Items.Add(aggressionItem);

            // Minimum Throttle Floor
            var minThrottleItem = new MenuItem { Header = $"Minimum Throttle: {_settings.MinThrottlePercent:F0}% (click to adjust)" };
            minThrottleItem.Click += (s, e) =>
            {
                var result = Microsoft.VisualBasic.Interaction.InputBox(
                    $"Enter minimum throttle floor (safety limit):\n\nCurrent: {_settings.MinThrottlePercent:F0}%\n\nThe system will never reduce throttle below this value to prevent stalling.\n\nTypical: 30-50%",
                    "Minimum Throttle %",
                    _settings.MinThrottlePercent.ToString()
                );

                if (double.TryParse(result, out double newMin) && newMin >= 0 && newMin <= 80)
                {
                    _settings.MinThrottlePercent = newMin;
                    if (!_settings.Save())
                    {
                        Logger.WriteLine("[MainWindow] Warning: Failed to save min throttle setting");
                    }
                    Logger.WriteLine($"[MainWindow] Minimum throttle set to {newMin:F0}%");
                    CreateContextMenu();
                }
            };
            torqueMenuItem.Items.Add(minThrottleItem);

            // Intervention Cooldown
            var cooldownItem = new MenuItem { Header = $"Intervention Cooldown: {_settings.InterventionCooldownMs}ms (click to adjust)" };
            cooldownItem.Click += (s, e) =>
            {
                var result = Microsoft.VisualBasic.Interaction.InputBox(
                    $"Enter time between throttle interventions (milliseconds):\n\nCurrent: {_settings.InterventionCooldownMs}ms\n\nThis allows engine/prop to stabilize between corrections.\n\nRecommended:\n• 1000ms (1 sec) = fast response\n• 2000ms (2 sec) = balanced\n• 3000ms (3 sec) = conservative",
                    "Intervention Cooldown (ms)",
                    _settings.InterventionCooldownMs.ToString()
                );

                if (int.TryParse(result, out int newCooldown) && newCooldown >= 500 && newCooldown <= 10000)
                {
                    _settings.InterventionCooldownMs = newCooldown;
                    if (!_settings.Save())
                    {
                        Logger.WriteLine("[MainWindow] Warning: Failed to save cooldown setting");
                    }
                    Logger.WriteLine($"[MainWindow] Intervention cooldown set to {newCooldown}ms");
                    CreateContextMenu();
                }
            };
            torqueMenuItem.Items.Add(cooldownItem);

            torqueMenuItem.Items.Add(new Separator());

            var interventionInfo = new MenuItem { Header = $"Interventions: {_torqueLimiter?.GetInterventionCount() ?? 0}", IsEnabled = false };
            torqueMenuItem.Items.Add(interventionInfo);
        }

        contextMenu.Items.Add(torqueMenuItem);

        contextMenu.Items.Add(new Separator());

        // About
        var aboutMenuItem = new MenuItem { Header = "About" };
        aboutMenuItem.Click += AboutMenuItem_Click;
        contextMenu.Items.Add(aboutMenuItem);

        // Exit
        var exitMenuItem = new MenuItem { Header = "Exit" };
        exitMenuItem.Click += (s, e) => Close();
        contextMenu.Items.Add(exitMenuItem);

        MainBorder.ContextMenu = contextMenu;
    }

    private void SelectJoystickDevice(int deviceIndex)
    {
        _settings.JoystickDeviceIndex = deviceIndex;

        // Clear button mapping when changing devices (button numbers may differ)
        if (_settings.JoystickButton.HasValue)
        {
            Logger.WriteLine($"[MainWindow] Clearing button mapping due to device change");
            _settings.JoystickButton = null;
            _joystickManager?.ClearTriggerButton();
        }

        if (!_settings.Save())
        {
            Logger.WriteLine("[MainWindow] Warning: Failed to save joystick device selection");
        }

        _joystickManager?.SelectDevice(deviceIndex);

        Logger.WriteLine($"[MainWindow] Selected joystick device {deviceIndex}");

        // Recreate context menu to update checkmarks and enable button configuration
        CreateContextMenu();
    }

    private void UpdateJoystickMenuText(MenuItem menuItem)
    {
        var buttonNum = _settings.JoystickButton;
        var deviceIndex = _settings.JoystickDeviceIndex;

        if (!deviceIndex.HasValue)
        {
            menuItem.Header = "Select Joystick Device First";
            menuItem.IsEnabled = false;
        }
        else if (buttonNum.HasValue)
        {
            menuItem.Header = $"Joystick Button: {buttonNum.Value} (click to change)";
            menuItem.IsEnabled = true;
        }
        else
        {
            menuItem.Header = "Set Joystick Button (click to configure)";
            menuItem.IsEnabled = true;
        }
    }

    private void JoystickMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = (MenuItem)sender;

        if (_settings.JoystickButton.HasValue)
        {
            // Already configured - show submenu to change or clear
            var contextMenu = new ContextMenu();

            var changeItem = new MenuItem { Header = "Change Button" };
            changeItem.Click += (s, args) => StartButtonCapture(menuItem);
            contextMenu.Items.Add(changeItem);

            var clearItem = new MenuItem { Header = "Clear Button" };
            clearItem.Click += (s, args) =>
            {
                _settings.JoystickButton = null;
                if (!_settings.Save())
                {
                    Logger.WriteLine("[MainWindow] Warning: Failed to save cleared button setting");
                }
                _joystickManager?.ClearTriggerButton();
                // Recreate menu to update button display
                CreateContextMenu();
            };
            contextMenu.Items.Add(clearItem);

            contextMenu.IsOpen = true;
            contextMenu.PlacementTarget = MainBorder;
            contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        }
        else
        {
            // Not configured yet - start capture
            StartButtonCapture(menuItem);
        }
    }

    private void StartButtonCapture(MenuItem menuItem)
    {
        if (_joystickManager == null) return;

        // Show capture overlay
        CaptureOverlay.Visibility = Visibility.Visible;
        CaptureOverlayText.Text = "Press any joystick button...";
        CaptureOverlayText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 179, 0)); // Amber
        Logger.WriteLine("[MainWindow] Started button capture mode");

        // Start capture mode
        _joystickManager.StartCaptureMode();

        // Subscribe to capture event (one-time)
        EventHandler<int>? captureHandler = null;
        DispatcherTimer? timeoutTimer = null;

        captureHandler = (sender, buttonIndex) =>
        {
            Logger.WriteLine($"[MainWindow] Captured button {buttonIndex}");

            // Stop timeout timer
            timeoutTimer?.Stop();

            _settings.JoystickButton = buttonIndex;
            if (!_settings.Save())
            {
                Logger.WriteLine("[MainWindow] Warning: Failed to save button capture result");
            }
            _joystickManager?.SetTriggerButton(buttonIndex);

            // Show success feedback
            CaptureOverlayText.Text = $"✓ Button {buttonIndex} captured!";
            CaptureOverlayText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 255, 136)); // Green

            // Wait 1 second before hiding overlay
            var hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            hideTimer.Tick += (s, e) =>
            {
                hideTimer.Stop();
                CaptureOverlay.Visibility = Visibility.Collapsed;
                // Recreate menu to update button display
                CreateContextMenu();
            };
            hideTimer.Start();

            // Unsubscribe after capture
            if (_joystickManager != null)
            {
                _joystickManager.ButtonCaptured -= captureHandler;
            }
        };

        _joystickManager.ButtonCaptured += captureHandler;

        // Add a timeout to exit capture mode if no button pressed within 10 seconds
        timeoutTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        timeoutTimer.Tick += (s, e) =>
        {
            timeoutTimer.Stop();

            if (_joystickManager != null)
            {
                _joystickManager.StopCaptureMode();
                _joystickManager.ButtonCaptured -= captureHandler;
            }

            CaptureOverlay.Visibility = Visibility.Collapsed;
            Logger.WriteLine("[MainWindow] Button capture timed out");
        };
        timeoutTimer.Start();
    }

    private void SetOpacity(double opacity)
    {
        MainBorder.Opacity = opacity;
        _settings.Opacity = opacity;
        if (!_settings.Save())
        {
            Logger.WriteLine("[MainWindow] Warning: Failed to save opacity setting");
        }
    }

    private void SetPollingRate(int milliseconds)
    {
        _settings.PollingRateMs = milliseconds;
        if (!_settings.Save())
        {
            Logger.WriteLine("[MainWindow] Warning: Failed to save polling rate setting");
        }
        _simConnectManager?.SetPollingRate(milliseconds);

        // Recreate context menu to update checkmark
        CreateContextMenu();
    }

    private void ToggleDisplay(string displayName, bool isVisible)
    {
        switch (displayName)
        {
            case "SimRate":
                _settings.ShowSimRate = isVisible;
                break;
            case "GroundSpeed":
                _settings.ShowGroundSpeed = isVisible;
                break;
            case "AGL":
                _settings.ShowAGL = isVisible;
                break;
            case "GlideSlope":
                _settings.ShowGlideSlope = isVisible;
                break;
            case "Wind":
                _settings.ShowWind = isVisible;
                break;
            case "Torque":
                _settings.ShowTorque = isVisible;
                break;
        }
        ApplyDisplayVisibility();
        if (!_settings.Save())
        {
            Logger.WriteLine("[MainWindow] Warning: Failed to save display visibility setting");
        }

        // Update SimConnect polling to only request data for visible panels
        _simConnectManager?.UpdateDataDefinition(
            pollGroundSpeed: _settings.ShowGroundSpeed,
            pollWind: _settings.ShowWind,
            pollAGL: _settings.ShowAGL,
            pollGlideSlope: _settings.ShowGlideSlope
        );
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Get the window handle
        var helper = new WindowInteropHelper(this);
        var handle = helper.Handle;

        // Add hook for SimConnect messages
        HwndSource source = HwndSource.FromHwnd(handle);
        source.AddHook(HandleSimConnectMessage);

        // Initialize SimConnect manager with polling rate
        _simConnectManager = new SimConnectManager(handle, _settings.PollingRateMs);
        _simConnectManager.DataUpdated += SimConnectManager_DataUpdated;
        _simConnectManager.ConnectionStatusChanged += SimConnectManager_ConnectionStatusChanged;
        _simConnectManager.TorqueDataUpdated += SimConnectManager_TorqueDataUpdated;

        // Initialize torque limiter if enabled (lazy initialization for zero overhead)
        if (_settings.TorqueLimiterEnabled)
        {
            EnableTorqueLimiter();
        }

        // Initialize Joystick manager
        _joystickManager = new JoystickManager();
        _joystickManager.ButtonPressed += JoystickManager_ButtonPressed;
        _joystickManager.DeviceError += JoystickManager_DeviceError;

        // Restore saved device selection
        if (_settings.JoystickDeviceIndex.HasValue)
        {
            _joystickManager.SelectDevice(_settings.JoystickDeviceIndex.Value);
        }

        // Apply saved joystick button configuration
        if (_settings.JoystickButton.HasValue)
        {
            _joystickManager.SetTriggerButton(_settings.JoystickButton.Value);
        }

        // Create context menu AFTER joystick manager is initialized
        CreateContextMenu();
    }

    private void JoystickManager_DeviceError(object? sender, string errorMessage)
    {
        Dispatcher.Invoke(() =>
        {
            Logger.ShowErrorWithLog(
                $"Joystick device error:\n\n{errorMessage}\n\nThe application cannot read from the configured joystick device.",
                "Joystick Error"
            );
        });
    }

    private void JoystickManager_ButtonPressed(object? sender, EventArgs e)
    {
        // Trigger 1x reset when configured button is pressed
        Dispatcher.Invoke(() =>
        {
            _simConnectManager?.SetSimulationRate(1.0);
        });
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Save window position
        _settings.WindowX = Left;
        _settings.WindowY = Top;
        if (!_settings.Save())
        {
            Logger.WriteLine("[MainWindow] Warning: Failed to save window position on exit");
        }

        // Unsubscribe all event handlers BEFORE disposing to prevent memory leaks
        if (_simConnectManager != null)
        {
            _simConnectManager.DataUpdated -= SimConnectManager_DataUpdated;
            _simConnectManager.ConnectionStatusChanged -= SimConnectManager_ConnectionStatusChanged;
            _simConnectManager.Shutdown();
        }

        if (_joystickManager != null)
        {
            _joystickManager.ButtonPressed -= JoystickManager_ButtonPressed;
            _joystickManager.Dispose();
        }

        // Remove window message hook to prevent memory leak
        var helper = new WindowInteropHelper(this);
        if (helper.Handle != IntPtr.Zero)
        {
            var source = HwndSource.FromHwnd(helper.Handle);
            if (source != null)
            {
                source.RemoveHook(HandleSimConnectMessage);
            }
        }
    }

    private IntPtr HandleSimConnectMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_USER_SIMCONNECT)
        {
            _simConnectManager?.ReceiveMessage();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void SimConnectManager_DataUpdated(object? sender, SimConnectManager.SimData data)
    {
        // Pre-calculate values on background thread to avoid UI thread work
        double simRate = Math.Round(data.SimulationRate, 2);
        double groundSpeed = Math.Round(data.GroundSpeed, 0);
        double agl = Math.Round(data.AltitudeAboveGround, 0);

        // Calculate glide slope angle
        double glideSlopeAngle = 0;
        if (data.GroundSpeed > 1)
        {
            double groundSpeedFtPerMin = data.GroundSpeed * 101.269;
            glideSlopeAngle = Math.Round(Math.Atan2(data.VerticalSpeed, groundSpeedFtPerMin) * (180 / Math.PI), 1);
        }

        // Calculate relative wind direction
        double relativeWindAngle = data.WindDirection - data.PlaneHeading;
        while (relativeWindAngle > 180) relativeWindAngle -= 360;
        while (relativeWindAngle < -180) relativeWindAngle += 360;

        int windSpeed = (int)Math.Round(data.WindSpeed);
        int windAngle = (int)Math.Round(Math.Abs(relativeWindAngle));

        // Only update UI if values have changed (use BeginInvoke for non-blocking)
        if (simRate != _lastSimRate || groundSpeed != _lastGroundSpeed || agl != _lastAGL ||
            glideSlopeAngle != _lastGlideSlope || windSpeed != _lastWindSpeed ||
            windAngle != _lastWindAngle || relativeWindAngle != _lastWindArrowAngle)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (simRate != _lastSimRate)
                {
                    SimRateTextBlock.Text = $"{simRate:F2}x";
                    _lastSimRate = simRate;
                }

                if (groundSpeed != _lastGroundSpeed)
                {
                    GroundSpeedTextBlock.Text = $"{groundSpeed:F0} kts";
                    _lastGroundSpeed = groundSpeed;
                }

                if (agl != _lastAGL)
                {
                    AGLTextBlock.Text = $"{agl:F0} ft";
                    _lastAGL = agl;
                }

                if (glideSlopeAngle != _lastGlideSlope)
                {
                    GlideSlopeTextBlock.Text = $"{glideSlopeAngle:F1}°";
                    _lastGlideSlope = glideSlopeAngle;
                }

                if (windSpeed != _lastWindSpeed)
                {
                    WindSpeedTextBlock.Text = $"{windSpeed:D2} kts";
                    _lastWindSpeed = windSpeed;
                }

                if (windAngle != _lastWindAngle)
                {
                    WindAngleTextBlock.Text = $"{windAngle:D3}°";
                    _lastWindAngle = windAngle;
                }

                if (relativeWindAngle != _lastWindArrowAngle)
                {
                    WindArrowRotation.Angle = relativeWindAngle;
                    _lastWindArrowAngle = relativeWindAngle;
                }
            });
        }
    }

    private void SimConnectManager_ConnectionStatusChanged(object? sender, bool isConnected)
    {
        Dispatcher.Invoke(() =>
        {
            if (!isConnected)
            {
                SimRateTextBlock.Text = "--";
                GroundSpeedTextBlock.Text = "-- kts";
                AGLTextBlock.Text = "-- ft";
                GlideSlopeTextBlock.Text = "--°";
                WindSpeedTextBlock.Text = "-- kts";
                WindAngleTextBlock.Text = "--°";

                // Reset cached values so first update after reconnect will show
                _lastSimRate = -1;
                _lastGroundSpeed = -1;
                _lastAGL = -1;
                _lastGlideSlope = -999;
                _lastWindSpeed = -1;
                _lastWindAngle = -1;
                _lastWindArrowAngle = -999;
            }
        });
    }

    private void SimConnectManager_TorqueDataUpdated(object? sender, SimConnectManager.TorqueData data)
    {
        // Process through torque limiter (if enabled)
        _torqueLimiter?.ProcessTorqueData(data.Engines);

        // Update vertical bar gauges for each engine
        Dispatcher.BeginInvoke(() =>
        {
            UpdateTorqueBars(data.Engines);
        });
    }

    private void UpdateTorqueBars(SimConnectManager.EngineData[] engines)
    {
        // Ensure we have the right number of bar elements
        while (TorqueBarsContainer.Children.Count < engines.Length)
        {
            var bar = CreateTorqueBar();
            TorqueBarsContainer.Children.Add(bar);
        }

        // Hide extra bars if aircraft has fewer engines
        for (int i = 0; i < TorqueBarsContainer.Children.Count; i++)
        {
            if (i < engines.Length)
            {
                TorqueBarsContainer.Children[i].Visibility = Visibility.Visible;
                UpdateTorqueBar((Border)TorqueBarsContainer.Children[i], engines[i].TorquePercent);
            }
            else
            {
                TorqueBarsContainer.Children[i].Visibility = Visibility.Collapsed;
            }
        }
    }

    private Border CreateTorqueBar()
    {
        var container = new Border
        {
            Width = 10,
            Height = 40,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 255, 255)),
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(2, 0, 2, 0)
        };

        var fillBar = new Border
        {
            Name = "FillBar",
            Width = 10,
            VerticalAlignment = VerticalAlignment.Bottom,
            CornerRadius = new CornerRadius(2),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 255, 136)) // Green
        };

        container.Child = fillBar;
        return container;
    }

    private void UpdateTorqueBar(Border container, double torquePercent)
    {
        var fillBar = (Border)container.Child;

        // Update height (0-100% of container height)
        double heightPercent = Math.Clamp(torquePercent / 100.0, 0.0, 1.0);
        fillBar.Height = 40 * heightPercent;

        // Update color based on thresholds
        System.Windows.Media.Color color;
        if (torquePercent >= _settings.MaxTorquePercent)
        {
            color = System.Windows.Media.Color.FromRgb(255, 0, 0); // Red - over limit
        }
        else if (torquePercent >= _settings.TorqueWarningThreshold * 100)
        {
            color = System.Windows.Media.Color.FromRgb(255, 200, 0); // Yellow - warning
        }
        else
        {
            color = System.Windows.Media.Color.FromRgb(0, 255, 136); // Green - normal
        }

        fillBar.Background = new System.Windows.Media.SolidColorBrush(color);
    }

    private void TorqueLimiter_LimitTriggered(object? sender, TorqueLimiterManager.TorqueLimitEvent e)
    {
        // Log details (already logged in TorqueLimiterManager, but keep for MainWindow perspective)
        Logger.WriteLine($"[MainWindow] Torque limiter intervention #{e.InterventionCount} - adjusting {e.OverlimitEngines.Length} engine(s)");

        // Set throttles to calculated target positions (all engines at once)
        _simConnectManager?.SetThrottlesAbsolute(e.RecommendedThrottlePercents);

        // Audible alert - claxon effect with discordant tones
        PlayClaxonAlert();
    }

    private void PlayClaxonAlert()
    {
        // Play a claxon-style alert with two simultaneous discordant tones
        Task.Run(() =>
        {
            try
            {
                // Generate audio data for two simultaneous tones
                int sampleRate = 8000; // 8kHz sample rate
                int duration = 150; // 150ms
                int samples = (sampleRate * duration) / 1000;

                byte[] waveData = GenerateClaxonWaveform(sampleRate, samples, 600, 800);

                // Play using SoundPlayer
                using (var ms = new System.IO.MemoryStream())
                {
                    WriteWavHeader(ms, sampleRate, 1, samples);
                    ms.Write(waveData, 0, waveData.Length);
                    ms.Position = 0;

                    using (var player = new System.Media.SoundPlayer(ms))
                    {
                        player.PlaySync();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"[MainWindow] Warning: Failed to play claxon alert: {ex.Message}");
            }
        });
    }

    private byte[] GenerateClaxonWaveform(int sampleRate, int samples, int freq1, int freq2)
    {
        // Generate two simultaneous sine waves at different frequencies (creates dissonance)
        byte[] data = new byte[samples * 2]; // 16-bit samples
        double amplitude = 0.3; // 30% volume to prevent clipping when mixed

        for (int i = 0; i < samples; i++)
        {
            // Generate two sine waves and mix them
            double t = (double)i / sampleRate;
            double wave1 = Math.Sin(2 * Math.PI * freq1 * t);
            double wave2 = Math.Sin(2 * Math.PI * freq2 * t);

            // Mix the two waveforms
            double mixed = (wave1 + wave2) * amplitude;

            // Convert to 16-bit PCM
            short sample = (short)(mixed * short.MaxValue);

            // Write as little-endian 16-bit
            data[i * 2] = (byte)(sample & 0xFF);
            data[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return data;
    }

    private void WriteWavHeader(System.IO.Stream stream, int sampleRate, int channels, int samples)
    {
        // WAV file header for PCM audio
        int byteRate = sampleRate * channels * 2; // 16-bit = 2 bytes per sample
        int dataSize = samples * channels * 2;

        // RIFF header
        stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"), 0, 4);
        stream.Write(BitConverter.GetBytes(36 + dataSize), 0, 4);
        stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"), 0, 4);

        // fmt chunk
        stream.Write(System.Text.Encoding.ASCII.GetBytes("fmt "), 0, 4);
        stream.Write(BitConverter.GetBytes(16), 0, 4); // Chunk size
        stream.Write(BitConverter.GetBytes((short)1), 0, 2); // Audio format (1 = PCM)
        stream.Write(BitConverter.GetBytes((short)channels), 0, 2);
        stream.Write(BitConverter.GetBytes(sampleRate), 0, 4);
        stream.Write(BitConverter.GetBytes(byteRate), 0, 4);
        stream.Write(BitConverter.GetBytes((short)(channels * 2)), 0, 2); // Block align
        stream.Write(BitConverter.GetBytes((short)16), 0, 2); // Bits per sample

        // data chunk
        stream.Write(System.Text.Encoding.ASCII.GetBytes("data"), 0, 4);
        stream.Write(BitConverter.GetBytes(dataSize), 0, 4);
    }

    private void EnableTorqueLimiter()
    {
        if (_torqueLimiter != null)
            return; // Already enabled

        Logger.WriteLine("[MainWindow] Enabling torque limiter");

        // Create torque limiter manager
        _torqueLimiter = new TorqueLimiterManager(_settings);
        _torqueLimiter.LimitTriggered += TorqueLimiter_LimitTriggered;

        // Enable torque monitoring in SimConnect
        _simConnectManager?.EnableTorqueMonitoring();

        // Show torque panel if user wants it visible
        if (_settings.ShowTorque)
        {
            TorquePanel.Visibility = Visibility.Visible;
        }
    }

    private void DisableTorqueLimiter()
    {
        if (_torqueLimiter == null)
            return; // Already disabled

        Logger.WriteLine("[MainWindow] Disabling torque limiter");

        // Unsubscribe and dispose
        _torqueLimiter.LimitTriggered -= TorqueLimiter_LimitTriggered;
        _torqueLimiter.Dispose();
        _torqueLimiter = null;

        // Disable torque monitoring in SimConnect (zero overhead)
        _simConnectManager?.DisableTorqueMonitoring();

        // Hide torque panel
        TorquePanel.Visibility = Visibility.Collapsed;
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            DragMove();
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _simConnectManager?.SetSimulationRate(1.0);
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow
        {
            Owner = this
        };
        aboutWindow.ShowDialog();
    }
}