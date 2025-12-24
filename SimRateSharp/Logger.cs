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
using System.IO;
using System.Windows;

namespace SimRateSharp;

public static class Logger
{
    private static StreamWriter? _logWriter;
    private static readonly object _lock = new object();
    private static bool _isDebugMode = false;

    public static void Initialize(bool debugMode = false)
    {
        _isDebugMode = debugMode;

        if (_isDebugMode)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var logFolder = Path.Combine(appData, "SimRateSharp", "Logs");
                Directory.CreateDirectory(logFolder);

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var logPath = Path.Combine(logFolder, $"SimRateSharp_{timestamp}.log");

                _logWriter = new StreamWriter(logPath, append: true) { AutoFlush = true };

                WriteLine($"=== SimRate Sharp Log Started: {DateTime.Now} ===");
                WriteLine($"Debug Mode: Enabled");
                WriteLine($"Log File: {logPath}");
            }
            catch (Exception ex)
            {
                _isDebugMode = false;
                MessageBox.Show($"Failed to initialize log file: {ex.Message}\n\nDebug logging will be disabled.",
                    "Logger Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    public static void WriteLine(string message)
    {
        lock (_lock)
        {
            var timestampedMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";

            if (_isDebugMode && _logWriter != null)
            {
                try
                {
                    _logWriter.WriteLine(timestampedMessage);
                }
                catch (Exception ex)
                {
                    // Disable debug mode and alert user
                    _isDebugMode = false;
                    MessageBox.Show($"Failed to write to log file: {ex.Message}\n\nDebug logging has been disabled.",
                        "Logger Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }

    public static void Shutdown()
    {
        lock (_lock)
        {
            if (_logWriter != null)
            {
                WriteLine("=== SimRate Sharp Log Ended ===");
                _logWriter.Flush();
                _logWriter.Close();
                _logWriter.Dispose();
                _logWriter = null;
            }
        }
    }
}
