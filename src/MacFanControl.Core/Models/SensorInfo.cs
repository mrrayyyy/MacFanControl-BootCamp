namespace MacFanControl.Core.Models;

public class SensorInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public float Value { get; set; }
    public string Unit { get; set; } = "°C";
    public float MinValue { get; set; } = 0;
    public float MaxValue { get; set; } = 100;
}

public class HardwareOverview
{
    public float CpuPackageTemp { get; set; }
    public float CpuMaxTemp { get; set; }
    public float CpuPowerWatts { get; set; }
    public float CpuUsagePercent { get; set; }

    public float GpuTemp { get; set; }
    public float GpuHotspotTemp { get; set; }
    public float GpuPowerWatts { get; set; }
    public float GpuUsagePercent { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.Now;
}
