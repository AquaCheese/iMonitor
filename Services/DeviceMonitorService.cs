using System.Collections.ObjectModel;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows;
using iMonitor.Models;

namespace iMonitor.Services;

public class DeviceMonitorService : IDisposable
{
    private ManagementEventWatcher? _insertWatcher;
    private ManagementEventWatcher? _removeWatcher;
    private readonly ObservableCollection<ExternalDevice> _connectedDevices = new();
    private bool _disposed = false;

    public ObservableCollection<ExternalDevice> ConnectedDevices => _connectedDevices;

    public event EventHandler<ExternalDevice>? DeviceConnected;
    public event EventHandler<ExternalDevice>? DeviceDisconnected;

    public void StartMonitoring()
    {
        try
        {
            // Monitor USB device insertions
            var insertQuery = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2");
            _insertWatcher = new ManagementEventWatcher(insertQuery);
            _insertWatcher.EventArrived += OnDeviceInserted;
            _insertWatcher.Start();

            // Monitor USB device removals
            var removeQuery = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 3");
            _removeWatcher = new ManagementEventWatcher(removeQuery);
            _removeWatcher.EventArrived += OnDeviceRemoved;
            _removeWatcher.Start();

            // Initial scan for existing devices
            ScanForExistingDevices();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error starting device monitoring: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void StopMonitoring()
    {
        try
        {
            _insertWatcher?.Stop();
            _removeWatcher?.Stop();
        }
        catch (Exception ex)
        {
            // Log error but don't throw
            System.Diagnostics.Debug.WriteLine($"Error stopping device monitoring: {ex.Message}");
        }
    }

    private void ScanForExistingDevices()
    {
        try
        {
            // Scan for USB devices
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE 'USB%'");
            
            foreach (ManagementObject device in searcher.Get())
            {
                var externalDevice = CreateDeviceFromManagementObject(device);
                if (externalDevice != null && externalDevice.CanBeUsedAsMonitor)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (!_connectedDevices.Contains(externalDevice))
                        {
                            _connectedDevices.Add(externalDevice);
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error scanning for existing devices: {ex.Message}");
        }
    }

    private void OnDeviceInserted(object sender, EventArrivedEventArgs e)
    {
        try
        {
            // Delay to allow device to be fully recognized
            Task.Delay(2000).ContinueWith(_ => RefreshDeviceList());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling device insertion: {ex.Message}");
        }
    }

    private void OnDeviceRemoved(object sender, EventArrivedEventArgs e)
    {
        try
        {
            RefreshDeviceList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling device removal: {ex.Message}");
        }
    }

    private void RefreshDeviceList()
    {
        try
        {
            var currentDevices = new List<ExternalDevice>();

            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE 'USB%'");
            
            foreach (ManagementObject device in searcher.Get())
            {
                var externalDevice = CreateDeviceFromManagementObject(device);
                if (externalDevice != null && externalDevice.CanBeUsedAsMonitor)
                {
                    currentDevices.Add(externalDevice);
                }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                // Remove disconnected devices
                for (int i = _connectedDevices.Count - 1; i >= 0; i--)
                {
                    if (!currentDevices.Contains(_connectedDevices[i]))
                    {
                        var removedDevice = _connectedDevices[i];
                        _connectedDevices.RemoveAt(i);
                        DeviceDisconnected?.Invoke(this, removedDevice);
                    }
                }

                // Add new devices
                foreach (var device in currentDevices)
                {
                    if (!_connectedDevices.Contains(device))
                    {
                        _connectedDevices.Add(device);
                        DeviceConnected?.Invoke(this, device);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error refreshing device list: {ex.Message}");
        }
    }

    private ExternalDevice? CreateDeviceFromManagementObject(ManagementObject device)
    {
        try
        {
            var deviceId = device["DeviceID"]?.ToString();
            var name = device["Name"]?.ToString();
            var description = device["Description"]?.ToString();
            var manufacturer = device["Manufacturer"]?.ToString();

            if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(name))
                return null;

            // Skip system devices and drivers
            if (name.ToLower().Contains("driver") || 
                name.ToLower().Contains("controller") || 
                name.ToLower().Contains("hub"))
                return null;

            var deviceType = DetermineDeviceType(name, description, manufacturer);

            return new ExternalDevice
            {
                DeviceId = deviceId,
                Name = name,
                Description = description ?? "",
                Manufacturer = manufacturer ?? "",
                Type = deviceType,
                IsConnected = true,
                LastSeen = DateTime.Now,
                Status = "Available"
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating device from management object: {ex.Message}");
            return null;
        }
    }

    private DeviceType DetermineDeviceType(string? name, string? description, string? manufacturer)
    {
        var fullText = $"{name} {description} {manufacturer}".ToLower();

        if (fullText.Contains("phone") || fullText.Contains("android") || fullText.Contains("iphone"))
            return DeviceType.Phone;
        
        if (fullText.Contains("tablet") || fullText.Contains("ipad"))
            return DeviceType.Tablet;
        
        if (fullText.Contains("monitor") || fullText.Contains("display"))
            return DeviceType.Monitor;
        
        if (fullText.Contains("computer") || fullText.Contains("pc"))
            return DeviceType.Computer;
        
        if (fullText.Contains("storage") || fullText.Contains("disk") || fullText.Contains("drive"))
            return DeviceType.Storage;

        // Default to tablet for mobile devices that might be used as monitors
        if (fullText.Contains("mobile") || fullText.Contains("portable"))
            return DeviceType.Tablet;

        return DeviceType.Other;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopMonitoring();
            _insertWatcher?.Dispose();
            _removeWatcher?.Dispose();
            _disposed = true;
        }
    }
}