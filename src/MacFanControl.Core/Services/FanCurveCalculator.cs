using MacFanControl.Core.Models;

namespace MacFanControl.Core.Services;

public class FanCurveCalculator
{
    private float _lastAppliedTemperature = float.MinValue;
    private float _lastCalculatedPercentage = 0f;

    /// <summary>
    /// Calculates the target fan percentage based on the current temperature and profile curve points.
    /// Incorporates hysteresis to prevent fan noise oscillations.
    /// </summary>
    public float CalculateFanPercentage(float currentTemp, FanProfile profile)
    {
        if (profile == null || profile.Points == null || profile.Points.Count == 0)
        {
            return 50f; // Fallback safe speed
        }

        var sortedPoints = profile.Points.OrderBy(p => p.Temperature).ToList();

        // Apply hysteresis: only recalculate downward if temperature drops by more than HysteresisDegrees
        if (_lastAppliedTemperature > float.MinValue)
        {
            bool isDropping = currentTemp < _lastAppliedTemperature;
            if (isDropping && (_lastAppliedTemperature - currentTemp) < profile.HysteresisDegrees)
            {
                return _lastCalculatedPercentage;
            }
        }

        float targetPercentage;

        // If below lowest point
        if (currentTemp <= sortedPoints[0].Temperature)
        {
            targetPercentage = sortedPoints[0].FanPercentage;
        }
        // If above highest point
        else if (currentTemp >= sortedPoints[^1].Temperature)
        {
            targetPercentage = sortedPoints[^1].FanPercentage;
        }
        // Linear interpolation between the two surrounding points
        else
        {
            FanCurvePoint? lower = null;
            FanCurvePoint? upper = null;

            for (int i = 0; i < sortedPoints.Count - 1; i++)
            {
                if (currentTemp >= sortedPoints[i].Temperature && currentTemp <= sortedPoints[i + 1].Temperature)
                {
                    lower = sortedPoints[i];
                    upper = sortedPoints[i + 1];
                    break;
                }
            }

            if (lower != null && upper != null)
            {
                float tempDelta = upper.Temperature - lower.Temperature;
                if (tempDelta > 0.001f)
                {
                    float factor = (currentTemp - lower.Temperature) / tempDelta;
                    targetPercentage = lower.FanPercentage + factor * (upper.FanPercentage - lower.FanPercentage);
                }
                else
                {
                    targetPercentage = upper.FanPercentage;
                }
            }
            else
            {
                targetPercentage = 50f;
            }
        }

        _lastAppliedTemperature = currentTemp;
        _lastCalculatedPercentage = Math.Clamp(targetPercentage, 0f, 100f);
        return _lastCalculatedPercentage;
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
