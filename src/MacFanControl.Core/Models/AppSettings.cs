namespace MacFanControl.Core.Models;

public class FanCurveConfig
{
    public string SensorName { get; set; } = "CPU Package";
    public float MinTemp { get; set; } = 50f;
    public float MaxTemp { get; set; } = 85f;
    public float MinFanPercent { get; set; } = 25f;
    public float MaxFanPercent { get; set; } = 100f;

    public FanCurveConfig Clone()
    {
        return new FanCurveConfig
        {
            SensorName = SensorName,
            MinTemp = MinTemp,
            MaxTemp = MaxTemp,
            MinFanPercent = MinFanPercent,
            MaxFanPercent = MaxFanPercent
        };
    }
}

public class AppSettings
{
    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimizedToTray { get; set; } = true;
    public bool MinimizeOnClose { get; set; } = true;
    public int PollingIntervalMs { get; set; } = 1500;
    public bool LinkBothFans { get; set; } = true;
    public FanMode CurrentMode { get; set; } = FanMode.Curve;
    public float ManualTargetRpm { get; set; } = 3500;

    // Independent Temperature-Based Control Configurations
    public FanCurveConfig LeftFanCurve { get; set; } = new()
    {
        SensorName = "CPU Package",
        MinTemp = 50f,
        MaxTemp = 85f,
        MinFanPercent = 25f,
        MaxFanPercent = 100f
    };

    public FanCurveConfig RightFanCurve { get; set; } = new()
    {
        SensorName = "GPU Core",
        MinTemp = 50f,
        MaxTemp = 80f,
        MinFanPercent = 25f,
        MaxFanPercent = 100f
    };

    // Smooth & Anti-Jitter Settings
    public bool EnableSmoothing { get; set; } = true;
    public float RampUpStepRpm { get; set; } = 200f;
    public float RampDownStepRpm { get; set; } = 80f;
}
