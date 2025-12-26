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
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Microsoft.FlightSimulator.SimConnect;

namespace SimRateSharp;

public class SimConnectManager
{
    private SimConnect? _simConnect;
    private readonly DispatcherTimer _reconnectTimer;
    private readonly DispatcherTimer _pollTimer;
    private IntPtr _handle;
    private bool _isConnected;
    private double _currentSimRate = 1.0;

    // Track which data types to poll (optimization)
    private bool _pollGroundSpeed = true;
    private bool _pollWind = true;
    private bool _pollAGL = true;
    private bool _pollGlideSlope = true;
    private bool _pollTorque = false; // Disabled by default - zero overhead

    public event EventHandler<SimData>? DataUpdated;
    public event EventHandler<TorqueData>? TorqueDataUpdated;
    public event EventHandler<bool>? ConnectionStatusChanged;

    private enum DEFINITIONS
    {
        SimDataDefinition,
        TorqueDataDefinition,
        ThrottleSetDefinition
    }

    private enum DATA_REQUESTS
    {
        SimDataRequest,
        TorqueDataRequest
    }

    private enum EVENTS
    {
        SimRateIncrease,
        SimRateDecrease,
        ThrottleSet,
        Throttle1Set,
        Throttle1AxisSet
    }

    private enum NOTIFICATION_GROUPS
    {
        SimRateGroup
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct SimDataStruct
    {
        public double SimulationRate;
        public double GroundSpeed;
        public double WindSpeed;
        public double WindDirection;
        public double PlaneHeading;
        public double AltitudeAboveGround;
        public double VerticalSpeed;
    }

    public class SimData
    {
        public double SimulationRate { get; set; }
        public double GroundSpeed { get; set; }
        public double WindSpeed { get; set; }
        public double WindDirection { get; set; }
        public double PlaneHeading { get; set; }
        public double AltitudeAboveGround { get; set; }
        public double VerticalSpeed { get; set; }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct TorqueDataStruct
    {
        // Fixed arrays for up to 4 engines (interleaved: torque, percent, throttle for each)
        // Engine 1
        public double Engine1Torque;
        public double Engine1TorquePercent;
        public double Engine1Throttle;
        // Engine 2
        public double Engine2Torque;
        public double Engine2TorquePercent;
        public double Engine2Throttle;
        // Engine 3
        public double Engine3Torque;
        public double Engine3TorquePercent;
        public double Engine3Throttle;
        // Engine 4
        public double Engine4Torque;
        public double Engine4TorquePercent;
        public double Engine4Throttle;

        public double NumberOfEngines;

        // Helper method to extract data into arrays
        public (double[] torques, double[] percents, double[] throttles, int numEngines) ToArrays()
        {
            int n = (int)NumberOfEngines;
            double[] torques = new[] { Engine1Torque, Engine2Torque, Engine3Torque, Engine4Torque };
            double[] percents = new[] { Engine1TorquePercent, Engine2TorquePercent, Engine3TorquePercent, Engine4TorquePercent };
            double[] throttles = new[] { Engine1Throttle, Engine2Throttle, Engine3Throttle, Engine4Throttle };
            return (torques, percents, throttles, n);
        }
    }

    public class EngineData
    {
        public double Torque { get; set; }
        public double TorquePercent { get; set; }
        public double ThrottlePosition { get; set; }
    }

    public class TorqueData
    {
        public EngineData[] Engines { get; set; } = Array.Empty<EngineData>();
        public int NumberOfEngines { get; set; }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct ThrottleSetStruct
    {
        public double Throttle1Percent;
        public double Throttle2Percent;
        public double Throttle3Percent;
        public double Throttle4Percent;

        // Helper to create from array
        public static ThrottleSetStruct FromArray(double[] throttles)
        {
            return new ThrottleSetStruct
            {
                Throttle1Percent = throttles.Length > 0 ? throttles[0] : 0,
                Throttle2Percent = throttles.Length > 1 ? throttles[1] : 0,
                Throttle3Percent = throttles.Length > 2 ? throttles[2] : 0,
                Throttle4Percent = throttles.Length > 3 ? throttles[3] : 0
            };
        }
    }

