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
            // Minimize to system tray instead of closing application
            e.Cancel = true;
            Hide();
        }
    }

    public void ForceClose()
    {
        _isExplicitExit = true;
        Close();
    }
}
