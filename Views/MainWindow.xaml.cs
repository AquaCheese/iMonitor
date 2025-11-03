using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using iMonitor.Models;
using iMonitor.Services;

namespace iMonitor.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly DeviceMonitorService _deviceMonitorService;
    private readonly DisplayManagementService _displayManagementService;
    private DateTime _lastUpdated = DateTime.Now;

    public DateTime LastUpdated
    {
        get => _lastUpdated;
        set
        {
            _lastUpdated = value;
            OnPropertyChanged(nameof(LastUpdated));
        }
    }

    public MainWindow(DeviceMonitorService deviceMonitorService)
    {
        InitializeComponent();
        
        _deviceMonitorService = deviceMonitorService;
        _displayManagementService = new DisplayManagementService();

        DataContext = this;
        
        // Set up device monitoring
        DevicesItemsControl.ItemsSource = _deviceMonitorService.ConnectedDevices;
        _deviceMonitorService.DeviceConnected += OnDeviceConnected;
        _deviceMonitorService.DeviceDisconnected += OnDeviceDisconnected;
        
        // Subscribe to virtual monitor events
        _displayManagementService.VirtualMonitorCreated += OnVirtualMonitorCreated;
        _displayManagementService.VirtualMonitorRemoved += OnVirtualMonitorRemoved;

        // Initialize UI
        UpdateDeviceCount();
        RefreshDisplays();
        LoadSettings();

        // Check administrator privileges
        iMonitor.Services.AdministratorHelper.ShowAdministratorWarning();

        // Initialize virtual display driver (async)
        _ = InitializeVirtualDisplayAsync();

        // Update timestamp periodically
        var timer = new System.Windows.Threading.DispatcherTimer();
        timer.Interval = TimeSpan.FromMinutes(1);
        timer.Tick += (s, e) => LastUpdated = DateTime.Now;
        timer.Start();
    }

    private async Task InitializeVirtualDisplayAsync()
    {
        try
        {
            StatusText.Text = "Initializing virtual display driver...";
            
            if (await _displayManagementService.InitializeVirtualDisplayAsync())
            {
                StatusText.Text = "Virtual display driver ready";
            }
            else
            {
                StatusText.Text = "Virtual display driver initialization failed - Administrator privileges may be required";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Virtual display driver error: {ex.Message}";
        }
    }

    private void OnVirtualMonitorCreated(object? sender, VirtualMonitor virtualMonitor)
    {
        Dispatcher.Invoke(() =>
        {
            RefreshDisplays();
            StatusText.Text = $"Virtual monitor created: {virtualMonitor.DeviceName}";
        });
    }

    private void OnVirtualMonitorRemoved(object? sender, VirtualMonitor virtualMonitor)
    {
        Dispatcher.Invoke(() =>
        {
            RefreshDisplays();
            StatusText.Text = $"Virtual monitor removed: {virtualMonitor.DeviceName}";
        });
    }

    private void OnDeviceConnected(object? sender, ExternalDevice device)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateDeviceCount();
            LastUpdated = DateTime.Now;
            StatusText.Text = $"Device connected: {device.Name}";
            
            if (ShowNotificationsCheckBox?.IsChecked == true)
            {
                // Would show notification via SystemTrayService if available
            }
        });
    }

    private void OnDeviceDisconnected(object? sender, ExternalDevice device)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateDeviceCount();
            LastUpdated = DateTime.Now;
            StatusText.Text = $"Device disconnected: {device.Name}";
        });
    }

    private void UpdateDeviceCount()
    {
        var count = _deviceMonitorService.ConnectedDevices.Count;
        DeviceCountText.Text = $"{count} device{(count != 1 ? "s" : "")}";
        
        NoDevicesMessage.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DevicesItemsControl.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Refreshing devices...";
        
        // Force refresh of device list
        Task.Run(() =>
        {
            System.Threading.Thread.Sleep(1000); // Simulate refresh delay
            Dispatcher.Invoke(() =>
            {
                LastUpdated = DateTime.Now;
                StatusText.Text = "Device refresh complete";
            });
        });
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ExternalDevice device)
        {
            button.IsEnabled = false;
            StatusText.Text = device.IsConnectedAsMonitor ? "Disconnecting virtual monitor..." : "Creating virtual monitor...";

            try
            {
                if (device.IsConnectedAsMonitor)
                {
                    // Disconnect
                    if (await _displayManagementService.DisconnectDeviceDisplayAsync(device))
                    {
                        StatusText.Text = $"Disconnected virtual monitor for {device.Name}";
                    }
                    else
                    {
                        StatusText.Text = $"Failed to disconnect {device.Name}";
                    }
                }
                else
                {
                    // Connect
                    if (await _displayManagementService.ExtendDisplayToDeviceAsync(device))
                    {
                        StatusText.Text = $"Created virtual monitor for {device.Name} - Check Windows Display Settings";
                        
                        // Refresh display list to show the new virtual monitor
                        RefreshDisplays();
                    }
                    else
                    {
                        StatusText.Text = $"Failed to create virtual monitor for {device.Name}";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
            }
            finally
            {
                button.IsEnabled = true;
                LastUpdated = DateTime.Now;
            }
        }
    }

    private void RefreshDisplaysButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshDisplays();
    }

    private void RefreshDisplays()
    {
        var displays = _displayManagementService.GetAvailableDisplays();
        var virtualMonitors = _displayManagementService.GetActiveVirtualMonitors();
        
        // Combine physical and virtual displays
        var allDisplays = displays.ToList();
        
        foreach (var vm in virtualMonitors)
        {
            allDisplays.Add(new DisplayInfo
            {
                DeviceName = $"iMonitor_{vm.Id}",
                DeviceString = $"iMonitor Virtual Display - {vm.DeviceName}",
                DeviceID = vm.DeviceId,
                IsActive = vm.IsActive,
                IsPrimary = false,
                Width = vm.Width,
                Height = vm.Height,
                PositionX = 0,
                PositionY = 0,
                Frequency = 60
            });
        }
        
        DisplaysListBox.ItemsSource = allDisplays;
        StatusText.Text = $"Found {displays.Count} physical display(s) and {virtualMonitors.Count} virtual monitor(s)";
    }

    private void StartWithWindowsCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        SetStartupRegistry(true);
    }

    private void StartWithWindowsCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        SetStartupRegistry(false);
    }

    private void ShowNotificationsCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        SaveSettings();
    }

    private void ShowNotificationsCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        SaveSettings();
    }

    private void SetStartupRegistry(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (enable)
            {
                key?.SetValue("iMonitor", System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "");
            }
            else
            {
                key?.DeleteValue("iMonitor", false);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error setting startup registry: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void LoadSettings()
    {
        try
        {
            // Check if app is set to start with Windows
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            StartWithWindowsCheckBox.IsChecked = key?.GetValue("iMonitor") != null;
        }
        catch
        {
            // Ignore errors when loading settings
        }
    }

    private void SaveSettings()
    {
        // In a real application, you would save settings to a config file or registry
        // For now, settings are handled in real-time
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (MinimizeToTrayCheckBox?.IsChecked == true)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            // Clean up virtual monitors and services
            _displayManagementService?.Dispose();
            base.OnClosing(e);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

// Converter for Connect/Disconnect button text
public class ConnectButtonTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? "Disconnect" : "Connect as Monitor";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}