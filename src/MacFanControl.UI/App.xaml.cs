using System.Threading;
using System.Windows;
using MacFanControl.Hardware;
using MacFanControl.SMC;
using MacFanControl.UI.Tray;
using MacFanControl.UI.ViewModels;

using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace MacFanControl.UI;

public partial class App : Application
{
    private static Mutex? _mutex;
    private TrayIconManager? _trayManager;
    private MainViewModel? _viewModel;
    private MainWindow? _mainWindow;

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        // Single Instance Protection
        const string mutexName = "MacFanControlBootCamp_SingleInstanceMutex";
        _mutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            MessageBox.Show("MacFanControl is already running in the System Tray (Khay hệ thống).",
                "MacFanControl", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Initialize Services
        var sensorService = new HardwareMonitorService();
        var smcService = new AppleSmcService();

        _viewModel = new MainViewModel(sensorService, smcService);
        await _viewModel.StartAsync();

        _mainWindow = new MainWindow(_viewModel);

        // Initialize Tray Icon
        _trayManager = new TrayIconManager(
            onOpenWindow: () =>
            {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            },
            onOpenSettings: () =>
            {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
                _viewModel.SelectedTabIndex = 2; // Settings tab
            },
            onSetMode: mode => _viewModel.SelectedMode = mode,
            onExitApp: () => Shutdown());

        _viewModel.TrayManager = _trayManager;

        // Check if started with --minimized flag (e.g. from Windows boot)
        bool startMinimized = e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
        if (!startMinimized)
        {
            _mainWindow.Show();
        }
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        _trayManager?.Dispose();
        _viewModel?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
    }
}
