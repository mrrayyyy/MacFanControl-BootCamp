namespace MacFanControl.Core.Models;

public class SensorInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "CPU"; // "CPU", "GPU", "Motherboard", "Memory", "Storage"
    public string SensorType { get; set; } = "Temperature"; // "Temperature", "Power", "Load", "Clock"
    public float Value { get; set; }
    public string Unit { get; set; } = "°C";
    public float MinValue { get; set; } = 0;
    public float MaxValue { get; set; } = 100;
    public float MinRecorded { get; set; } = float.MaxValue;
    public float MaxRecorded { get; set; } = float.MinValue;

    public string FormattedValue => Unit switch
    {
        "°C" => $"{Value:F1} °C",
        "%" => $"{Value:F0} %",
        "W" => $"{Value:F1} W",
        "MHz" => $"{Value:F0} MHz",
        _ => $"{Value:F1} {Unit}"
    };

    public string FormattedMin => MinRecorded < float.MaxValue ? $"{MinRecorded:F1} {Unit}" : "--";
    public string FormattedMax => MaxRecorded > float.MinValue ? $"{MaxRecorded:F1} {Unit}" : "--";

    public float ValueRatio
    {
        get
        {
            if (MaxValue <= MinValue) return 0;
            return Math.Clamp((Value - MinValue) / (MaxValue - MinValue), 0f, 1f);
        }
    }
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