    public SimConnectManager(IntPtr handle, int pollingRateMs = 500)
    {
        _handle = handle;
        _isConnected = false;

        // Reconnect timer - try every 3 seconds
        _reconnectTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _reconnectTimer.Tick += ReconnectTimer_Tick;

        // Poll timer - request data at specified rate
        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(pollingRateMs)
        };
        _pollTimer.Tick += PollTimer_Tick;

        // Start trying to connect
        _reconnectTimer.Start();
        TryConnect();
    }

    public void SetPollingRate(int milliseconds)
    {
        _pollTimer.Interval = TimeSpan.FromMilliseconds(milliseconds);
    }

    public void UpdateDataDefinition(bool pollGroundSpeed, bool pollWind, bool pollAGL, bool pollGlideSlope)
    {
        if (_simConnect == null || !_isConnected)
        {
            // Store for when we connect
            _pollGroundSpeed = pollGroundSpeed;
            _pollWind = pollWind;
            _pollAGL = pollAGL;
            _pollGlideSlope = pollGlideSlope;
            return;
        }

        // Only rebuild if something changed
        if (_pollGroundSpeed == pollGroundSpeed && _pollWind == pollWind &&
            _pollAGL == pollAGL && _pollGlideSlope == pollGlideSlope)
        {
            return;
        }

        Logger.WriteLine($"[SimConnectManager] Updating data definition: GS={pollGroundSpeed}, Wind={pollWind}, AGL={pollAGL}, Glide={pollGlideSlope}");

        _pollGroundSpeed = pollGroundSpeed;
        _pollWind = pollWind;
        _pollAGL = pollAGL;
        _pollGlideSlope = pollGlideSlope;

        try
        {
            // Clear old definition
            _simConnect.ClearDataDefinition(DEFINITIONS.SimDataDefinition);

            // Always poll sim rate (needed for reset button functionality)
            _simConnect.AddToDataDefinition(
                DEFINITIONS.SimDataDefinition,
                "SIMULATION RATE",
                "Number",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );

            // Conditionally add ground speed (needed by GroundSpeed panel AND GlideSlope panel)
            if (_pollGroundSpeed || _pollGlideSlope)
            {
                _simConnect.AddToDataDefinition(
                    DEFINITIONS.SimDataDefinition,
                    "GROUND VELOCITY",
                    "Knots",
                    SIMCONNECT_DATATYPE.FLOAT64,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED
                );
            }

            // Wind data (3 SimVars)
            if (_pollWind)
            {
                _simConnect.AddToDataDefinition(
                    DEFINITIONS.SimDataDefinition,
                    "AMBIENT WIND VELOCITY",
                    "Knots",
                    SIMCONNECT_DATATYPE.FLOAT64,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED
                );

                _simConnect.AddToDataDefinition(
                    DEFINITIONS.SimDataDefinition,
                    "AMBIENT WIND DIRECTION",
                    "Degrees",
                    SIMCONNECT_DATATYPE.FLOAT64,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED
                );

                _simConnect.AddToDataDefinition(
                    DEFINITIONS.SimDataDefinition,
                    "PLANE HEADING DEGREES TRUE",
                    "Degrees",
                    SIMCONNECT_DATATYPE.FLOAT64,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED
                );
            }

            // AGL
            if (_pollAGL)
            {
                _simConnect.AddToDataDefinition(
                    DEFINITIONS.SimDataDefinition,
                    "PLANE ALT ABOVE GROUND",
                    "Feet",
                    SIMCONNECT_DATATYPE.FLOAT64,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED
                );
            }

            // Vertical speed (for glide slope)
            if (_pollGlideSlope)
            {
                _simConnect.AddToDataDefinition(
                    DEFINITIONS.SimDataDefinition,
                    "VERTICAL SPEED",
                    "Feet per minute",
                    SIMCONNECT_DATATYPE.FLOAT64,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED
                );
            }

            // Re-register struct with new definition
            _simConnect.RegisterDataDefineStruct<SimDataStruct>(DEFINITIONS.SimDataDefinition);

            Logger.WriteLine("[SimConnectManager] Data definition updated successfully");
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"[SimConnectManager] Failed to update data definition: {ex.Message}");
        }
    }

