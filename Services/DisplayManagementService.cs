using System.Runtime.InteropServices;
using System.Windows;
using iMonitor.Models;

namespace iMonitor.Services;

public class DisplayManagementService : IDisposable
{
    private readonly VirtualDisplayDriverService _virtualDisplayService;
    private readonly DisplayStreamingService _streamingService;
    private bool _disposed = false;
    #region Windows API Declarations
    
    [DllImport("user32.dll")]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

    [DllImport("user32.dll")]
    private static extern int ChangeDisplaySettings(ref DEVMODE devMode, uint dwflags);

    [DllImport("user32.dll")]
    private static extern int ChangeDisplaySettingsEx(string? lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

    private const int ENUM_CURRENT_SETTINGS = -1;
    private const int ENUM_REGISTRY_SETTINGS = -2;
    private const uint CDS_UPDATEREGISTRY = 0x01;
    private const uint CDS_TEST = 0x02;
    private const uint CDS_NORESET = 0x10000000;
    private const uint CDS_SET_PRIMARY = 0x00000010;

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAY_DEVICE
    {
        public uint cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public short dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }

    #endregion

    private readonly List<DisplayInfo> _availableDisplays = new();
    private readonly Dictionary<string, DisplayInfo> _deviceToDisplayMap = new();

    public DisplayManagementService()
    {
        _virtualDisplayService = new VirtualDisplayDriverService();
        _streamingService = new DisplayStreamingService();
        
        // Subscribe to virtual monitor events
        _virtualDisplayService.VirtualMonitorCreated += OnVirtualMonitorCreated;
        _virtualDisplayService.VirtualMonitorRemoved += OnVirtualMonitorRemoved;
        
        // Subscribe to streaming events
        _streamingService.StreamingStarted += OnStreamingStarted;
        _streamingService.StreamingStopped += OnStreamingStopped;
        _streamingService.StreamingError += OnStreamingError;
    }

    public event EventHandler<VirtualMonitor>? VirtualMonitorCreated;
    public event EventHandler<VirtualMonitor>? VirtualMonitorRemoved;

    public List<DisplayInfo> GetAvailableDisplays()
    {
        _availableDisplays.Clear();
        _deviceToDisplayMap.Clear();

        DISPLAY_DEVICE displayDevice = new DISPLAY_DEVICE();
        displayDevice.cb = (uint)Marshal.SizeOf(displayDevice);

        uint deviceIndex = 0;
        while (EnumDisplayDevices(null, deviceIndex, ref displayDevice, 0))
        {
            var displayInfo = new DisplayInfo
            {
                DeviceName = displayDevice.DeviceName,
                DeviceString = displayDevice.DeviceString,
                DeviceID = displayDevice.DeviceID,
                IsActive = (displayDevice.StateFlags & 0x00000001) != 0, // DISPLAY_DEVICE_ACTIVE
                IsPrimary = (displayDevice.StateFlags & 0x00000004) != 0 // DISPLAY_DEVICE_PRIMARY_DEVICE
            };

            // Get current settings for active displays
            if (displayInfo.IsActive)
            {
                DEVMODE devMode = new DEVMODE();
                devMode.dmSize = (short)Marshal.SizeOf(devMode);

                if (EnumDisplaySettings(displayDevice.DeviceName, ENUM_CURRENT_SETTINGS, ref devMode))
                {
                    displayInfo.Width = devMode.dmPelsWidth;
                    displayInfo.Height = devMode.dmPelsHeight;
                    displayInfo.PositionX = devMode.dmPositionX;
                    displayInfo.PositionY = devMode.dmPositionY;
                    displayInfo.Frequency = devMode.dmDisplayFrequency;
                }
            }

            _availableDisplays.Add(displayInfo);
            deviceIndex++;
        }

        return _availableDisplays;
    }

    public async Task<bool> ExtendDisplayToDeviceAsync(ExternalDevice device)
    {
        try
        {
            // Initialize virtual display driver if not already done
            if (!await _virtualDisplayService.InitializeVirtualDisplayDriverAsync())
            {
                MessageBox.Show(
                    "Failed to initialize virtual display driver. Please ensure you are running as Administrator.",
                    "Driver Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            // Create virtual monitor for the device
            var virtualMonitor = await _virtualDisplayService.CreateVirtualMonitorAsync(device, 1920, 1080);
            
            if (virtualMonitor != null)
            {
                device.IsConnectedAsMonitor = true;
                
                // Start streaming to the device
                if (await _streamingService.StartStreamingAsync(virtualMonitor, device))
                {
                    // Success message is shown by the streaming service
                    return true;
                }
                else
                {
                    MessageBox.Show(
                        $"Virtual monitor created for {device.Name}, but streaming failed to start.\n\n" +
                        $"Monitor Name: iMonitor_{virtualMonitor.Id}\n" +
                        $"Resolution: {virtualMonitor.Width}x{virtualMonitor.Height}\n\n" +
                        "The monitor is available in Windows Display Settings, but device streaming is not active.",
                        "Partial Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return true;
                }
            }
            else
            {
                MessageBox.Show(
                    $"Failed to create virtual monitor for {device.Name}.\n" +
                    "Please check that you have Administrator privileges.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show(
                "Administrator privileges are required to create virtual monitors.\n" +
                "Please restart iMonitor as Administrator.",
                "Access Denied",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error extending display to device: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    public bool ExtendDisplayToDevice(ExternalDevice device)
    {
        // Legacy synchronous method - calls async version
        return ExtendDisplayToDeviceAsync(device).GetAwaiter().GetResult();
    }

    public async Task<bool> DisconnectDeviceDisplayAsync(ExternalDevice device)
    {
        try
        {
            // Find and remove the virtual monitor associated with this device
            var virtualMonitors = _virtualDisplayService.GetActiveVirtualMonitors();
            var deviceVirtualMonitor = virtualMonitors.FirstOrDefault(vm => vm.DeviceId == device.DeviceId);

            if (deviceVirtualMonitor != null)
            {
                // Stop streaming first
                await _streamingService.StopStreamingAsync(deviceVirtualMonitor.Id);
                
                if (await _virtualDisplayService.RemoveVirtualMonitorAsync(deviceVirtualMonitor.Id))
                {
                    device.IsConnectedAsMonitor = false;
                    
                    MessageBox.Show(
                        $"Successfully disconnected virtual monitor for {device.Name}.\n" +
                        "The monitor has been removed from Windows Display Settings and streaming has stopped.",
                        "Monitor Disconnected",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return true;
                }
            }

            device.IsConnectedAsMonitor = false;
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error disconnecting device display: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    public bool DisconnectDeviceDisplay(ExternalDevice device)
    {
        // Legacy synchronous method - calls async version
        return DisconnectDeviceDisplayAsync(device).GetAwaiter().GetResult();
    }

    public bool SetDisplayConfiguration(int width, int height, int positionX, int positionY, string deviceName)
    {
        try
        {
            DEVMODE devMode = new DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(devMode);
            devMode.dmPelsWidth = width;
            devMode.dmPelsHeight = height;
            devMode.dmPositionX = positionX;
            devMode.dmPositionY = positionY;
            devMode.dmFields = 0x00000020 | 0x00000040 | 0x00020000 | 0x00000001; // DM_PELSWIDTH | DM_PELSHEIGHT | DM_POSITION | DM_BITSPERPEL

            var result = ChangeDisplaySettingsEx(deviceName, ref devMode, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
            return result == 0; // DISP_CHANGE_SUCCESSFUL
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error setting display configuration: {ex.Message}");
            return false;
        }
    }

    private void OnVirtualMonitorCreated(object? sender, VirtualMonitor virtualMonitor)
    {
        VirtualMonitorCreated?.Invoke(this, virtualMonitor);
    }

    private void OnVirtualMonitorRemoved(object? sender, VirtualMonitor virtualMonitor)
    {
        VirtualMonitorRemoved?.Invoke(this, virtualMonitor);
    }

    public List<VirtualMonitor> GetActiveVirtualMonitors()
    {
        return _virtualDisplayService.GetActiveVirtualMonitors();
    }

    private void OnStreamingStarted(object? sender, string virtualMonitorId)
    {
        System.Diagnostics.Debug.WriteLine($"Streaming started for virtual monitor: {virtualMonitorId}");
    }

    private void OnStreamingStopped(object? sender, string virtualMonitorId)
    {
        System.Diagnostics.Debug.WriteLine($"Streaming stopped for virtual monitor: {virtualMonitorId}");
    }

    private void OnStreamingError(object? sender, StreamingError error)
    {
        System.Diagnostics.Debug.WriteLine($"Streaming error for virtual monitor {error.VirtualMonitorId}: {error.Message}");
    }

    public List<StreamingSession> GetActiveStreams()
    {
        return _streamingService.GetActiveStreams();
    }

    public async Task<bool> InitializeVirtualDisplayAsync()
    {
        return await _virtualDisplayService.InitializeVirtualDisplayDriverAsync();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _streamingService?.Dispose();
            _virtualDisplayService?.Dispose();
            _disposed = true;
        }
    }
}

public class DisplayInfo
{
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceString { get; set; } = string.Empty;
    public string DeviceID { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsPrimary { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public int Frequency { get; set; }

    public string DisplayText => $"{DeviceString} ({Width}x{Height})" + (IsPrimary ? " [Primary]" : "");
}