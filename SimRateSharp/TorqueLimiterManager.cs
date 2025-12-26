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

namespace SimRateSharp;

public class TorqueLimiterManager : IDisposable
{
    private readonly Settings _settings;
    private bool _isLimiting = false;
    private int _interventionCount = 0;
    private DateTime _lastInterventionTime = DateTime.MinValue;
    private int _lastEngineCount = 0;

    // Note: TorqueUpdated event removed - UI updates directly from SimConnect data
    // Only LimitTriggered event is used for throttle intervention
    public event EventHandler<TorqueLimitEvent>? LimitTriggered;

    public class TorqueLimitEvent
    {
        public int InterventionCount { get; set; }
        public double[] CurrentTorquePercents { get; set; } = Array.Empty<double>();
        public double[] CurrentThrottlePercents { get; set; } = Array.Empty<double>();
        public double[] RecommendedThrottlePercents { get; set; } = Array.Empty<double>();
        public int[] OverlimitEngines { get; set; } = Array.Empty<int>(); // Indices of engines over limit
    }

    public TorqueLimiterManager(Settings settings)
    {
        _settings = settings;
        Logger.WriteLine("[TorqueLimiter] Torque limiter manager initialized");
    }

    public void ProcessTorqueData(SimConnectManager.EngineData[] engines)
    {
        if (!_settings.TorqueLimiterEnabled)
            return;

        // Detect when engines become available (aircraft loaded)
        if (_lastEngineCount == 0 && engines.Length > 0)
        {
            Logger.WriteLine($"[TorqueLimiter] Engines detected: {engines.Length} engine(s) now available");
            _isLimiting = false;
            _interventionCount = 0;
            _lastInterventionTime = DateTime.MinValue;
        }

        _lastEngineCount = engines.Length;

        if (engines.Length == 0)
            return;

        // Use percentage-based limits
        double maxPercent = _settings.MaxTorquePercent;
        double warningThreshold = maxPercent * _settings.TorqueWarningThreshold;

        // Check which engines are over limit
        var overlimitEngines = new List<int>();
        for (int i = 0; i < engines.Length; i++)
        {
            if (engines[i].TorquePercent > maxPercent)
                overlimitEngines.Add(i);
        }

        bool anyOverLimit = overlimitEngines.Count > 0;

        // Iterative intervention logic
        if (anyOverLimit)
        {
            var timeSinceLastIntervention = (DateTime.Now - _lastInterventionTime).TotalMilliseconds;
            bool canIntervene = timeSinceLastIntervention >= _settings.InterventionCooldownMs;

            if (canIntervene)
            {
                if (!_isLimiting)
                {
                    _isLimiting = true;
                    Logger.WriteLine($"[TorqueLimiter] OVERTORQUE DETECTED! Engines: {string.Join(", ", overlimitEngines.Select(i => $"#{i + 1}"))}");
                }

                _interventionCount++;
                _lastInterventionTime = DateTime.Now;

                // Calculate recommended throttle for each engine
                var currentThrottles = new double[engines.Length];
                var recommendedThrottles = new double[engines.Length];
                var torquePercents = new double[engines.Length];

                for (int i = 0; i < engines.Length; i++)
                {
                    currentThrottles[i] = engines[i].ThrottlePosition;
                    torquePercents[i] = engines[i].TorquePercent;

                    if (overlimitEngines.Contains(i))
                    {
                        // Calculate reduction for this engine
                        recommendedThrottles[i] = CalculateThrottleReduction(
                            engines[i].TorquePercent,
                            engines[i].ThrottlePosition,
                            maxPercent
                        );
                    }
                    else
                    {
                        // Keep throttle unchanged for engines within limits
                        recommendedThrottles[i] = engines[i].ThrottlePosition;
                    }
                }

                Logger.WriteLine($"[TorqueLimiter] Intervention #{_interventionCount}:");
                for (int i = 0; i < engines.Length; i++)
                {
                    if (overlimitEngines.Contains(i))
                    {
                        Logger.WriteLine($"  Engine {i + 1}: {torquePercents[i]:F1}% torque, throttle {currentThrottles[i]:F1}% → {recommendedThrottles[i]:F1}%");
                    }
                }

                LimitTriggered?.Invoke(this, new TorqueLimitEvent
                {
                    InterventionCount = _interventionCount,
                    CurrentTorquePercents = torquePercents,
                    CurrentThrottlePercents = currentThrottles,
                    RecommendedThrottlePercents = recommendedThrottles,
                    OverlimitEngines = overlimitEngines.ToArray()
                });
            }
        }
        else if (!anyOverLimit && _isLimiting)
        {
            _isLimiting = false;
            Logger.WriteLine($"[TorqueLimiter] All engines returned to safe levels after {_interventionCount} interventions");
            _interventionCount = 0;
        }
    }

    /// <summary>
    /// Calculates intelligent throttle reduction based on overtorque severity.
    ///
    /// Algorithm:
    /// 1. Calculate overtorque severity (how far over the limit we are)
    /// 2. Apply proportional response - more severe = larger reduction
    /// 3. Use configurable aggression factor for tuning
    /// 4. Ensure safe bounds (never reduce below minimum throttle)
    ///
    /// Example: If at 108% torque with 100% limit and 100% throttle:
    /// - Overtorque excess = 8%
    /// - With aggression 2.0x, reduction = 16%
    /// - New throttle = 100% - 16% = 84%
    /// </summary>
    private double CalculateThrottleReduction(double currentTorquePercent, double currentThrottlePercent, double targetTorquePercent)
    {
        // Calculate how far over the limit we are (in percentage points)
        double overtorqueExcess = currentTorquePercent - targetTorquePercent;

        // Proportional response: throttle reduction proportional to overtorque severity
        // Aggression factor determines how aggressively we reduce throttle
        // Higher values = more aggressive reduction
        double aggressionFactor = _settings.ThrottleReductionAggression;

        double throttleReduction = overtorqueExcess * aggressionFactor;

        // Calculate new throttle position
        double newThrottle = currentThrottlePercent - throttleReduction;

        // Safety bounds: never go below minimum or above maximum
        double minThrottle = _settings.MinThrottlePercent;
        newThrottle = Math.Max(minThrottle, Math.Min(100.0, newThrottle));

        return newThrottle;
    }

    public bool IsCurrentlyLimiting() => _isLimiting;

    public int GetInterventionCount() => _interventionCount;

    public void ResetInterventionCount()
    {
        _interventionCount = 0;
        Logger.WriteLine("[TorqueLimiter] Intervention count reset");
    }

    public void Dispose()
    {
        Logger.WriteLine("[TorqueLimiter] Torque limiter manager disposed");
    }
}