    public void EnableTorqueMonitoring()
    {
        if (_pollTorque)
            return; // Already enabled

        Logger.WriteLine("[SimConnectManager] Enabling torque monitoring");
        _pollTorque = true;

        if (_simConnect == null || !_isConnected)
            return;

        try
        {
            // Register torque data definition for all 4 engines
            for (int i = 1; i <= 4; i++)
            {
                _simConnect.AddToDataDefinition(
                    DEFINITIONS.TorqueDataDefinition,
                    $"ENG TORQUE:{i}",
                    "Foot pounds",
                    SIMCONNECT_DATATYPE.FLOAT64,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED
                );

                _simConnect.AddToDataDefinition(
                    DEFINITIONS.TorqueDataDefinition,
                    $"TURB ENG MAX TORQUE PERCENT:{i}",
                    "Percent",
                    SIMCONNECT_DATATYPE.FLOAT64,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED
                );

                _simConnect.AddToDataDefinition(
                    DEFINITIONS.TorqueDataDefinition,
                    $"GENERAL ENG THROTTLE LEVER POSITION:{i}",
                    "Percent",
                    SIMCONNECT_DATATYPE.FLOAT64,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED
                );
            }

            // Add number of engines
            _simConnect.AddToDataDefinition(
                DEFINITIONS.TorqueDataDefinition,
                "NUMBER OF ENGINES",
                "Number",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );

            _simConnect.RegisterDataDefineStruct<TorqueDataStruct>(DEFINITIONS.TorqueDataDefinition);

            // Register throttle set definition for all 4 engines
            for (int i = 1; i <= 4; i++)
            {
                _simConnect.AddToDataDefinition(
                    DEFINITIONS.ThrottleSetDefinition,
                    $"GENERAL ENG THROTTLE LEVER POSITION:{i}",
                    "Percent",
                    SIMCONNECT_DATATYPE.FLOAT64,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED
                );
            }
            _simConnect.RegisterDataDefineStruct<ThrottleSetStruct>(DEFINITIONS.ThrottleSetDefinition);

            Logger.WriteLine("[SimConnectManager] Torque monitoring enabled successfully");
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"[SimConnectManager] Failed to enable torque monitoring: {ex.Message}");
        }
    }

