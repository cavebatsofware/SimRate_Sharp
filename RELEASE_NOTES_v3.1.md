# SimRate Sharp v3.1.0 Release Notes

## Major New Feature: Internationalization & Localization

### Multi-Language Support
The headline feature of v3.1.0 is comprehensive **internationalization (i18n) and localization (l10n)** support, making SimRate Sharp more accessible to users worldwide. Most users are able
to use the English version just fine but it seems likely that some users will prefer to have
labels and menues in a language they are more comfortable with. These translations were
created with the help of AI since I do not speak any of these languages well myself, so any corrections are welcome.

**Supported Languages:**
- **English (en)** - Default language
- **Deutsch (de)** - German
- **Français (fr)** - French
- **中文 (zh-CN)** - Simplified Chinese
- **Español (es)** - Spanish

**Key Capabilities:**
- **Automatic Language Detection** - Attempts to detect Windows system language on launch
- **Runtime Language Switching** - Change language (requires application restart)
- **Fully Localized UI** - All menus, windows, and messages translated
- **Intelligent Fallback** - Defaults to English if system language is not supported
- **Persistent Language Preference** - Selected language saved to settings

**Use Cases:**
- International pilots and flight simulation enthusiasts
- Non-English speaking markets (Europe, Asia, Latin America)
- Training environments with multilingual users
- Community sharing and adoption across language barriers

---

## Unit System Localization

### Display Unit Options
v3.1.0 introduces configurable **unit systems** for speed and altitude displays, accommodating different regional preferences and standards.

**Speed Units:**
- **Knots (kts)** - ICAO standard, default for aviation
- **Kilometers per Hour (km/h)** - Metric system
- **Miles per Hour (mph)** - Imperial system

**Altitude/Distance Units:**
- **Feet (ft)** - ICAO standard, default for aviation
- **Meters (m)** - Metric system

**Key Features:**
- **Automatic Conversion** - Conversion from SimConnect data
- **Runtime Switching** - Change units via context menu without restart
- **Localized Labels** - Unit abbreviations translated per language
- **Precise Calculations** - Industry-standard conversion constants (1 kt = 1.852 km/h, 1 ft = 0.3048 m)
- **Settings Persistence** - Unit preferences saved to `settings.json`

**Configuration:**
All unit preferences are accessible through the enhanced context menu:
- Language → Select from 5 supported languages
- Speed Units → Knots / km/h / mph
- Altitude Units → Feet / Meters

---

## Technical Implementation

### Localization Architecture

**LocalizationManager:**
- Manages application culture and thread culture settings
- Auto-detects system language from Windows `CurrentUICulture`
- Supports exact culture matching (e.g., zh-CN) and two-letter ISO codes
- Graceful fallback mechanism to English if culture setting fails
- Comprehensive logging of language detection and culture changes

**Supported Languages Array:**
```csharp
{ "en", "de", "fr", "zh-CN", "es" }
```

### Unit Conversion System

**UnitConverter (Static Class):**
- Conversion constants based on international standards
- Separate methods for speed and altitude conversions
- Formatting methods that combine conversion + localized labels
- Display name methods for menu items (language-aware)

**Conversion Constants:**
```csharp
KNOTS_TO_KMH = 1.852
KNOTS_TO_MPH = 1.15078
FEET_TO_METERS = 0.3048
```

**Enum Types:**
```csharp
SpeedUnit { Knots, KilometersPerHour, MilesPerHour }
AltitudeUnit { Feet, Meters }
```

**Data Flow:**
```
SimConnect (knots/feet)
  → UnitConverter.ConvertSpeed/ConvertAltitude
  → Display (user's preferred unit)
```

### Localized Components

**MainWindow:**
- Context menu items (all display toggles, torque limiter, units, language)
- Tooltip texts
- Dynamic menu item updates on language/unit changes
- Runtime UI refresh after language selection

**AboutWindow:**
- Window title and version display
- Developer information
- License text
- GitHub repository link

**Error Messages:**
- SimConnect connection errors
- File I/O errors
- Settings load/save failures

---

## Settings & Configuration

### New Settings Properties

**Language Settings:**
- `Language` (string) - ISO language code (e.g., "en", "de", "fr", "zh-CN", "es")
  - Default: `null` (triggers auto-detection)
  - Persisted to `settings.json`

**Unit Settings:**
- `SpeedUnit` (enum) - Speed display unit preference
  - Options: `Knots`, `KilometersPerHour`, `MilesPerHour`
  - Default: `Knots`
- `AltitudeUnit` (enum) - Altitude display unit preference
  - Options: `Feet`, `Meters`
  - Default: `Feet`

**Settings Migration:**
Existing `settings.json` files from v3.0 will automatically gain these new properties with default values on first launch of v3.1.0.

---

## UI/UX Improvements

### Enhanced Context Menu

**New Language Submenu:**
- Top-level "Language" menu item with submenu
- 5 language options with native names (English, Deutsch, Français, 中文, Español)
- Radio-button style checkmarks showing current selection
- Immediate UI update on selection

**New Units Submenus:**
- "Speed Units" submenu with 3 options
- "Altitude Units" submenu with 2 options
- Localized menu item labels that update with language changes
- Radio-button style checkmarks showing current selection
- Real-time display updates when units are changed

**Dynamic Menu Updates:**
- All menu items refresh when language changes
- Checkmarks update to reflect current settings
- Tooltip texts update to match new language

---

## Breaking Changes

**None** - All existing settings and configurations from v3.0 are forward-compatible. The localization and unit system features integrate seamlessly with existing functionality.

---

## Upgrade Notes

### From v3.0 to v3.1.0

1. **Settings Migration** - Existing `settings.json` will automatically gain new default values:
   - `Language`: `null` (auto-detect on first launch)
   - `SpeedUnit`: `Knots`
   - `AltitudeUnit`: `Feet`

2. **First Launch Behavior** - Application will detect your Windows system language and apply it automatically (if supported)

3. **Language Selection** - To change language: Right-click → Language → Select preferred language

4. **Unit Selection** - To change units:
   - Speed: Right-click → Speed Units → Select preferred unit
   - Altitude: Right-click → Altitude Units → Select preferred unit

## Translation Quality

All translations (German, French, Spanish, Chinese) are AI generated attempting to consider:
- Aviation terminology accuracy
- Cultural appropriateness
- Consistent terminology across the application
- Proper character encoding (UTF-8 with BOM for RESX files)

---

## Known Limitations

### Language Support
- Additional languages can be added in future releases based on community demand

### Unit Conversions
- All internal calculations still use ICAO standard units (knots, feet)
- Conversion is display-only; does not affect SimConnect data exchange

---

## Credits

Developed by Grant DeFayette / CavebatSoftware LLC

Special thanks to the international MSFS community for translation review and feedback.

---

## License

GNU General Public License v3.0 - See LICENSE file for details
