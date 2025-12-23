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
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
        }
        catch
        {
            // If loading fails, return defaults
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            var path = GetSettingsPath();
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch
        {
            // Silently fail if we can't save settings
        }
    }
}
