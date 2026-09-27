using MacFanControl.Core.Models;

namespace MacFanControl.Core.Services;

public class FanCurveCalculator
{
    /// <summary>
    /// Smooths temperature reading using asymmetric Exponential Moving Average (EMA).
    /// Ramps up promptly for sustained heat, but cools down smoothly to prevent fan pulsation.
    /// </summary>
    public static float FilterTemperature(float rawTemp, float previousSmoothed, float alphaUp = 0.25f, float alphaDown = 0.12f)
    {
        if (previousSmoothed <= 0 || float.IsNaN(previousSmoothed))
            return rawTemp;

        if (rawTemp >= previousSmoothed)
        {
            // Temperature rising: respond smoothly but securely
            return previousSmoothed * (1f - alphaUp) + rawTemp * alphaUp;
        }
        else
        {
            // Temperature dropping: cool down gradually to avoid jerky drops
            return previousSmoothed * (1f - alphaDown) + rawTemp * alphaDown;
        }
    }

    /// <summary>
    /// Calculates the target fan percentage based on temperature and a 2-point linear Min/Max curve.
    /// </summary>
    public static float CalculatePercentageFromCurve(float currentTemp, FanCurveConfig curve)
    {
        if (curve.MaxTemp <= curve.MinTemp)
        {
            return curve.MinFanPercent;
        }

        if (currentTemp <= curve.MinTemp)
        {
            return Math.Clamp(curve.MinFanPercent, 0f, 100f);
        }

        if (currentTemp >= curve.MaxTemp)
        {
            return Math.Clamp(curve.MaxFanPercent, 0f, 100f);
        }

        float factor = (currentTemp - curve.MinTemp) / (curve.MaxTemp - curve.MinTemp);
        float percentage = curve.MinFanPercent + factor * (curve.MaxFanPercent - curve.MinFanPercent);
        return Math.Clamp(percentage, 0f, 100f);
    }

    /// <summary>
    /// Slew-rate limiter for fan RPM: ensures fan speed changes smoothly without acoustic spikes.
    /// </summary>
    public static float SlewRateLimitRpm(float targetRpm, float currentCommandedRpm, float maxStepUp, float maxStepDown)
    {
        if (currentCommandedRpm <= 0)
            return targetRpm;

        float diff = targetRpm - currentCommandedRpm;

        if (diff > 0)
        {
            // Ramping up
            return currentCommandedRpm + Math.Min(diff, maxStepUp);
        }
        else
        {
            // Ramping down
            return currentCommandedRpm - Math.Min(-diff, maxStepDown);
        }
    }

    /// <summary>
    /// Converts a percentage (0..100) to actual RPM based on min and max RPM.
    /// </summary>
    public static float PercentageToRpm(float percentage, float minRpm, float maxRpm)
    {
        percentage = Math.Clamp(percentage, 0f, 100f);
        return minRpm + (percentage / 100f) * (maxRpm - minRpm);
    }
}
