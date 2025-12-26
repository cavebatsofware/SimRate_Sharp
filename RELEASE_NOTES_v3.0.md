# SimRate Sharp v3.0 Release Notes

## Major New Feature: Overtorque Protection System

### Engine Protection (Optional Feature)
The headline feature of v3.0 is a comprehensive **Overtorque Protection (OTP) system** designed to prevent engine damage during flight operations.

**Key Capabilities:**
- **Multi-Engine Support** - Monitors and controls 1-4 engines independently
- **Visual Monitoring** - Vertical bar gauges with color-coded warnings (green → yellow → red)
- **Intelligent Throttle Limiting** - Automatically reduces throttle when engines exceed torque limits
- **Proportional Response** - Reduction severity scales with overtorque amount (configurable aggression factor)
- **Iterative Correction** - Makes multiple adjustments with configurable stabilization time between interventions
- **Per-Engine Control** - Only intervenes on engines exceeding limits, leaving others untouched
- **Audio Alerts** - Claxon-style warning tone (dual-frequency synthesized audio) on intervention
- **Zero Overhead** - No performance impact when disabled; fully opt-in feature

**Use Cases:**
- Firefighting operations (e.g., CL-415, DC-10, 737 tankers)
- Turboprop aircraft operations (PT6A, TPE331, PW100 series engines)
- Training scenarios where engine management is being learned
- Any high-workload situation where precise throttle management is challenging

**Configuration (All Parameters Adjustable):**
- Max Torque % (default: 100%) - Trigger threshold as percentage of aircraft's rated maximum
- Warning Threshold % (default: 90%) - When visual bars turn yellow before red
- Reduction Aggression (default: 2.5x) - Multiplier determining throttle cut severity
- Minimum Throttle % (default: 40%) - Safety floor to prevent engine stall
- Intervention Cooldown (default: 2000ms) - Time between corrections for engine/prop stabilization

**Technical Implementation:**
- Uses `SetDataOnSimObject` to directly write throttle positions, overriding hardware input
- Percentage-based limits work with any aircraft (not tied to absolute torque values)
- Array-based architecture for clean multi-engine handling
- Lazy initialization ensures zero overhead when disabled

---

## UI/UX Improvements

### Display Panel Management
- **Integrated Reset Button** - 1x reset button now part of Sim Rate panel (cleaner layout)
- **Consistent Spacing** - Fixed panel margins for uniform appearance when showing/hiding displays
- **Dynamic Layout** - Panels automatically adjust when toggled, maintaining consistent spacing

### Visual Refinements
- **Removed Numeric Separators** - Cleaner visual design with margin-based spacing
- **Consolidated Grid Layout** - Reduced from 13 to 6 columns (plus reset button)
- **Torque Visualization** - Vertical bar gauge system

---

## Performance & Architecture

### Optimization
- **Dynamic SimConnect Polling** - Only polls data for visible panels (up to 86% reduction in SimVars when minimal panels shown)
- **Conditional Data Definitions** - Rebuilds SimConnect data definition based on visibility settings
- **Zero-Overhead Torque System** - Torque monitoring disabled by default; no resources consumed until enabled

---

## Settings & Configuration

### Enhanced Context Menu
All torque limiter settings are now fully configurable through the UI:
- Enable/disable torque limiter
- Show/hide torque display
- Configure max torque percentage
- Adjust warning threshold
- Tune reduction aggression
- Set minimum throttle floor
- Configure intervention cooldown timing
- View intervention count

### Settings Persistence
All new torque limiter parameters are automatically saved to `settings.json`:
- `TorqueLimiterEnabled` - Feature on/off state
- `ShowTorque` - Display visibility
- `MaxTorquePercent` - Limit threshold
- `TorqueWarningThreshold` - Yellow warning trigger
- `ThrottleReductionAggression` - Intervention multiplier
- `MinThrottlePercent` - Safety floor
- `InterventionCooldownMs` - Correction timing

---

## Audio System

### Synthesized Alert Tones
- **Claxon Effect** - Dual-frequency (600Hz + 800Hz) simultaneous tones
- **WAV Synthesis** - Generates proper audio waveforms in-memory
- **Professional Sound** - Similar to real aircraft warning systems
- **Non-Intrusive** - 150ms duration, 30% amplitude, attention-getting without being annoying

---

## Bug Fixes

### UI Stability
- **Panel Spacing** - Fixed inconsistent margins when toggling display panels

---

## Breaking Changes

**None** - All existing settings and configurations are forward-compatible. The overtorque protection system is entirely opt-in.

---

## Upgrade Notes

### From v2.0 to v3.0

1. **Settings Migration** - Existing `settings.json` will automatically gain new default values for torque limiter parameters
2. **Clean Install** (Optional) - If unexpected behavior is observed, delete `%APPDATA%\SimRateSharp\settings.json` before first launch of v3.0. This should not be needed unless the file was modified.
3. **Feature Activation** - Overtorque Protection is **disabled by default**; enable via context menu → Torque Limiter → Enable

### Recommended Configuration for CL-415 Firefighting

```
Max Torque: 100%
Warning Threshold: 90%
Reduction Aggression: 2.8x
Minimum Throttle: 40%
Intervention Cooldown: 3000ms
```

These settings provide smooth, effective protection during water bombing operations without interfering with normal flight operations.

---

## Technical Details

### Multi-Engine Architecture

**Data Structures:**
```csharp
TorqueDataStruct:
  - Engine1Torque, Engine1TorquePercent, Engine1Throttle
  - Engine2Torque, Engine2TorquePercent, Engine2Throttle
  - Engine3Torque, Engine3TorquePercent, Engine3Throttle
  - Engine4Torque, Engine4TorquePercent, Engine4Throttle
  - NumberOfEngines
  - Helper: ToArrays() for clean array extraction
```

**Algorithm:**
```
For each engine:
  if torquePercent > maxTorquePercent:
    overtorqueExcess = torquePercent - maxTorquePercent
    throttleReduction = overtorqueExcess × aggression
    newThrottle = currentThrottle - throttleReduction
    newThrottle = clamp(newThrottle, minThrottle, 100%)
```

### SimConnect Integration

**Monitored SimVars (per engine):**
- `ENG TORQUE:n` - Absolute torque in foot-pounds
- `TURB ENG MAX TORQUE PERCENT:n` - Percentage of aircraft's rated maximum
- `GENERAL ENG THROTTLE LEVER POSITION:n` - Current throttle position (0-100%)
- `NUMBER OF ENGINES` - Aircraft engine count for dynamic display

**Control Method:**
- Uses `SetDataOnSimObject` to write throttle positions
- Bypasses event system to override hardware input reliably
- Updates all engines simultaneously with single SimConnect call

---

## Credits

Developed by Grant DeFayette / CavebatSoftware LLC

Special thanks to the MSFS community for testing and feedback, particularly on the CL-415 overtorque protection use case.

---

## License

GNU General Public License v3.0 - See LICENSE file for details
