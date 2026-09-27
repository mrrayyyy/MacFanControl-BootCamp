namespace MacFanControl.Core.Models;

public class FanCurvePoint
{
    public float Temperature { get; set; }
    public float FanPercentage { get; set; }

    public FanCurvePoint() { }

    public FanCurvePoint(float temp, float percent)
    {
        Temperature = temp;
        FanPercentage = percent;
    }
}

public class FanProfile
{
    public string Name { get; set; } = "Balanced";
    public SensorTarget TargetSensor { get; set; } = SensorTarget.MaxCpuGpu;
    public float HysteresisDegrees { get; set; } = 3.0f; // Tránh quạt rú lên giật xuống liên tục
    public List<FanCurvePoint> Points { get; set; } = new();

    public static FanProfile CreateDefaultAggressive()
    {
        return new FanProfile
        {
            Name = "Aggressive (Gaming & Rendering)",
            TargetSensor = SensorTarget.MaxCpuGpu,
            HysteresisDegrees = 3.0f,
            Points = new List<FanCurvePoint>
            {
                new(45, 20), // 45°C: 20%
                new(60, 45), // 60°C: 45%
                new(70, 70), // 70°C: 70%
                new(80, 90), // 80°C: 90%
                new(85, 100) // 85°C+: 100% Turbo
            }
        };
    }

    public static FanProfile CreateDefaultSilent()
    {
        return new FanProfile
        {
            Name = "Quiet (Office & Media)",
            TargetSensor = SensorTarget.CpuPackage,
            HysteresisDegrees = 4.0f,
            Points = new List<FanCurvePoint>
            {
                new(50, 0),
                new(65, 30),
                new(75, 55),
                new(85, 80),
                new(95, 100)
            }
        };
    }
}
