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

    public event EventHandler<double>? SimRateUpdated;
    public event EventHandler<bool>? ConnectionStatusChanged;

    private enum DEFINITIONS
    {
        SimRateDefinition
    }

    private enum DATA_REQUESTS
    {
        SimRateRequest
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct SimRateStruct
    {
        public double SimulationRate;
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
                DEFINITIONS.SimRateDefinition,
                "SIMULATION RATE",
                "Number",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED
            );

            _simConnect.RegisterDataDefineStruct<SimRateStruct>(DEFINITIONS.SimRateDefinition);

            // Subscribe to events
            _simConnect.OnRecvSimobjectDataBytype += SimConnect_OnRecvSimobjectDataBytype;
            _simConnect.OnRecvQuit += SimConnect_OnRecvQuit;
            _simConnect.OnRecvException += SimConnect_OnRecvException;

            _isConnected = true;
            _reconnectTimer.Stop();
            _pollTimer.Start();

            ConnectionStatusChanged?.Invoke(this, true);
        }
        catch (COMException)
        {
            // MSFS not running or SimConnect not available
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
                DATA_REQUESTS.SimRateRequest,
                DEFINITIONS.SimRateDefinition,
                0,
                SIMCONNECT_SIMOBJECT_TYPE.USER
            );
        }
        catch (COMException)
        {
            Disconnect();
        }
    }

    private void SimConnect_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
    {
        if (data.dwRequestID == (uint)DATA_REQUESTS.SimRateRequest)
        {
            var simRateData = (SimRateStruct)data.dwData[0];
            SimRateUpdated?.Invoke(this, simRateData.SimulationRate);
        }
    }

    private void SimConnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
    {
        Disconnect();
    }

    private void SimConnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
    {
        Console.WriteLine($"SimConnect Exception: {data.dwException}");
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
