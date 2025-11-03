using System.Configuration;
using System.Data;
using System.Windows;
using iMonitor.Services;
using iMonitor.Views;

namespace iMonitor;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private DeviceMonitorService? _deviceMonitorService;
    private SystemTrayService? _systemTrayService;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Hide the main window initially - app starts in system tray
        this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Initialize services
        _deviceMonitorService = new DeviceMonitorService();
        _systemTrayService = new SystemTrayService();

        // Set up system tray
        _systemTrayService.ShowMainWindow += OnShowMainWindow;
        _systemTrayService.ExitApplication += OnExitApplication;
        _systemTrayService.Initialize();

        // Start device monitoring
        _deviceMonitorService.StartMonitoring();
    }

    private void OnShowMainWindow()
    {
        if (_mainWindow == null)
        {
            _mainWindow = new MainWindow(_deviceMonitorService!);
        }

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void OnExitApplication()
    {
        _deviceMonitorService?.StopMonitoring();
        _systemTrayService?.Dispose();
        this.Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _deviceMonitorService?.StopMonitoring();
        _systemTrayService?.Dispose();
        base.OnExit(e);
    }
}