    public void DisableTorqueMonitoring()
    {
        if (!_pollTorque)
            return; // Already disabled

        Logger.WriteLine("[SimConnectManager] Disabling torque monitoring");
        _pollTorque = false;

        if (_simConnect == null || !_isConnected)
            return;

        try
        {
            _simConnect.ClearDataDefinition(DEFINITIONS.TorqueDataDefinition);
            Logger.WriteLine("[SimConnectManager] Torque monitoring disabled successfully");
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"[SimConnectManager] Failed to disable torque monitoring: {ex.Message}");
        }
    }

    /// <summary>
    /// Sets throttles to absolute percentage values (0-100%) for all engines.
    /// Uses SetDataOnSimObject to directly write throttle values, overriding hardware input.
    /// </summary>
    public void SetThrottlesAbsolute(double[] targetThrottlePercents)
    {
        if (_simConnect == null || !_isConnected) return;

        try
        {
            // Clamp all values to valid range
            var clampedThrottles = targetThrottlePercents.Select(t => Math.Max(0.0, Math.Min(100.0, t))).ToArray();

            Logger.WriteLine($"[SimConnectManager] Setting throttles: [{string.Join(", ", clampedThrottles.Select(t => $"{t:F1}%"))}] (via SetDataOnSimObject)");

            // Use SetDataOnSimObject to directly write throttle positions
            var throttleData = ThrottleSetStruct.FromArray(clampedThrottles);

            _simConnect.SetDataOnSimObject(
                DEFINITIONS.ThrottleSetDefinition,
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_DATA_SET_FLAG.DEFAULT,
                throttleData
            );

            Logger.WriteLine($"[SimConnectManager] Throttle set command sent successfully");
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"[SimConnectManager] Failed to set throttles: {ex.Message}");
            Logger.WriteLine($"[SimConnectManager] Exception details: {ex.GetType().Name}");
        }
    }

    [Obsolete("Use SetThrottlesAbsolute instead - supports multi-engine")]
    public void ReduceThrottle(double currentThrottlePercent, int percentReduction)
    {
        double newThrottlePercent = currentThrottlePercent * (1.0 - (percentReduction / 100.0));
        SetThrottlesAbsolute(new[] { newThrottlePercent });
    }

    private void TryConnect()
    {
        if (_isConnected) return;

        try
        {
            _simConnect = new SimConnect("SimRate Sharp", _handle, 0x0402, null, 0);

            // Map sim rate events
            _simConnect.MapClientEventToSimEvent(EVENTS.SimRateIncrease, "SIM_RATE_INCR");
            _simConnect.MapClientEventToSimEvent(EVENTS.SimRateDecrease, "SIM_RATE_DECR");

            // Subscribe to events
            _simConnect.OnRecvSimobjectDataBytype += SimConnect_OnRecvSimobjectDataBytype;
            _simConnect.OnRecvQuit += SimConnect_OnRecvQuit;
            _simConnect.OnRecvException += SimConnect_OnRecvException;

            _isConnected = true;
            _reconnectTimer.Stop();
            _pollTimer.Start();

            // Build initial data definition based on current poll settings
            // Force rebuild by temporarily clearing the flags
            var tempGS = _pollGroundSpeed;
            var tempWind = _pollWind;
            var tempAGL = _pollAGL;
            var tempGlide = _pollGlideSlope;

            _pollGroundSpeed = !tempGS;
            _pollWind = !tempWind;
            _pollAGL = !tempAGL;
            _pollGlideSlope = !tempGlide;

            UpdateDataDefinition(tempGS, tempWind, tempAGL, tempGlide);

            ConnectionStatusChanged?.Invoke(this, true);
        }
        catch (COMException ex)
        {
            // MSFS not running or SimConnect not available
            Logger.WriteLine($"[SimConnectManager] Failed to connect: {ex.Message}");
            _simConnect = null;
            _isConnected = false;
        }
    }

    private void ReconnectTimer_Tick(object? sender, EventArgs e)
    {
        TryConnect();
    }

    private void PollTimer_Tick(object? sender, EventArgs e)
    {
        if (_simConnect == null || !_isConnected) return;

        try
        {
            _simConnect.RequestDataOnSimObjectType(
                DATA_REQUESTS.SimDataRequest,
                DEFINITIONS.SimDataDefinition,
                0,
                SIMCONNECT_SIMOBJECT_TYPE.USER
            );

            // Poll torque data if monitoring is enabled
            if (_pollTorque)
            {
                _simConnect.RequestDataOnSimObjectType(
                    DATA_REQUESTS.TorqueDataRequest,
                    DEFINITIONS.TorqueDataDefinition,
                    0,
                    SIMCONNECT_SIMOBJECT_TYPE.USER
                );
            }
        }
        catch (COMException ex)
        {
            Logger.WriteLine($"[SimConnectManager] Failed to request data: {ex.Message}");
            Disconnect();
        }
    }

    private void SimConnect_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
    {
        if (data.dwRequestID == (uint)DATA_REQUESTS.SimDataRequest)
        {
            var simData = (SimDataStruct)data.dwData[0];
            _currentSimRate = simData.SimulationRate;
            DataUpdated?.Invoke(this, new SimData
            {
                SimulationRate = simData.SimulationRate,
                GroundSpeed = simData.GroundSpeed,
                WindSpeed = simData.WindSpeed,
                WindDirection = simData.WindDirection,
                PlaneHeading = simData.PlaneHeading,
                AltitudeAboveGround = simData.AltitudeAboveGround,
                VerticalSpeed = simData.VerticalSpeed
            });
        }
        else if (data.dwRequestID == (uint)DATA_REQUESTS.TorqueDataRequest)
        {
            var torqueData = (TorqueDataStruct)data.dwData[0];
            var (torques, percents, throttles, numEngines) = torqueData.ToArrays();

            // Convert to EngineData array (only include actual engines)
            var engines = new EngineData[numEngines];
            for (int i = 0; i < numEngines; i++)
            {
                engines[i] = new EngineData
                {
                    Torque = torques[i],
                    TorquePercent = percents[i],
                    ThrottlePosition = throttles[i]
                };
            }

            TorqueDataUpdated?.Invoke(this, new TorqueData
            {
                Engines = engines,
                NumberOfEngines = numEngines
            });
        }
    }

    private void SimConnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
    {
        Disconnect();
    }

    private void SimConnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
    {
        Logger.WriteLine($"[SimConnectManager] SimConnect Exception ID: {data.dwException}");
        Logger.WriteLine($"[SimConnectManager] Exception details - SendID: {data.dwSendID}, Index: {data.dwIndex}");
    }

    private void Disconnect()
    {
        if (!_isConnected) return;

        _isConnected = false;
        _pollTimer.Stop();

        if (_simConnect != null)
        {
            try
            {
                _simConnect.Dispose();
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"[SimConnectManager] Error disposing SimConnect during disconnect: {ex.Message}");
            }
            _simConnect = null;
        }

        ConnectionStatusChanged?.Invoke(this, false);
        _reconnectTimer.Start();
    }

    public void ReceiveMessage()
    {
        _simConnect?.ReceiveMessage();
    }

    public void SetSimulationRate(double targetRate)
    {
        if (_simConnect == null || !_isConnected) return;

        try
        {
            // Calculate how many steps we need to reach the target rate
            // MSFS sim rates: 0.25, 0.5, 1, 2, 4, 8, 16, 32, 64, 128
            double diff = targetRate - _currentSimRate;

            if (Math.Abs(diff) < 0.01) return; // Already at target rate

            EVENTS eventToSend = diff > 0 ? EVENTS.SimRateIncrease : EVENTS.SimRateDecrease;
            int steps = (int)Math.Abs(Math.Round(Math.Log(Math.Abs(diff) + 1, 2)));

            // Send the appropriate number of increase/decrease events
            // We'll use a different approach: just send events until we're close
            // Safety limit: max 10 iterations (0.25 to 128 is only 9 doublings)
            const int MAX_ITERATIONS = 10;
            int iterations = 0;

            if (targetRate < _currentSimRate)
            {
                // Decreasing - send decrease events
                while (_currentSimRate > targetRate + 0.01 && iterations < MAX_ITERATIONS)
                {
                    _simConnect.TransmitClientEvent(
                        SimConnect.SIMCONNECT_OBJECT_ID_USER,
                        EVENTS.SimRateDecrease,
                        0,
                        NOTIFICATION_GROUPS.SimRateGroup,
                        SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY
                    );
                    // Estimate new rate (each decrease roughly halves it)
                    _currentSimRate = Math.Max(0.25, _currentSimRate / 2);
                    iterations++;
                }
                if (iterations >= MAX_ITERATIONS)
                {
                    Logger.WriteLine($"[SimConnectManager] Warning: Hit iteration limit while decreasing sim rate from {_currentSimRate} to {targetRate}");
                }
            }
            else
            {
                // Increasing - send increase events
                while (_currentSimRate < targetRate - 0.01 && iterations < MAX_ITERATIONS)
                {
                    _simConnect.TransmitClientEvent(
                        SimConnect.SIMCONNECT_OBJECT_ID_USER,
                        EVENTS.SimRateIncrease,
                        0,
                        NOTIFICATION_GROUPS.SimRateGroup,
                        SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY
                    );
                    // Estimate new rate (each increase roughly doubles it)
                    _currentSimRate = Math.Min(128, _currentSimRate * 2);
                    iterations++;
                }
                if (iterations >= MAX_ITERATIONS)
                {
                    Logger.WriteLine($"[SimConnectManager] Warning: Hit iteration limit while increasing sim rate from {_currentSimRate} to {targetRate}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"[SimConnectManager] Failed to set sim rate: {ex.Message}");
            Logger.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    public void Shutdown()
    {
        _reconnectTimer.Stop();
        _pollTimer.Stop();

        if (_simConnect != null)
        {
            try
            {
                _simConnect.Dispose();
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"[SimConnectManager] Error disposing SimConnect during shutdown: {ex.Message}");
            }
            _simConnect = null;
        }
    }
}
