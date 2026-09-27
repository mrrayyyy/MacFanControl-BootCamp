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

namespace MacFanControl.UI.ViewModels;

public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ISensorService _sensorService;
    private readonly ISmcService _smcService;
    private readonly FanCurveCalculator _curveCalculator;
    private readonly DispatcherTimer _timer;

    private HardwareOverview _overview = new();
    private FanInfo _fan0 = new() { Index = 0, Name = "Left Fan (CPU)" };
    private FanInfo _fan1 = new() { Index = 1, Name = "Right Fan (GPU)" };

    private FanMode _selectedMode = FanMode.Curve;
    private FanProfile _activeProfile = FanProfile.CreateDefaultAggressive();
    private float _manualTargetRpm = 3500;
    private bool _linkBothFans = true;
    private bool _startWithWindows;
    private string _statusMessage = "Ready";
    private int _selectedTabIndex = 0; // 0 = Dashboard, 1 = Log Center

    public ObservableCollection<LogEntry> Logs { get; } = new();

    public TrayIconManager? TrayManager { get; set; }

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
        get => _selectedMode;
        set
        {
            if (_selectedMode != value)
            {
                _selectedMode = value;
                OnPropertyChanged();
                ApplyFanMode();
            }
        }
    }

    public FanProfile ActiveProfile
    {
        get => _activeProfile;
        set { _activeProfile = value; OnPropertyChanged(); }
    }

    public float ManualTargetRpm
    {
        get => _manualTargetRpm;
        set
        {
            _manualTargetRpm = (float)Math.Round(value);
            OnPropertyChanged();
            if (SelectedMode == FanMode.Manual)
            {
                ApplyManualSpeed();
            }
        }
    }

    public bool LinkBothFans
    {
        get => _linkBothFans;
        set { _linkBothFans = value; OnPropertyChanged(); }
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (_startWithWindows != value)
            {
                _startWithWindows = value;
                OnPropertyChanged();
                TaskSchedulerHelper.SetStartup(value);
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public string DeviceModel => _smcService.DeviceModel;
    public bool IsSMCConnected => _smcService.IsConnected;

    // Commands
    public ICommand SetAppleAutoCommand { get; }
    public ICommand SetManualCommand { get; }
    public ICommand SetCurveCommand { get; }
    public ICommand SetTurboCommand { get; }
    public ICommand SelectProfileCommand { get; }
    public ICommand ExportLogsCommand { get; }
    public ICommand RunDiagnosticCommand { get; }
    public ICommand CopyLogsCommand { get; }
    public ICommand SwitchTabCommand { get; }

    public MainViewModel(ISensorService sensorService, ISmcService smcService)
    {
        _sensorService = sensorService;
        _smcService = smcService;
        _curveCalculator = new FanCurveCalculator();

        _startWithWindows = TaskSchedulerHelper.IsStartupEnabled();

        // Populate initial logs
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
        SetManualCommand    = new RelayCommand(() => SelectedMode = FanMode.Manual);
        SetCurveCommand     = new RelayCommand(() => SelectedMode = FanMode.Curve);
        SetTurboCommand     = new RelayCommand(() => SelectedMode = FanMode.Turbo);

        SelectProfileCommand = new RelayCommand(p =>
        {
            if (p is string profileName)
            {
                if (profileName.Contains("Aggressive", StringComparison.OrdinalIgnoreCase))
                    ActiveProfile = FanProfile.CreateDefaultAggressive();
                else
                    ActiveProfile = FanProfile.CreateDefaultSilent();
            }
        });

        ExportLogsCommand = new RelayCommand(ExportLogs);
        RunDiagnosticCommand = new RelayCommand(RunDiagnostic);
        CopyLogsCommand = new RelayCommand(CopyLogsToClipboard);
        SwitchTabCommand = new RelayCommand(param =>
        {
            if (param is string tabIndexStr && int.TryParse(tabIndexStr, out int idx))
            {
                SelectedTabIndex = idx;
            }
        });

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1500)
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

    private void UpdateHardwareAndFans()
    {
        Overview = _sensorService.ReadHardwareOverview();

        Fan0 = _smcService.GetFanInfo(0);
        Fan1 = _smcService.GetFanInfo(1);

        TrayManager?.UpdateTemperatureIcon(Overview.CpuPackageTemp);

        if (SelectedMode == FanMode.Curve)
        {
            EvaluateCurveLogic();
        }
    }

    private void EvaluateCurveLogic()
    {
        float targetTemp = ActiveProfile.TargetSensor switch
        {
            SensorTarget.CpuPackage => Overview.CpuPackageTemp,
            SensorTarget.CpuMaxCore => Overview.CpuMaxTemp,
            SensorTarget.GpuCore => Overview.GpuTemp,
            SensorTarget.MaxCpuGpu => Math.Max(Overview.CpuPackageTemp, Overview.GpuTemp),
            _ => Overview.CpuPackageTemp
        };

        float targetPercent = _curveCalculator.CalculateFanPercentage(targetTemp, ActiveProfile);
        float targetRpm = FanCurveCalculator.PercentageToRpm(targetPercent, Fan0.MinRpm, Fan0.MaxRpm);

        if (LinkBothFans)
        {
            _smcService.SetFanSpeed(0, targetRpm);
            _smcService.SetFanSpeed(1, targetRpm);
        }
        else
        {
            float cpuPercent = _curveCalculator.CalculateFanPercentage(Overview.CpuPackageTemp, ActiveProfile);
            float gpuPercent = _curveCalculator.CalculateFanPercentage(Overview.GpuTemp, ActiveProfile);

            _smcService.SetFanSpeed(0, FanCurveCalculator.PercentageToRpm(cpuPercent, Fan0.MinRpm, Fan0.MaxRpm));
            _smcService.SetFanSpeed(1, FanCurveCalculator.PercentageToRpm(gpuPercent, Fan1.MinRpm, Fan1.MaxRpm));
        }

        StatusMessage = $"Curve: {targetTemp:F1}°C -> {targetRpm:F0} RPM ({targetPercent:F0}%)";
    }

    private void ApplyFanMode()
    {
        switch (SelectedMode)
        {
            case FanMode.AppleAuto:
                _smcService.RestoreAppleDefaults();
                StatusMessage = "Apple Default Automatic Control restored";
                break;

            case FanMode.Turbo:
                _smcService.SetAllFansMode(FanMode.Turbo);
                StatusMessage = "Turbo Mode: 100% cooling power";
                break;

            case FanMode.Manual:
                ApplyManualSpeed();
                break;

            case FanMode.Curve:
                EvaluateCurveLogic();
                break;
        }
    }

    private void ApplyManualSpeed()
    {
        _smcService.SetAllFansMode(FanMode.Manual, ManualTargetRpm);
        StatusMessage = $"Manual Mode set to {ManualTargetRpm:F0} RPM";
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

            // Open Explorer with file selected
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
