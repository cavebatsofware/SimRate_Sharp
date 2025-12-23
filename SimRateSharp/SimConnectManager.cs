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

    public event EventHandler<SimData>? DataUpdated;
    public event EventHandler<bool>? ConnectionStatusChanged;

    private enum DEFINITIONS
    {
        SimDataDefinition
    }

    private enum DATA_REQUESTS
    {
        SimDataRequest
    }

    private enum EVENTS
    {
        SimRateIncrease,
        SimRateDecrease
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
    }

    public class SimData
    {
        public double SimulationRate { get; set; }
        public double GroundSpeed { get; set; }
        public double WindSpeed { get; set; }
        public double WindDirection { get; set; }
        public double PlaneHeading { get; set; }
    }

    public SimConnectManager(IntPtr handle)
    {
        _handle = handle;
        _isConnected = false;

        // Reconnect timer - try every 3 seconds
        _reconnectTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _reconnectTimer.Tick += ReconnectTimer_Tick;

        // Poll timer - request data every 500ms
        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _pollTimer.Tick += PollTimer_Tick;

        // Start trying to connect
        _reconnectTimer.Start();
        TryConnect();
    }

    private void TryConnect()
    {
        if (_isConnected) return;

        try
        {
            _simConnect = new SimConnect("SimRate Sharp", _handle, 0x0402, null, 0);

            // Define the data structure
            _simConnect.AddToDataDefinition(
                DEFINITIONS.SimDataDefinition,
                "SIMULATION RATE",
                "Number",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );

            _simConnect.AddToDataDefinition(
                DEFINITIONS.SimDataDefinition,
                "GROUND VELOCITY",
                "Knots",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );

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

            _simConnect.RegisterDataDefineStruct<SimDataStruct>(DEFINITIONS.SimDataDefinition);

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
                PlaneHeading = simData.PlaneHeading
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
            catch { }
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
            if (targetRate < _currentSimRate)
            {
                // Decreasing - send decrease events
                while (_currentSimRate > targetRate + 0.01)
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
                }
            }
            else
            {
                // Increasing - send increase events
                while (_currentSimRate < targetRate - 0.01)
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
            catch { }
            _simConnect = null;
        }
    }
}
