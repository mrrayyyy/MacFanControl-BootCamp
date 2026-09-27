namespace MacFanControl.Core.Models;

public class FanInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public float CurrentRpm { get; set; }
    public float TargetRpm { get; set; }
    public float MinRpm { get; set; } = 1800;
    public float MaxRpm { get; set; } = 5616;
    public FanMode Mode { get; set; } = FanMode.AppleAuto;

    public float Percentage
    {
        get
        {
            if (MaxRpm <= MinRpm) return 0;
            var clamped = Math.Clamp(CurrentRpm, MinRpm, MaxRpm);
            return (clamped - MinRpm) / (MaxRpm - MinRpm) * 100f;
        }
    }

    public float TargetPercentage
    {
        get
        {
            if (MaxRpm <= MinRpm) return 0;
            var clamped = Math.Clamp(TargetRpm, MinRpm, MaxRpm);
            return (clamped - MinRpm) / (MaxRpm - MinRpm) * 100f;
        }
    }
}
