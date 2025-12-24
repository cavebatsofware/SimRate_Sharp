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
using System.Text.Json;

namespace SimRateSharp;

public class Settings
{
    public double WindowX { get; set; } = 100;
    public double WindowY { get; set; } = 100;
    public double Opacity { get; set; } = 0.8;
    public int PollingRateMs { get; set; } = 500; // Default 500ms
    public int? JoystickDeviceIndex { get; set; } = null;
    public int? JoystickButton { get; set; } = null;

    // Display visibility settings
    public bool ShowSimRate { get; set; } = true;
    public bool ShowGroundSpeed { get; set; } = true;
    public bool ShowAGL { get; set; } = true;
    public bool ShowWind { get; set; } = true;
    public bool ShowGlideSlope { get; set; } = true;
    public bool ShowTorque { get; set; } = false; // Hidden by default

    // Torque limiter settings (feature disabled by default - zero overhead)
    public bool TorqueLimiterEnabled { get; set; } = false;
    public double MaxTorquePercent { get; set; } = 100.0; // Trigger at 100% of aircraft's rated max torque
    public double TorqueWarningThreshold { get; set; } = 0.90; // Warn at 90% of max (yellow at 90%, red at 100%)

    // Intelligent throttle reduction algorithm settings
    public double ThrottleReductionAggression { get; set; } = 2.5; // Multiplier for overtorque severity (2.5x means 8% overtorque → 20% throttle reduction)
    public double MinThrottlePercent { get; set; } = 40.0; // Never reduce throttle below this value (safety floor)
    public int InterventionCooldownMs { get; set; } = 2000; // Minimum time between interventions (allows engine/prop to stabilize - 2 seconds)

    private static string GetSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "SimRateSharp");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "settings.json");
    }

    public static Settings Load()
    {
        try
        {
            var path = GetSettingsPath();
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
                Logger.WriteLine($"[Settings] Loaded settings from {path}");
                return settings;
            }
            else
            {
                Logger.WriteLine($"[Settings] No settings file found at {path}, using defaults");
            }
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"[Settings] Failed to load settings: {ex.Message}");
            Logger.WriteLine($"[Settings] Using default settings");
        }
        return new Settings();
    }

    public bool Save()
    {
        try
        {
            var path = GetSettingsPath();
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            Logger.WriteLine($"[Settings] Successfully saved settings to {path}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"[Settings] Failed to save settings: {ex.Message}");
            Logger.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }
}
