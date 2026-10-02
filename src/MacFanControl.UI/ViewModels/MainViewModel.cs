using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MacFanControl.Core.Interfaces;
using MacFanControl.Core.Models;
using MacFanControl.Core.Services;
using MacFanControl.UI.Startup;
using MacFanControl.UI.Tray;

using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;

namespace MacFanControl.UI.ViewModels;

public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ISensorService _sensorService;
    private readonly ISmcService _smcService;
    private readonly DispatcherTimer _timer;

    private AppSettings _settings;
    private HardwareOverview _overview = new();
    private FanInfo _fan0 = new() { Index = 0, Name = "Left Fan (CPU)" };
    private FanInfo _fan1 = new() { Index = 1, Name = "Right Fan (GPU)" };

    private int _selectedTabIndex = 0; // 0=Fan Control, 1=Sensors, 2=Settings, 3=Logs
    private string _statusMessage = "Ready";
    private string _selectedSensorCategory = "All";
    private string _sensorSearchText = string.Empty;

    // Smoothed values to prevent jitter
    private float _smoothedLeftTemp = 0;
    private float _smoothedRightTemp = 0;
    private float _currentCommandedRpm0 = 0;
    private float _currentCommandedRpm1 = 0;
    private float _lastWrittenRpm0 = -1;
    private float _lastWrittenRpm1 = -1;

    private float _calculatedLeftRpm;
    private float _calculatedRightRpm;
    private float _calculatedLeftPercent;
    private float _calculatedRightPercent;

    public ObservableCollection<SensorInfo> AllSensors { get; } = new();
    public ObservableCollection<SensorInfo> DisplayedSensors { get; } = new();
    public ObservableCollection<LogEntry> Logs { get; } = new();

    public TrayIconManager? TrayManager { get; set; }

    public AppSettings Settings
    {
        get => _settings;
        private set { _settings = value; OnPropertyChanged(); }
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set { _selectedTabIndex = value; OnPropertyChanged(); }
    }

    public HardwareOverview Overview
    {
        get => _overview;
        private set { _overview = value; OnPropertyChanged(); }
    }

    public FanInfo Fan0
    {
        get => _fan0;
        private set { _fan0 = value; OnPropertyChanged(); }
    }

    public FanInfo Fan1
    {
        get => _fan1;
        private set { _fan1 = value; OnPropertyChanged(); }
    }

    public FanMode SelectedMode
    {
        get => _settings.CurrentMode;
        set
        {
            if (_settings.CurrentMode != value)
            {
                _settings.CurrentMode = value;
                OnPropertyChanged();
                SaveSettings();
                ApplyFanMode();
            }
        }
    }

    public bool LinkBothFans
    {
        get => _settings.LinkBothFans;
        set
        {
            if (_settings.LinkBothFans != value)
            {
                _settings.LinkBothFans = value;
                if (value)
                {
                    // Sync right curve to left curve when linked
                    _settings.RightFanCurve.MinTemp = _settings.LeftFanCurve.MinTemp;
                    _settings.RightFanCurve.MaxTemp = _settings.LeftFanCurve.MaxTemp;
                    _settings.RightFanCurve.MinFanPercent = _settings.LeftFanCurve.MinFanPercent;
                    _settings.RightFanCurve.MaxFanPercent = _settings.LeftFanCurve.MaxFanPercent;
                    OnPropertyChanged(nameof(RightMinTemp));
                    OnPropertyChanged(nameof(RightMaxTemp));
                    OnPropertyChanged(nameof(RightMinPercent));
                    OnPropertyChanged(nameof(RightMaxPercent));
                }
                OnPropertyChanged();
                SaveSettings();
            }
        }
    }

    public bool StartWithWindows
    {
        get => _settings.StartWithWindows;
        set
        {
            if (_settings.StartWithWindows != value)
            {
                _settings.StartWithWindows = value;
                OnPropertyChanged();
                TaskSchedulerHelper.SetStartup(value, _settings.StartMinimizedToTray);
                SaveSettings();
            }
        }
    }

    public bool StartMinimizedToTray
    {
        get => _settings.StartMinimizedToTray;
        set
        {
            if (_settings.StartMinimizedToTray != value)
            {
                _settings.StartMinimizedToTray = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OpenWindowOnStart));
                if (_settings.StartWithWindows)
                {
                    TaskSchedulerHelper.SetStartup(true, value);
                }
                SaveSettings();
            }
        }
    }

    public bool OpenWindowOnStart
    {
        get => !_settings.StartMinimizedToTray;
        set
        {
            StartMinimizedToTray = !value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StartMinimizedToTray));
        }
    }

    public bool MinimizeOnClose
    {
        get => _settings.MinimizeOnClose;
        set
        {
            if (_settings.MinimizeOnClose != value)
            {
                _settings.MinimizeOnClose = value;
                OnPropertyChanged();
                SaveSettings();
            }
        }
    }

    public bool ShowCpuTempTray
    {
        get => _settings.ShowCpuTempTray;
        set
        {
            if (_settings.ShowCpuTempTray != value)
            {
                _settings.ShowCpuTempTray = value;
                OnPropertyChanged();
                SaveSettings();
                UpdateTrayVisibility();
            }
        }
    }

    public bool ShowGpuTempTray
    {
        get => _settings.ShowGpuTempTray;
        set
        {
            if (_settings.ShowGpuTempTray != value)
            {
                _settings.ShowGpuTempTray = value;
                OnPropertyChanged();
                SaveSettings();
                UpdateTrayVisibility();
            }
        }
    }

    public bool ShowCpuUsageTray
    {
        get => _settings.ShowCpuUsageTray;
        set
        {
            if (_settings.ShowCpuUsageTray != value)
            {
                _settings.ShowCpuUsageTray = value;
                OnPropertyChanged();
                SaveSettings();
                UpdateTrayVisibility();
            }
        }
    }

    public bool ShowGpuUsageTray
    {
        get => _settings.ShowGpuUsageTray;
        set
        {
            if (_settings.ShowGpuUsageTray != value)
            {
                _settings.ShowGpuUsageTray = value;
                OnPropertyChanged();
                SaveSettings();
                UpdateTrayVisibility();
            }
        }
    }

    public void UpdateTrayVisibility()
    {
        TrayManager?.UpdateVisibility(
            _settings.ShowCpuTempTray,
            _settings.ShowGpuTempTray,
            _settings.ShowCpuUsageTray,
            _settings.ShowGpuUsageTray
        );
    }

    public bool EnableSmoothing
    {
        get => _settings.EnableSmoothing;
        set
        {
            if (_settings.EnableSmoothing != value)
            {
                _settings.EnableSmoothing = value;
                OnPropertyChanged();
                SaveSettings();
            }
        }
    }

    public float RampUpStepRpm
    {
        get => _settings.RampUpStepRpm;
        set
        {
            _settings.RampUpStepRpm = Math.Clamp(value, 50f, 600f);
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public float RampDownStepRpm
    {
        get => _settings.RampDownStepRpm;
        set
        {
            _settings.RampDownStepRpm = Math.Clamp(value, 20f, 300f);
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public int PollingIntervalMs
    {
        get => _settings.PollingIntervalMs;
        set
        {
            if (_settings.PollingIntervalMs != value && value >= 500)
            {
                _settings.PollingIntervalMs = value;
                _timer.Interval = TimeSpan.FromMilliseconds(value);
                OnPropertyChanged();
                SaveSettings();
            }
        }
    }

    // --- Left Fan Curve Properties ---
    public float LeftMinTemp
    {
        get => _settings.LeftFanCurve.MinTemp;
        set
        {
            _settings.LeftFanCurve.MinTemp = Math.Clamp(value, 20f, _settings.LeftFanCurve.MaxTemp - 5f);
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public float LeftMaxTemp
    {
        get => _settings.LeftFanCurve.MaxTemp;
        set
        {
            _settings.LeftFanCurve.MaxTemp = Math.Clamp(value, _settings.LeftFanCurve.MinTemp + 5f, 105f);
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public float LeftMinPercent
    {
        get => _settings.LeftFanCurve.MinFanPercent;
        set
        {
            _settings.LeftFanCurve.MinFanPercent = Math.Clamp(value, 0f, _settings.LeftFanCurve.MaxFanPercent);
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public float LeftMaxPercent
    {
        get => _settings.LeftFanCurve.MaxFanPercent;
        set
        {
            _settings.LeftFanCurve.MaxFanPercent = Math.Clamp(value, _settings.LeftFanCurve.MinFanPercent, 100f);
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public string LeftSensorName
    {
        get => _settings.LeftFanCurve.SensorName;
        set
        {
            _settings.LeftFanCurve.SensorName = value;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    // --- Right Fan Curve Properties ---
    public float RightMinTemp
    {
        get => _settings.RightFanCurve.MinTemp;
        set
        {
            _settings.RightFanCurve.MinTemp = Math.Clamp(value, 20f, _settings.RightFanCurve.MaxTemp - 5f);
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public float RightMaxTemp
    {
        get => _settings.RightFanCurve.MaxTemp;
        set
        {
            _settings.RightFanCurve.MaxTemp = Math.Clamp(value, _settings.RightFanCurve.MinTemp + 5f, 105f);
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public float RightMinPercent
    {
        get => _settings.RightFanCurve.MinFanPercent;
        set
        {
            _settings.RightFanCurve.MinFanPercent = Math.Clamp(value, 0f, _settings.RightFanCurve.MaxFanPercent);
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public float RightMaxPercent
    {
        get => _settings.RightFanCurve.MaxFanPercent;
        set
        {
            _settings.RightFanCurve.MaxFanPercent = Math.Clamp(value, _settings.RightFanCurve.MinFanPercent, 100f);
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public string RightSensorName
    {
        get => _settings.RightFanCurve.SensorName;
        set
        {
            _settings.RightFanCurve.SensorName = value;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    // --- Status and Preview Bindings ---
    public float CalculatedLeftRpm
    {
        get => _calculatedLeftRpm;
        private set { _calculatedLeftRpm = value; OnPropertyChanged(); }
    }

    public float CalculatedRightRpm
    {
        get => _calculatedRightRpm;
        private set { _calculatedRightRpm = value; OnPropertyChanged(); }
    }

    public float CalculatedLeftPercent
    {
        get => _calculatedLeftPercent;
        private set { _calculatedLeftPercent = value; OnPropertyChanged(); }
    }

    public float CalculatedRightPercent
    {
        get => _calculatedRightPercent;
        private set { _calculatedRightPercent = value; OnPropertyChanged(); }
    }

    public float SmoothedLeftTemp
    {
        get => _smoothedLeftTemp;
        private set { _smoothedLeftTemp = value; OnPropertyChanged(); }
    }

    public float SmoothedRightTemp
    {
        get => _smoothedRightTemp;
        private set { _smoothedRightTemp = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public string SelectedSensorCategory
    {
        get => _selectedSensorCategory;
        set
        {
            _selectedSensorCategory = value;
            OnPropertyChanged();
            FilterSensors();
        }
    }

    public string SensorSearchText
    {
        get => _sensorSearchText;
        set
        {
            _sensorSearchText = value;
            OnPropertyChanged();
            FilterSensors();
        }
    }

    public string DeviceModel => _smcService.DeviceModel;
    public bool IsSMCConnected => _smcService.IsConnected;

    // Commands
    public ICommand SetAppleAutoCommand { get; }
    public ICommand SetCurveCommand { get; }
    public ICommand SetTurboCommand { get; }
    public ICommand SwitchTabCommand { get; }
    public ICommand ApplyPresetCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand ResetSettingsCommand { get; }
    public ICommand ExportLogsCommand { get; }
    public ICommand RunDiagnosticCommand { get; }
    public ICommand CopyLogsCommand { get; }

    // Numeric Stepper Commands (▲ / ▼)
    public ICommand ChangeLeftMinTempCommand { get; }
    public ICommand ChangeLeftMinPercentCommand { get; }
    public ICommand ChangeLeftMaxTempCommand { get; }
    public ICommand ChangeLeftMaxPercentCommand { get; }

    public ICommand ChangeRightMinTempCommand { get; }
    public ICommand ChangeRightMinPercentCommand { get; }
    public ICommand ChangeRightMaxTempCommand { get; }
    public ICommand ChangeRightMaxPercentCommand { get; }

    public MainViewModel(ISensorService sensorService, ISmcService smcService)
    {
        _sensorService = sensorService;
        _smcService = smcService;

        _settings = SettingsService.Instance.Load();

        // Ensure Left Fan migrates to CPU Core Average on first run with this update
        if (!_settings.MigratedToCoreAverage)
        {
            _settings.LeftFanCurve.SensorName = "CPU Core Average";
            _settings.MigratedToCoreAverage = true;
            SaveSettings();
        }

        // Check if Task Scheduler is currently active
        _settings.StartWithWindows = TaskSchedulerHelper.IsStartupEnabled();

        // Populate initial diagnostic logs
        foreach (var log in DiagnosticLogger.Instance.GetRecentLogs())
        {
            Logs.Add(log);
        }

        DiagnosticLogger.Instance.OnLogAdded += entry =>
        {
            Application.Current?.Dispatcher?.BeginInvoke(() =>
            {
                Logs.Add(entry);
                if (Logs.Count > 1000)
                    Logs.RemoveAt(0);
            });
        };

        SetAppleAutoCommand = new RelayCommand(() => SelectedMode = FanMode.AppleAuto);
        SetCurveCommand = new RelayCommand(() => SelectedMode = FanMode.Curve);
        SetTurboCommand = new RelayCommand(() => SelectedMode = FanMode.Turbo);

        SwitchTabCommand = new RelayCommand(p =>
        {
            if (p is string s && int.TryParse(s, out int idx))
            {
                SelectedTabIndex = idx;
            }
        });

        // Numeric Stepper Handlers (+1/-1 for °C, +5/-5 for %)
        ChangeLeftMinTempCommand = new RelayCommand(p =>
        {
            if (float.TryParse(p?.ToString(), out float delta))
                LeftMinTemp = (float)Math.Round(LeftMinTemp + delta);
        });
        ChangeLeftMinPercentCommand = new RelayCommand(p =>
        {
            if (float.TryParse(p?.ToString(), out float delta))
                LeftMinPercent = (float)Math.Round(LeftMinPercent + delta);
        });
        ChangeLeftMaxTempCommand = new RelayCommand(p =>
        {
            if (float.TryParse(p?.ToString(), out float delta))
                LeftMaxTemp = (float)Math.Round(LeftMaxTemp + delta);
        });
        ChangeLeftMaxPercentCommand = new RelayCommand(p =>
        {
            if (float.TryParse(p?.ToString(), out float delta))
                LeftMaxPercent = (float)Math.Round(LeftMaxPercent + delta);
        });

        ChangeRightMinTempCommand = new RelayCommand(p =>
        {
            if (float.TryParse(p?.ToString(), out float delta))
                RightMinTemp = (float)Math.Round(RightMinTemp + delta);
        });
        ChangeRightMinPercentCommand = new RelayCommand(p =>
        {
            if (float.TryParse(p?.ToString(), out float delta))
                RightMinPercent = (float)Math.Round(RightMinPercent + delta);
        });
        ChangeRightMaxTempCommand = new RelayCommand(p =>
        {
            if (float.TryParse(p?.ToString(), out float delta))
                RightMaxTemp = (float)Math.Round(RightMaxTemp + delta);
        });
        ChangeRightMaxPercentCommand = new RelayCommand(p =>
        {
            if (float.TryParse(p?.ToString(), out float delta))
                RightMaxPercent = (float)Math.Round(RightMaxPercent + delta);
        });

        ApplyPresetCommand = new RelayCommand(p =>
        {
            if (p is string preset)
            {
                ApplyPreset(preset);
            }
        });

        SaveSettingsCommand = new RelayCommand(() =>
        {
            SaveSettings();
            StatusMessage = "Settings successfully saved!";
        });

        ResetSettingsCommand = new RelayCommand(ResetToDefaults);
        ExportLogsCommand = new RelayCommand(ExportLogs);
        RunDiagnosticCommand = new RelayCommand(RunDiagnostic);
        CopyLogsCommand = new RelayCommand(CopyLogsToClipboard);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Math.Max(500, _settings.PollingIntervalMs))
        };
        _timer.Tick += (s, e) => UpdateHardwareAndFans();
    }

    public async Task StartAsync()
    {
        await _sensorService.InitializeAsync();
        UpdateHardwareAndFans();
        _timer.Start();
        StatusMessage = "Sensors and SMC active";
    }

    private void SaveSettings()
    {
        SettingsService.Instance.Save(_settings);
    }

    private void ApplyPreset(string presetName)
    {
        switch (presetName.ToLowerInvariant())
        {
            case "quiet":
                LeftMinTemp = 55f;
                LeftMaxTemp = 88f;
                LeftMinPercent = 20f;
                LeftMaxPercent = 85f;
                RightMinTemp = 55f;
                RightMaxTemp = 85f;
                RightMinPercent = 20f;
                RightMaxPercent = 85f;
                StatusMessage = "Applied Quiet (Office) Preset";
                break;

            case "balanced":
                LeftMinTemp = 50f;
                LeftMaxTemp = 82f;
                LeftMinPercent = 25f;
                LeftMaxPercent = 95f;
                RightMinTemp = 50f;
                RightMaxTemp = 80f;
                RightMinPercent = 25f;
                RightMaxPercent = 95f;
                StatusMessage = "Applied Balanced Preset";
                break;

            case "gaming":
                LeftMinTemp = 45f;
                LeftMaxTemp = 75f;
                LeftMinPercent = 35f;
                LeftMaxPercent = 100f;
                RightMinTemp = 45f;
                RightMaxTemp = 72f;
                RightMinPercent = 35f;
                RightMaxPercent = 100f;
                StatusMessage = "Applied Gaming & Rendering Preset";
                break;
        }

        SaveSettings();
    }

    private void ResetToDefaults()
    {
        _settings = new AppSettings();
        OnPropertyChanged(string.Empty);
        TaskSchedulerHelper.SetStartup(_settings.StartWithWindows, _settings.StartMinimizedToTray);
        SaveSettings();
        StatusMessage = "Settings restored to factory defaults";
    }

    private void UpdateHardwareAndFans()
    {
        Overview = _sensorService.ReadHardwareOverview();

        // Refresh all hardware sensors list
        var sensors = _sensorService.GetAllSensors();
        AllSensors.Clear();
        foreach (var s in sensors)
        {
            AllSensors.Add(s);
        }
        FilterSensors();

        // Query SMC Fan speeds
        Fan0 = _smcService.GetFanInfo(0);
        Fan1 = _smcService.GetFanInfo(1);

        // Update Tray Icons with CPU/GPU Temperatures & CPU/GPU Usage/Load
        TrayManager?.UpdateTrayIcons(
            Overview.CpuMaxTemp,
            Overview.GpuHotspotTemp,
            Overview.CpuUsagePercent,
            Overview.GpuUsagePercent);

        // Handle Fan Controls
        if (SelectedMode == FanMode.Curve)
        {
            EvaluateCurveLogic();
        }
    }

    private void FilterSensors()
    {
        DisplayedSensors.Clear();
        foreach (var s in AllSensors)
        {
            bool matchCategory = SelectedSensorCategory == "All" ||
                                 s.Category.Equals(SelectedSensorCategory, StringComparison.OrdinalIgnoreCase) ||
                                 (SelectedSensorCategory == "Temperatures" && s.Unit == "°C") ||
                                 (SelectedSensorCategory == "Loads" && s.Unit == "%") ||
                                 (SelectedSensorCategory == "Power" && s.Unit == "W");

            bool matchSearch = string.IsNullOrWhiteSpace(SensorSearchText) ||
                               s.Name.Contains(SensorSearchText, StringComparison.OrdinalIgnoreCase);

            if (matchCategory && matchSearch)
            {
                DisplayedSensors.Add(s);
            }
        }
    }

    private void EvaluateCurveLogic()
    {
        // 1. Determine raw temperatures
        float rawLeftTemp = GetSensorTemperature(_settings.LeftFanCurve.SensorName, Overview.CpuPackageTemp);
        float rawRightTemp = GetSensorTemperature(_settings.RightFanCurve.SensorName, Overview.GpuTemp);

        if (LinkBothFans)
        {
            // When linked, both use the maximum temperature of CPU & GPU
            float highestTemp = Math.Max(rawLeftTemp, rawRightTemp);
            rawLeftTemp = highestTemp;
            rawRightTemp = highestTemp;
        }

        // 2. Apply Anti-Jitter EMA Temperature Filter
        if (EnableSmoothing)
        {
            SmoothedLeftTemp = FanCurveCalculator.FilterTemperature(rawLeftTemp, SmoothedLeftTemp);
            SmoothedRightTemp = FanCurveCalculator.FilterTemperature(rawRightTemp, SmoothedRightTemp);
        }
        else
        {
            SmoothedLeftTemp = rawLeftTemp;
            SmoothedRightTemp = rawRightTemp;
        }

        // 3. Compute target percentages
        CalculatedLeftPercent = FanCurveCalculator.CalculatePercentageFromCurve(SmoothedLeftTemp, _settings.LeftFanCurve);
        CalculatedRightPercent = FanCurveCalculator.CalculatePercentageFromCurve(SmoothedRightTemp, _settings.RightFanCurve);

        float desiredRpm0 = FanCurveCalculator.PercentageToRpm(CalculatedLeftPercent, Fan0.MinRpm, Fan0.MaxRpm);
        float desiredRpm1 = FanCurveCalculator.PercentageToRpm(CalculatedRightPercent, Fan1.MinRpm, Fan1.MaxRpm);

        if (LinkBothFans)
        {
            float maxRpm = Math.Max(desiredRpm0, desiredRpm1);
            desiredRpm0 = maxRpm;
            desiredRpm1 = maxRpm;
        }

        // 4. Apply RPM Slew-Rate Limiting to eliminate jerkiness
        if (EnableSmoothing)
        {
            _currentCommandedRpm0 = FanCurveCalculator.SlewRateLimitRpm(desiredRpm0, _currentCommandedRpm0, RampUpStepRpm, RampDownStepRpm);
            _currentCommandedRpm1 = FanCurveCalculator.SlewRateLimitRpm(desiredRpm1, _currentCommandedRpm1, RampUpStepRpm, RampDownStepRpm);
        }
        else
        {
            _currentCommandedRpm0 = desiredRpm0;
            _currentCommandedRpm1 = desiredRpm1;
        }

        CalculatedLeftRpm = _currentCommandedRpm0;
        CalculatedRightRpm = _currentCommandedRpm1;

        // 5. Send to SMC only if delta exceeds deadband (30 RPM) to prevent needless IOCTL spam
        if (Math.Abs(_currentCommandedRpm0 - _lastWrittenRpm0) >= 30)
        {
            _smcService.SetFanSpeed(0, _currentCommandedRpm0);
            _lastWrittenRpm0 = _currentCommandedRpm0;
        }

        if (Math.Abs(_currentCommandedRpm1 - _lastWrittenRpm1) >= 30)
        {
            _smcService.SetFanSpeed(1, _currentCommandedRpm1);
            _lastWrittenRpm1 = _currentCommandedRpm1;
        }

        StatusMessage = $"Curve Active: Left {CalculatedLeftRpm:F0} RPM ({CalculatedLeftPercent:F0}%) | Right {CalculatedRightRpm:F0} RPM ({CalculatedRightPercent:F0}%)";
    }

    private float GetSensorTemperature(string sensorName, float defaultTemp)
    {
        if (string.IsNullOrWhiteSpace(sensorName))
            return defaultTemp;

        // 1. CPU Core Average (Trung bình các nhân CPU)
        if (sensorName.Contains("Average", StringComparison.OrdinalIgnoreCase) || 
            sensorName.Contains("Trung bình", StringComparison.OrdinalIgnoreCase))
        {
            var coreSensors = AllSensors
                .Where(s => s.Category == "CPU" && s.Unit == "°C" && s.Name.Contains("Core #", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (coreSensors.Count > 0)
                return (float)Math.Round(coreSensors.Average(s => s.Value), 1);
            return Overview.CpuPackageTemp;
        }

        // 2. CPU Core Max (Nhân nóng nhất)
        if (sensorName.Contains("Max Core", StringComparison.OrdinalIgnoreCase) || 
            sensorName.Contains("Lõi nóng nhất", StringComparison.OrdinalIgnoreCase) ||
            sensorName.Contains("Nhân nóng nhất", StringComparison.OrdinalIgnoreCase))
        {
            var coreSensors = AllSensors
                .Where(s => s.Category == "CPU" && s.Unit == "°C" && s.Name.Contains("Core #", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (coreSensors.Count > 0)
                return coreSensors.Max(s => s.Value);
            return Overview.CpuMaxTemp;
        }

        // 3. Individual Core (Core #1, Core #2, ... Core #8)
        if (sensorName.Contains("Core #", StringComparison.OrdinalIgnoreCase))
        {
            var matchCore = AllSensors.FirstOrDefault(s => s.Category == "CPU" && s.Unit == "°C" && s.Name.Contains(sensorName, StringComparison.OrdinalIgnoreCase));
            if (matchCore != null)
                return matchCore.Value;
        }

        // 4. CPU Package
        if (sensorName.Contains("CPU Package", StringComparison.OrdinalIgnoreCase))
            return Overview.CpuPackageTemp;

        // 5. GPU Hot Spot
        if (sensorName.Contains("Hotspot", StringComparison.OrdinalIgnoreCase) || 
            sensorName.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase))
            return Overview.GpuHotspotTemp;

        // 6. GPU Core
        if (sensorName.Contains("GPU", StringComparison.OrdinalIgnoreCase))
            return Overview.GpuTemp;

        // 7. Highest (CPU / GPU)
        if (sensorName.Contains("Highest", StringComparison.OrdinalIgnoreCase) ||
            sensorName.Contains("Cao nhất", StringComparison.OrdinalIgnoreCase))
            return Math.Max(Overview.CpuPackageTemp, Overview.GpuTemp);

        // Fallback: match by full name in AllSensors
        var match = AllSensors.FirstOrDefault(s => s.Name.Contains(sensorName, StringComparison.OrdinalIgnoreCase) && s.Unit == "°C");
        return match?.Value ?? defaultTemp;
    }

    private void ApplyFanMode()
    {
        switch (SelectedMode)
        {
            case FanMode.AppleAuto:
                _smcService.RestoreAppleDefaults();
                StatusMessage = "Apple Default Automatic Control active";
                break;

            case FanMode.Turbo:
                _smcService.SetAllFansMode(FanMode.Turbo);
                StatusMessage = "Turbo Mode: 100% cooling power";
                break;

            case FanMode.Curve:
                _lastWrittenRpm0 = -1;
                _lastWrittenRpm1 = -1;
                EvaluateCurveLogic();
                break;
        }
    }

    private void RunDiagnostic()
    {
        StatusMessage = "Running Full Hardware & SMC Diagnostic...";
        _smcService.RunFullDiagnostic();
        StatusMessage = "Diagnostic completed. View or Export log below.";
    }

    private void ExportLogs()
    {
        try
        {
            string exportedPath = DiagnosticLogger.Instance.ExportToFile();
            StatusMessage = $"Exported to Desktop: {Path.GetFileName(exportedPath)}";
            Process.Start("explorer.exe", $"/select,\"{exportedPath}\"");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    private void CopyLogsToClipboard()
    {
        try
        {
            string allLogs = DiagnosticLogger.Instance.GetAllLogsAsText();
            Clipboard.SetText(allLogs);
            StatusMessage = "Diagnostic log copied to Clipboard!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Clipboard copy failed: {ex.Message}";
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _smcService.RestoreAppleDefaults();
        _smcService.Dispose();
        _sensorService.Dispose();
        GC.SuppressFinalize(this);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
