using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SimRateSharp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private SimConnectManager? _simConnectManager;
    private const int WM_USER_SIMCONNECT = 0x0402;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Get the window handle
        var helper = new WindowInteropHelper(this);
        var handle = helper.Handle;

        // Add hook for SimConnect messages
        HwndSource source = HwndSource.FromHwnd(handle);
        source.AddHook(HandleSimConnectMessage);

        // Initialize SimConnect manager
        _simConnectManager = new SimConnectManager(handle);
        _simConnectManager.SimRateUpdated += SimConnectManager_SimRateUpdated;
        _simConnectManager.ConnectionStatusChanged += SimConnectManager_ConnectionStatusChanged;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _simConnectManager?.Shutdown();
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

    private void SimConnectManager_SimRateUpdated(object? sender, double simRate)
    {
        Dispatcher.Invoke(() =>
        {
            SimRateTextBlock.Text = $"{simRate:F2}x";
        });
    }

    private void SimConnectManager_ConnectionStatusChanged(object? sender, bool isConnected)
    {
        Dispatcher.Invoke(() =>
        {
            if (isConnected)
            {
                StatusTextBlock.Text = "Connected to MSFS";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                StatusTextBlock.Text = "Disconnected - Reconnecting...";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                SimRateTextBlock.Text = "--";
            }
        });
    }

    private void AlwaysOnTopCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        Topmost = true;
    }

    private void AlwaysOnTopCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        Topmost = false;
    }
}