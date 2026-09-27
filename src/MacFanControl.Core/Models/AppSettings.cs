namespace MacFanControl.Core.Models;

public class AppSettings
{
    public bool StartWithWindows { get; set; } = true;
    public bool StartMinimizedToTray { get; set; } = true;
    public bool MinimizeOnClose { get; set; } = true;
    public int PollingIntervalMs { get; set; } = 1500;
    public bool LinkBothFans { get; set; } = true; // Cả quạt CPU và GPU chạy đồng tốc
    public FanMode CurrentMode { get; set; } = FanMode.Curve;
    public float ManualTargetRpm { get; set; } = 3500;
    public FanProfile ActiveProfile { get; set; } = FanProfile.CreateDefaultAggressive();
}
