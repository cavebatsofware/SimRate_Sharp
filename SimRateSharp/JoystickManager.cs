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
 
using System;
using System.Linq;
using System.Windows.Threading;
using SharpDX.DirectInput;

namespace SimRateSharp;

public class JoystickManager : IDisposable
{
    private DirectInput? _directInput;
    private Joystick? _joystick;
    private readonly DispatcherTimer _pollTimer;
    private bool[] _previousButtonStates = Array.Empty<bool>();
    private int? _configuredButton;
    private bool _captureMode = false;
    private int? _capturedButton = null;
    private List<DeviceInstance> _availableDevices = new List<DeviceInstance>();
    private Guid? _selectedDeviceGuid = null;

    public event EventHandler? ButtonPressed;
    public event EventHandler<int>? ButtonCaptured;

    public JoystickManager()
    {
        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250) // Poll 4 times per second (reduced from 50ms to avoid USB contention)
        };
        _pollTimer.Tick += PollTimer_Tick;

        InitializeDirectInput();
    }

    private void InitializeDirectInput()
    {
        try
        {
            Logger.WriteLine("[JoystickManager] Initializing DirectInput...");
            _directInput = new DirectInput();

            // Enumerate and store all joystick/gamepad devices
            _availableDevices.Clear();

            var joysticks = _directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AllDevices);
            var gamepads = _directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices);

            _availableDevices.AddRange(joysticks);
            _availableDevices.AddRange(gamepads);

            Logger.WriteLine($"[JoystickManager] Found {_availableDevices.Count} input devices:");
            for (int i = 0; i < _availableDevices.Count; i++)
            {
                Logger.WriteLine($"[JoystickManager]   [{i}] {_availableDevices[i].ProductName}");
            }
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"[JoystickManager] Failed to initialize DirectInput: {ex.Message}");
        }
    }

    public List<string> GetAvailableDevices()
    {
        return _availableDevices.Select(d => d.ProductName).ToList();
    }

    public void SelectDevice(int deviceIndex)
    {
        if (deviceIndex < 0 || deviceIndex >= _availableDevices.Count)
        {
            Logger.WriteLine($"[JoystickManager] Invalid device index: {deviceIndex}");
            return;
        }

        _selectedDeviceGuid = _availableDevices[deviceIndex].InstanceGuid;
        Logger.WriteLine($"[JoystickManager] Selected device: {_availableDevices[deviceIndex].ProductName}");

        InitializeJoystick();
    }

    public int? GetSelectedDeviceIndex()
    {
        if (_selectedDeviceGuid == null) return null;

        for (int i = 0; i < _availableDevices.Count; i++)
        {
            if (_availableDevices[i].InstanceGuid == _selectedDeviceGuid)
                return i;
        }
        return null;
    }

    private void InitializeJoystick()
    {
        try
        {
            // Stop polling if already running
            _pollTimer.Stop();

            // Release old joystick if exists
            if (_joystick != null)
            {
                _joystick.Unacquire();
                _joystick.Dispose();
                _joystick = null;
            }

            if (_selectedDeviceGuid == null)
            {
                Logger.WriteLine("[JoystickManager] No device selected - please select a device from the menu");
                return;
            }

            if (_directInput == null)
            {
                Logger.WriteLine("[JoystickManager] DirectInput not initialized");
                return;
            }

            // Instantiate the joystick
            Logger.WriteLine("[JoystickManager] Creating joystick instance...");
            _joystick = new Joystick(_directInput, _selectedDeviceGuid.Value);

            Logger.WriteLine("[JoystickManager] Acquiring joystick...");
            _joystick.Acquire();

            Logger.WriteLine($"[JoystickManager] Joystick acquired successfully with {_joystick.Capabilities.ButtonCount} buttons");

            // Reset button states
            _previousButtonStates = Array.Empty<bool>();

            // Start polling
            _pollTimer.Start();
            Logger.WriteLine("[JoystickManager] Started polling timer");
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"[JoystickManager] Failed to initialize joystick: {ex.Message}");
            Logger.WriteLine($"[JoystickManager] Exception type: {ex.GetType().Name}");
            Logger.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    public void SetTriggerButton(int buttonIndex)
    {
        _configuredButton = buttonIndex;
        _captureMode = false;
        Logger.WriteLine($"[JoystickManager] Configured button {buttonIndex} as reset trigger");
    }

    public void ClearTriggerButton()
    {
        _configuredButton = null;
        Logger.WriteLine("[JoystickManager] Cleared trigger button configuration");
    }

    public void StartCaptureMode()
    {
        _captureMode = true;
        _capturedButton = null;
        Logger.WriteLine("[JoystickManager] Started button capture mode");
    }

    public void StopCaptureMode()
    {
        _captureMode = false;
        Logger.WriteLine("[JoystickManager] Stopped button capture mode");
    }

    public int? GetConfiguredButton() => _configuredButton;

    private void PollTimer_Tick(object? sender, EventArgs e)
    {
        if (_joystick == null) return;

        try
        {
            _joystick.Poll();
            var state = _joystick.GetCurrentState();
            var buttons = state.Buttons;

            // Initialize button states on first poll
            if (_previousButtonStates.Length != buttons.Length)
            {
                _previousButtonStates = new bool[buttons.Length];
            }

            // Check each button for press (transition from not pressed to pressed)
            for (int i = 0; i < buttons.Length; i++)
            {
                bool isPressed = buttons[i];
                bool wasPressed = _previousButtonStates[i];

                // Detect button press (rising edge)
                if (isPressed && !wasPressed)
                {
                    Logger.WriteLine($"[JoystickManager] Button {i} pressed");

                    // If in capture mode, capture this button and exit capture mode
                    if (_captureMode)
                    {
                        _capturedButton = i;
                        _captureMode = false;
                        ButtonCaptured?.Invoke(this, i);
                        Logger.WriteLine($"[JoystickManager] Captured button {i}");
                    }
                    // If this is the configured button, raise the event
                    else if (_configuredButton.HasValue && _configuredButton.Value == i)
                    {
                        ButtonPressed?.Invoke(this, EventArgs.Empty);
                    }
                }

                _previousButtonStates[i] = isPressed;
            }
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"[JoystickManager] Error polling joystick: {ex.Message}");
            // Try to reacquire
            try
            {
                _joystick?.Acquire();
            }
            catch { }
        }
    }

    public void Dispose()
    {
        _pollTimer.Stop();
        _joystick?.Unacquire();
        _joystick?.Dispose();
        _directInput?.Dispose();
    }
}
