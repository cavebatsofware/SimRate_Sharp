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

namespace SimRateSharp;

/// <summary>
/// Speed unit options
/// </summary>
public enum SpeedUnit
{
    Knots,          // kts - ICAO standard
    KilometersPerHour,  // km/h - Metric
    MilesPerHour    // mph - Imperial
}

/// <summary>
/// Altitude/Distance unit options
/// </summary>
public enum AltitudeUnit
{
    Feet,   // ft - ICAO standard
    Meters  // m - Metric
}

/// <summary>
/// Manages unit conversions and formatting
/// </summary>
public static class UnitConverter
{
    // Conversion constants
    private const double KNOTS_TO_KMH = 1.852;
    private const double KNOTS_TO_MPH = 1.15078;
    private const double FEET_TO_METERS = 0.3048;

    /// <summary>
    /// Converts speed from knots to the specified unit
    /// </summary>
    public static double ConvertSpeed(double knots, SpeedUnit targetUnit)
    {
        return targetUnit switch
        {
            SpeedUnit.Knots => knots,
            SpeedUnit.KilometersPerHour => knots * KNOTS_TO_KMH,
            SpeedUnit.MilesPerHour => knots * KNOTS_TO_MPH,
            _ => knots
        };
    }

    /// <summary>
    /// Converts altitude from feet to the specified unit
    /// </summary>
    public static double ConvertAltitude(double feet, AltitudeUnit targetUnit)
    {
        return targetUnit switch
        {
            AltitudeUnit.Feet => feet,
            AltitudeUnit.Meters => feet * FEET_TO_METERS,
            _ => feet
        };
    }

    /// <summary>
    /// Formats speed with the appropriate unit label
    /// </summary>
    public static string FormatSpeed(double knots, SpeedUnit unit)
    {
        var converted = ConvertSpeed(knots, unit);
        var label = GetSpeedUnitLabel(unit);
        return $"{converted:F0} {label}";
    }

    /// <summary>
    /// Formats altitude with the appropriate unit label
    /// </summary>
    public static string FormatAltitude(double feet, AltitudeUnit unit)
    {
        var converted = ConvertAltitude(feet, unit);
        var label = GetAltitudeUnitLabel(unit);
        return $"{converted:F0} {label}";
    }

    /// <summary>
    /// Gets the localized label for a speed unit
    /// </summary>
    public static string GetSpeedUnitLabel(SpeedUnit unit)
    {
        return unit switch
        {
            SpeedUnit.Knots => SimRateSharp.Resources.Strings.Unit_Knots,
            SpeedUnit.KilometersPerHour => SimRateSharp.Resources.Strings.Unit_KilometersPerHour,
            SpeedUnit.MilesPerHour => SimRateSharp.Resources.Strings.Unit_MilesPerHour,
            _ => "kts"
        };
    }

    /// <summary>
    /// Gets the localized label for an altitude unit
    /// </summary>
    public static string GetAltitudeUnitLabel(AltitudeUnit unit)
    {
        return unit switch
        {
            AltitudeUnit.Feet => SimRateSharp.Resources.Strings.Unit_Feet,
            AltitudeUnit.Meters => SimRateSharp.Resources.Strings.Unit_Meters,
            _ => "ft"
        };
    }

    /// <summary>
    /// Gets the display name for a speed unit (for menu items)
    /// </summary>
    public static string GetSpeedUnitDisplayName(SpeedUnit unit)
    {
        return unit switch
        {
            SpeedUnit.Knots => SimRateSharp.Resources.Strings.Menu_Units_Speed_Knots,
            SpeedUnit.KilometersPerHour => SimRateSharp.Resources.Strings.Menu_Units_Speed_KMH,
            SpeedUnit.MilesPerHour => SimRateSharp.Resources.Strings.Menu_Units_Speed_MPH,
            _ => "Knots"
        };
    }

    /// <summary>
    /// Gets the display name for an altitude unit (for menu items)
    /// </summary>
    public static string GetAltitudeUnitDisplayName(AltitudeUnit unit)
    {
        return unit switch
        {
            AltitudeUnit.Feet => SimRateSharp.Resources.Strings.Menu_Units_Altitude_Feet,
            AltitudeUnit.Meters => SimRateSharp.Resources.Strings.Menu_Units_Altitude_Meters,
            _ => "Feet"
        };
    }
}
