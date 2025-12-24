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
using System.Windows.Threading;

namespace SimRateSharp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private SimConnectManager? _simConnectManager;
    private JoystickManager? _joystickManager;
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

        // Collect visible panels in order
        var visiblePanels = new List<string>();
        if (_settings.ShowSimRate) visiblePanels.Add("SimRate");
        if (_settings.ShowGroundSpeed) visiblePanels.Add("GroundSpeed");
        if (_settings.ShowAGL) visiblePanels.Add("AGL");
        if (_settings.ShowGlideSlope) visiblePanels.Add("GlideSlope");
        if (_settings.ShowWind) visiblePanels.Add("Wind");

        // Hide all separators initially
        Separator1.Visibility = Visibility.Collapsed;
        Separator2.Visibility = Visibility.Collapsed;
        Separator3.Visibility = Visibility.Collapsed;
        Separator4.Visibility = Visibility.Collapsed;
        Separator5.Visibility = Visibility.Collapsed;

        // Show separators between consecutive visible panels
        if (visiblePanels.Count >= 2)
        {
            for (int i = 0; i < visiblePanels.Count - 1; i++)
            {
                // Show separator between panel[i] and panel[i+1]
                if (i == 0 && visiblePanels.Count > 1)
                    Separator1.Visibility = Visibility.Visible;
                if (i == 1 && visiblePanels.Count > 2)
                    Separator2.Visibility = Visibility.Visible;
                if (i == 2 && visiblePanels.Count > 3)
                    Separator3.Visibility = Visibility.Visible;
                if (i == 3 && visiblePanels.Count > 4)
                    Separator4.Visibility = Visibility.Visible;
            }
        }

        // Separator5: before button (only if at least one panel is visible)
        if (visiblePanels.Count > 0)
            Separator5.Visibility = Visibility.Visible;

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

        // Initialize Joystick manager
        _joystickManager = new JoystickManager();
        _joystickManager.ButtonPressed += JoystickManager_ButtonPressed;

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