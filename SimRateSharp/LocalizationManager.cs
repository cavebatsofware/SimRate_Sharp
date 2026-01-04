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
using System.Globalization;
using System.Threading;

namespace SimRateSharp;

public static class LocalizationManager
{
    private static readonly string[] SupportedLanguages = { "en", "de", "fr", "zh-CN", "es" };

    /// <summary>
    /// Initializes the application culture based on settings or auto-detection
    /// </summary>
    public static void Initialize(Settings settings)
    {
        string cultureName;

        if (!string.IsNullOrEmpty(settings.Language))
        {
            // Use explicit language from settings
            cultureName = settings.Language;
            Logger.WriteLine($"[Localization] Using language from settings: {cultureName}");
        }
        else
        {
            // Auto-detect from Windows
            cultureName = GetSystemLanguage();
            Logger.WriteLine($"[Localization] Auto-detected language from Windows: {cultureName}");
        }

        SetCulture(cultureName);
    }

    /// <summary>
    /// Gets the system language, falling back to English if not supported
    /// </summary>
    private static string GetSystemLanguage()
    {
        var currentCulture = CultureInfo.CurrentUICulture;
        var cultureName = currentCulture.Name.ToLower();

        // First check for exact culture match (e.g., zh-CN)
        foreach (var lang in SupportedLanguages)
        {
            if (cultureName.StartsWith(lang.ToLower()))
            {
                Logger.WriteLine($"[Localization] System language '{cultureName}' matches supported language '{lang}'");
                return lang;
            }
        }

        // Then check two-letter code
        var twoLetterCode = currentCulture.TwoLetterISOLanguageName.ToLower();
        foreach (var lang in SupportedLanguages)
        {
            if (lang == twoLetterCode)
            {
                Logger.WriteLine($"[Localization] System language '{twoLetterCode}' is supported");
                return twoLetterCode;
            }
        }

        // Default to English if not supported
        Logger.WriteLine($"[Localization] System language '{cultureName}' not supported, defaulting to English");
        return "en";
    }

    /// <summary>
    /// Sets the application culture
    /// </summary>
    private static void SetCulture(string cultureName)
    {
        try
        {
            var culture = new CultureInfo(cultureName);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            // Set the culture for the resource manager
            Resources.Strings.Culture = culture;

            Logger.WriteLine($"[Localization] Culture set to: {culture.Name} ({culture.DisplayName})");
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"[Localization] Failed to set culture '{cultureName}': {ex.Message}");
            Logger.WriteLine($"[Localization] Falling back to English");

            // Fall back to English
            var englishCulture = new CultureInfo("en");
            Thread.CurrentThread.CurrentCulture = englishCulture;
            Thread.CurrentThread.CurrentUICulture = englishCulture;
            CultureInfo.DefaultThreadCurrentCulture = englishCulture;
            CultureInfo.DefaultThreadCurrentUICulture = englishCulture;
            Resources.Strings.Culture = englishCulture;
        }
    }

    /// <summary>
    /// Gets the display name for a language code
    /// </summary>
    public static string GetLanguageName(string languageCode)
    {
        return languageCode.ToLower() switch
        {
            "en" => "English",
            "de" => "Deutsch",
            "fr" => "Français",
            "zh-cn" => "中文",
            "es" => "Español",
            _ => languageCode
        };
    }

    /// <summary>
    /// Gets the current language code
    /// </summary>
    public static string GetCurrentLanguage()
    {
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLower();
    }
}
