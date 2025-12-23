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
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace SimRateSharp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Check for debug mode flag
        bool debugMode = false;
        foreach (string arg in e.Args)
        {
            if (arg.Equals("--debug", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("/debug", StringComparison.OrdinalIgnoreCase))
            {
                debugMode = true;
                break;
            }
        }

        // Initialize logger
        Logger.Initialize(debugMode);
        Logger.WriteLine($"SimRate Sharp starting (Debug Mode: {debugMode})");

        // Global exception handlers
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        DispatcherUnhandledException += App_DispatcherUnhandledException;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogException(e.ExceptionObject as Exception, "AppDomain.UnhandledException");
        MessageBox.Show($"Fatal error: {e.ExceptionObject}", "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception, "Dispatcher.UnhandledException");
        MessageBox.Show($"Unhandled exception: {e.Exception.Message}\n\n{e.Exception.StackTrace}",
            "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Shutdown();
        base.OnExit(e);
    }

    private void LogException(Exception? ex, string source)
    {
        if (ex == null) return;

        Logger.WriteLine($"EXCEPTION in {source}: {ex.Message}");
        Logger.WriteLine($"Stack trace: {ex.StackTrace}");

        try
        {
            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "SimRateSharp_Error.log");
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}\n{ex}\n\n";
            File.AppendAllText(logPath, logMessage);
        }
        catch { }
    }
}

