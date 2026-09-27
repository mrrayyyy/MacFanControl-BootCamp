using System.ComponentModel;
using System.Windows;
using MacFanControl.UI.ViewModels;

namespace MacFanControl.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _isExplicitExit;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!_isExplicitExit)
        {
            if (_viewModel.Settings.MinimizeOnClose)
            {
                // Minimize to system tray instead of terminating application
                e.Cancel = true;
                Hide();
            }
        }
    }

    private void FilterCategory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string cat)
        {
            _viewModel.SelectedSensorCategory = cat;
        }
    }

    public void ForceClose()
    {
        _isExplicitExit = true;
        Close();
    }
}
