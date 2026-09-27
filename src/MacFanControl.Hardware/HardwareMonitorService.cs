using LibreHardwareMonitor.Hardware;
using MacFanControl.Core.Interfaces;
using MacFanControl.Core.Models;
using MacFanControl.Core.Services;

namespace MacFanControl.Hardware;

public class HardwareMonitorService : ISensorService
{
    private Computer? _computer;
    private bool _isInitialized;
    private bool _useSimulation;
    private readonly Random _random = new();

    // Simulation baseline values (MBP 16" 2019 Core i9 + Radeon Pro)
    private float _simCpuTemp = 58f;
    private float _simGpuTemp = 54f;

    public bool IsAvailable => _isInitialized;

    public Task InitializeAsync()
    {
        DiagnosticLogger.Instance.Info("HardwareMonitorService initializing LibreHardwareMonitor...");
        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = true,
                IsMemoryEnabled = true
            };

            _computer.Open();
            _isInitialized = true;

            DiagnosticLogger.Instance.Info("LibreHardwareMonitor Computer opened successfully.");

            // Probe and log detected hardware
            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                DiagnosticLogger.Instance.Info($"Detected Hardware: [{hw.HardwareType}] {hw.Name} (Sensors: {hw.Sensors.Length})");
                foreach (var sensor in hw.Sensors)
                {
                    if (sensor.SensorType == SensorType.Temperature)
                    {
                        DiagnosticLogger.Instance.Debug($"  -> Sensor [{sensor.SensorType}] {sensor.Name}: {sensor.Value}°C");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _useSimulation = true;
            _isInitialized = true;
            DiagnosticLogger.Instance.Warn($"Could not initialize physical LibreHardwareMonitor drivers ({ex.Message}). Using Simulation Mode for development/testing.");
        }

        return Task.CompletedTask;
    }

    public HardwareOverview ReadHardwareOverview()
    {
        var overview = new HardwareOverview();

        if (_useSimulation || _computer == null)
        {
            _simCpuTemp += (float)(_random.NextDouble() * 2.0 - 1.0);
            _simCpuTemp = Math.Clamp(_simCpuTemp, 45f, 95f);

            _simGpuTemp += (float)(_random.NextDouble() * 1.6 - 0.8);
            _simGpuTemp = Math.Clamp(_simGpuTemp, 42f, 90f);

            overview.CpuPackageTemp = (float)Math.Round(_simCpuTemp, 1);
            overview.CpuMaxTemp = (float)Math.Round(_simCpuTemp + 4.5f, 1);
            overview.CpuUsagePercent = (float)Math.Round(15f + _random.NextDouble() * 20f, 1);
            overview.CpuPowerWatts = (float)Math.Round(28f + _random.NextDouble() * 15f, 1);

            overview.GpuTemp = (float)Math.Round(_simGpuTemp, 1);
            overview.GpuHotspotTemp = (float)Math.Round(_simGpuTemp + 6.0f, 1);
            overview.GpuUsagePercent = (float)Math.Round(8f + _random.NextDouble() * 12f, 1);
            overview.GpuPowerWatts = (float)Math.Round(18f + _random.NextDouble() * 10f, 1);

            return overview;
        }

        try
        {
            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();

                // CPU Monitoring (Intel Core i9-9880H)
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            if (sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))
                                overview.CpuPackageTemp = sensor.Value ?? overview.CpuPackageTemp;

                            if (sensor.Value > overview.CpuMaxTemp)
                                overview.CpuMaxTemp = sensor.Value ?? overview.CpuMaxTemp;
                        }
                        else if (sensor.SensorType == SensorType.Power && sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))
                        {
                            overview.CpuPowerWatts = sensor.Value ?? overview.CpuPowerWatts;
                        }
                        else if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase))
                        {
                            overview.CpuUsagePercent = sensor.Value ?? overview.CpuUsagePercent;
                        }
                    }
                }

                // GPU Monitoring (AMD Radeon Pro 5300M / 5500M / 5600M)
                if (hardware.HardwareType == HardwareType.GpuAmd || hardware.HardwareType == HardwareType.GpuIntel)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            if (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) || sensor.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase))
                                overview.GpuTemp = sensor.Value ?? overview.GpuTemp;

                            if (sensor.Name.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase))
                                overview.GpuHotspotTemp = sensor.Value ?? overview.GpuHotspotTemp;
                        }
                        else if (sensor.SensorType == SensorType.Power)
                        {
                            overview.GpuPowerWatts = sensor.Value ?? overview.GpuPowerWatts;
                        }
                        else if (sensor.SensorType == SensorType.Load && (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) || sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase)))
                        {
                            overview.GpuUsagePercent = sensor.Value ?? overview.GpuUsagePercent;
                        }
                    }
                }
            }
        }
        catch
        {
            // Graceful error recovery
        }

        return overview;
    }

    public IReadOnlyList<SensorInfo> GetAllSensors()
    {
        var list = new List<SensorInfo>();
        if (_computer == null) return list;

        try
        {
            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.Value.HasValue)
                    {
                        list.Add(new SensorInfo
                        {
                            Id = sensor.Identifier.ToString(),
                            Name = $"{hardware.Name} - {sensor.Name}",
                            Value = sensor.Value.Value,
                            Unit = sensor.SensorType == SensorType.Temperature ? "°C" :
                                   sensor.SensorType == SensorType.Power ? "W" :
                                   sensor.SensorType == SensorType.Load ? "%" : ""
                        });
                    }
                }
            }
        }
        catch { }

        return list;
    }

    public void Dispose()
    {
        _computer?.Close();
        _computer = null;
        GC.SuppressFinalize(this);
    }
}
