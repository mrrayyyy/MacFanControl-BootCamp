using System.Collections.Concurrent;
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

    // Min / Max peaks cache
    private readonly ConcurrentDictionary<string, float> _minPeaks = new();
    private readonly ConcurrentDictionary<string, float> _maxPeaks = new();

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
                IsMemoryEnabled = true,
                IsStorageEnabled = true
            };

            _computer.Open();
            _isInitialized = true;

            DiagnosticLogger.Instance.Info("LibreHardwareMonitor Computer opened successfully.");

            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                DiagnosticLogger.Instance.Info($"Detected Hardware: [{hw.HardwareType}] {hw.Name} (Sensors: {hw.Sensors.Length})");
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
            _simCpuTemp += (float)(_random.NextDouble() * 1.6 - 0.8);
            _simCpuTemp = Math.Clamp(_simCpuTemp, 45f, 95f);

            _simGpuTemp += (float)(_random.NextDouble() * 1.4 - 0.7);
            _simGpuTemp = Math.Clamp(_simGpuTemp, 42f, 90f);

            overview.CpuPackageTemp = (float)Math.Round(_simCpuTemp, 1);
            overview.CpuMaxTemp = (float)Math.Round(_simCpuTemp + 3.8f, 1);
            overview.CpuUsagePercent = (float)Math.Round(15f + _random.NextDouble() * 15f, 1);
            overview.CpuPowerWatts = (float)Math.Round(26f + _random.NextDouble() * 12f, 1);

            overview.GpuTemp = (float)Math.Round(_simGpuTemp, 1);
            overview.GpuHotspotTemp = (float)Math.Round(_simGpuTemp + 5.5f, 1);
            overview.GpuUsagePercent = (float)Math.Round(8f + _random.NextDouble() * 10f, 1);
            overview.GpuPowerWatts = (float)Math.Round(16f + _random.NextDouble() * 8f, 1);

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
                    bool cpuTotalFound = false;
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
                        else if (sensor.SensorType == SensorType.Load)
                        {
                            if (sensor.Name.Equals("CPU Total", StringComparison.OrdinalIgnoreCase) || sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase))
                            {
                                overview.CpuUsagePercent = sensor.Value ?? overview.CpuUsagePercent;
                                cpuTotalFound = true;
                            }
                            else if (!cpuTotalFound && overview.CpuUsagePercent <= 0f)
                            {
                                overview.CpuUsagePercent = sensor.Value ?? overview.CpuUsagePercent;
                            }
                        }
                    }
                }

                // GPU Monitoring (AMD Radeon Pro 5300M / 5500M / 5600M / Intel Iris / Nvidia)
                if (hardware.HardwareType == HardwareType.GpuAmd || hardware.HardwareType == HardwareType.GpuIntel || hardware.HardwareType == HardwareType.GpuNvidia)
                {
                    bool gpuCoreFound = false;
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
                        else if (sensor.SensorType == SensorType.Load)
                        {
                            if (sensor.Name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase))
                            {
                                overview.GpuUsagePercent = sensor.Value ?? overview.GpuUsagePercent;
                                gpuCoreFound = true;
                            }
                            else if (!gpuCoreFound && (sensor.Name.Contains("3D", StringComparison.OrdinalIgnoreCase) || sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) || sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase)))
                            {
                                overview.GpuUsagePercent = sensor.Value ?? overview.GpuUsagePercent;
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Graceful error recovery
        }

        if (overview.CpuMaxTemp <= 0f && overview.CpuPackageTemp > 0f)
            overview.CpuMaxTemp = overview.CpuPackageTemp;

        if (overview.GpuHotspotTemp <= 0f && overview.GpuTemp > 0f)
            overview.GpuHotspotTemp = overview.GpuTemp;

        return overview;
    }

    public IReadOnlyList<SensorInfo> GetAllSensors()
    {
        var list = new List<SensorInfo>();

        if (_useSimulation || _computer == null)
        {
            return GenerateSimulatedSensors();
        }

        try
        {
            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();

                string category = hardware.HardwareType switch
                {
                    HardwareType.Cpu => "CPU",
                    HardwareType.GpuAmd or HardwareType.GpuIntel or HardwareType.GpuNvidia => "GPU",
                    HardwareType.Memory => "Memory",
                    HardwareType.Storage => "Storage",
                    HardwareType.Motherboard => "Motherboard",
                    _ => "System"
                };

                foreach (var sensor in hardware.Sensors)
                {
                    if (!sensor.Value.HasValue) continue;

                    float val = (float)Math.Round(sensor.Value.Value, 1);
                    string id = sensor.Identifier.ToString();

                    // Update peaks
                    float min = _minPeaks.AddOrUpdate(id, val, (_, currentMin) => Math.Min(currentMin, val));
                    float max = _maxPeaks.AddOrUpdate(id, val, (_, currentMax) => Math.Max(currentMax, val));

                    string unit = sensor.SensorType switch
                    {
                        SensorType.Temperature => "°C",
                        SensorType.Power => "W",
                        SensorType.Load => "%",
                        SensorType.Clock => "MHz",
                        SensorType.Fan => "RPM",
                        _ => ""
                    };

                    float maxScale = sensor.SensorType switch
                    {
                        SensorType.Temperature => 105f,
                        SensorType.Power => 125f,
                        SensorType.Load => 100f,
                        SensorType.Clock => 5000f,
                        SensorType.Fan => 6000f,
                        _ => 100f
                    };

                    list.Add(new SensorInfo
                    {
                        Id = id,
                        Name = $"{hardware.Name} • {sensor.Name}",
                        Category = category,
                        SensorType = sensor.SensorType.ToString(),
                        Value = val,
                        Unit = unit,
                        MinValue = 0,
                        MaxValue = maxScale,
                        MinRecorded = min,
                        MaxRecorded = max
                    });
                }
            }
        }
        catch
        {
            // Graceful handling
        }

        return list;
    }

    private List<SensorInfo> GenerateSimulatedSensors()
    {
        var list = new List<SensorInfo>();
        var overview = ReadHardwareOverview();

        void AddSim(string id, string name, string category, string type, float val, string unit, float maxScale)
        {
            val = (float)Math.Round(val, 1);
            float min = _minPeaks.AddOrUpdate(id, val, (_, currentMin) => Math.Min(currentMin, val));
            float max = _maxPeaks.AddOrUpdate(id, val, (_, currentMax) => Math.Max(currentMax, val));

            list.Add(new SensorInfo
            {
                Id = id,
                Name = name,
                Category = category,
                SensorType = type,
                Value = val,
                Unit = unit,
                MinValue = 0,
                MaxValue = maxScale,
                MinRecorded = min,
                MaxRecorded = max
            });
        }

        // CPU Sensors
        AddSim("/intelcpu/0/temp/0", "Intel Core i9-9880H • CPU Package", "CPU", "Temperature", overview.CpuPackageTemp, "°C", 105);
        AddSim("/intelcpu/0/temp/max", "Intel Core i9-9880H • CPU Max Core", "CPU", "Temperature", overview.CpuMaxTemp, "°C", 105);
        for (int i = 1; i <= 8; i++)
        {
            float coreTemp = overview.CpuPackageTemp - 2f + (i % 3);
            AddSim($"/intelcpu/0/temp/core{i}", $"Intel Core i9-9880H • Core #{i}", "CPU", "Temperature", coreTemp, "°C", 105);
        }
        AddSim("/intelcpu/0/power/0", "Intel Core i9-9880H • CPU Package Power", "CPU", "Power", overview.CpuPowerWatts, "W", 125);
        AddSim("/intelcpu/0/load/0", "Intel Core i9-9880H • CPU Total Load", "CPU", "Load", overview.CpuUsagePercent, "%", 100);
        AddSim("/intelcpu/0/clock/0", "Intel Core i9-9880H • Core Clocks (Avg)", "CPU", "Clock", 2600 + overview.CpuUsagePercent * 18, "MHz", 5000);

        // GPU Sensors
        AddSim("/amdgpu/0/temp/0", "AMD Radeon Pro 5500M • GPU Core", "GPU", "Temperature", overview.GpuTemp, "°C", 105);
        AddSim("/amdgpu/0/temp/hotspot", "AMD Radeon Pro 5500M • GPU Hot Spot", "GPU", "Temperature", overview.GpuHotspotTemp, "°C", 115);
        AddSim("/amdgpu/0/temp/memory", "AMD Radeon Pro 5500M • GPU Memory (VRAM)", "GPU", "Temperature", overview.GpuTemp - 3f, "°C", 105);
        AddSim("/amdgpu/0/power/0", "AMD Radeon Pro 5500M • GPU Power", "GPU", "Power", overview.GpuPowerWatts, "W", 100);
        AddSim("/amdgpu/0/load/0", "AMD Radeon Pro 5500M • GPU Core Load", "GPU", "Load", overview.GpuUsagePercent, "%", 100);

        // Motherboard & Storage
        AddSim("/nvme/0/temp/0", "Apple SSD NVMe • Drive Temperature", "Storage", "Temperature", 42.0f + (float)(_random.NextDouble() * 1.5), "°C", 85);
        AddSim("/ram/0/load/0", "Generic DDR4 • Memory Utilization", "Memory", "Load", 45f + (float)(_random.NextDouble() * 2.0), "%", 100);
        AddSim("/smc/ambient/0", "Apple T2 SMC • Ambient Enclosure Proximity", "Motherboard", "Temperature", 38.5f + (float)(_random.NextDouble() * 1.0), "°C", 75);

        return list;
    }

    public void Dispose()
    {
        _computer?.Close();
        _computer = null;
        GC.SuppressFinalize(this);
    }
}
