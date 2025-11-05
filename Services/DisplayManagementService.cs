using System.Runtime.InteropServices;
using System.Windows;
using iMonitor.Models;

namespace iMonitor.Services;

public class DisplayManagementService : IDisposable
{
    private readonly IddCxVirtualDisplayService _iddCxDisplayService;
    private readonly IOSDeviceCommunicationService _iosCommService;
    private readonly ScreenCaptureStreamingService _streamingService;
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
        _iosCommService = new IOSDeviceCommunicationService();
        _iddCxDisplayService = new IddCxVirtualDisplayService();
        _streamingService = new ScreenCaptureStreamingService(_iosCommService);
        
        // Subscribe to virtual monitor events
        _iddCxDisplayService.VirtualMonitorCreated += OnVirtualMonitorCreated;
        _iddCxDisplayService.VirtualMonitorRemoved += OnVirtualMonitorRemoved;
        _iddCxDisplayService.VirtualMonitorError += OnVirtualMonitorError;
        
        // Subscribe to streaming events
        _streamingService.StreamingStarted += OnStreamingStarted;
        _streamingService.StreamingStopped += OnStreamingStopped;
        _streamingService.StreamingError += OnStreamingError;
        
        // Subscribe to iOS communication events
        _iosCommService.DeviceConnected += OnIOSDeviceConnected;
        _iosCommService.DeviceDisconnected += OnIOSDeviceDisconnected;
        _iosCommService.DevicePaired += OnIOSDevicePaired;
        _iosCommService.TouchInputReceived += OnTouchInputReceived;
        _iosCommService.CommunicationError += OnIOSCommunicationError;
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
            // Start iOS communication service if not already started
            if (!await _iosCommService.StartServiceAsync())
            {
                MessageBox.Show(
                    "Failed to start iOS communication service.",
                    "Communication Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            // Connect to iOS device
            if (!await _iosCommService.ConnectToDeviceAsync(device))
            {
                MessageBox.Show(
                    $"Failed to connect to {device.Name}. Please ensure:\n" +
                    "1. Device is unlocked\n" +
                    "2. You have trusted this computer on the device\n" +
                    "3. Device is properly connected via USB",
                    "Connection Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            // Pair with iOS device
            if (!await _iosCommService.PairWithDeviceAsync(device.DeviceId))
            {
                MessageBox.Show(
                    $"Failed to pair with {device.Name}. Please check the device for pairing prompts.",
                    "Pairing Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            // Initialize IddCx virtual display framework
            if (!await _iddCxDisplayService.InitializeIddCxFrameworkAsync())
            {
                MessageBox.Show(
                    "Failed to initialize virtual display driver. Please ensure you are running as Administrator.",
                    "Driver Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            // Create virtual monitor for the device
            var virtualMonitor = await _iddCxDisplayService.CreateVirtualMonitorAsync(device, 1920, 1080, 60);
            
            if (virtualMonitor != null)
            {
                device.IsConnectedAsMonitor = true;
                
                // Start screen capture and streaming to the device
                if (await _streamingService.StartStreamingAsync(virtualMonitor, device))
                {
                    MessageBox.Show(
                        $"Successfully connected {device.Name} as a virtual monitor!\n\n" +
                        $"Monitor Name: {virtualMonitor.DeviceName}\n" +
                        $"Resolution: {virtualMonitor.Width}x{virtualMonitor.Height}@{virtualMonitor.RefreshRate}Hz\n\n" +
                        "You can now:\n" +
                        "• Drag windows to this monitor in Windows Display Settings\n" +
                        "• Arrange displays and set resolution\n" +
                        "• Use touch input on your device\n\n" +
                        "The display will appear on your iOS device momentarily.",
                        "Success!",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return true;
                }
                else
                {
                    MessageBox.Show(
                        $"Virtual monitor created for {device.Name}, but streaming failed to start.\n\n" +
                        $"Monitor Name: {virtualMonitor.DeviceName}\n" +
                        $"Resolution: {virtualMonitor.Width}x{virtualMonitor.Height}\n\n" +
                        "The monitor is available in Windows Display Settings, but device streaming is not active.\n" +
                        "Check your network connection and try again.",
                        "Partial Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return true;
                }
            }
            else
            {
                MessageBox.Show(
                    $"Failed to create virtual monitor for {device.Name}.\n\n" +
                    "This could be due to:\n" +
                    "• Insufficient administrator privileges\n" +
                    "• Display driver installation issues\n" +
                    "• System compatibility problems\n\n" +
                    "Please ensure you're running as Administrator and try again.",
                    "Virtual Monitor Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show(
                "Administrator privileges are required to create virtual monitors.\n\n" +
                "Please:\n" +
                "1. Close iMonitor\n" +
                "2. Right-click on iMonitor and select 'Run as Administrator'\n" +
                "3. Try connecting your device again",
                "Administrator Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"An unexpected error occurred while connecting to {device.Name}:\n\n" +
                $"{ex.Message}\n\n" +
                "Please try the following:\n" +
                "• Restart iMonitor as Administrator\n" +
                "• Reconnect your device\n" +
                "• Check Windows Device Manager for any issues",
                "Connection Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
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
            // Find streaming sessions for this device
            var activeStreams = _streamingService.GetActiveStreams();
            var deviceStreams = activeStreams.Where(s => s.TargetDevice.DeviceId == device.DeviceId).ToList();

            bool success = true;

            // Stop all streaming sessions for this device
            foreach (var stream in deviceStreams)
            {
                if (!await _streamingService.StopStreamingAsync(stream.SessionId))
                {
                    success = false;
                }
            }

            // Find and remove the virtual monitor associated with this device
            var virtualMonitors = _iddCxDisplayService.GetActiveVirtualMonitors();
            var deviceVirtualMonitor = virtualMonitors.FirstOrDefault(vm => vm.DeviceId == device.DeviceId);

            if (deviceVirtualMonitor != null)
            {
                if (await _iddCxDisplayService.RemoveVirtualMonitorAsync(deviceVirtualMonitor.Id))
                {
                    success = true;
                }
                else
                {
                    success = false;
                }
            }

            // Disconnect from iOS device
            _iosCommService.DisconnectDevice(device.DeviceId);
            
            device.IsConnectedAsMonitor = false;

            if (success)
            {
                MessageBox.Show(
                    $"Successfully disconnected {device.Name}.\n\n" +
                    "• Virtual monitor removed from Windows Display Settings\n" +
                    "• Display streaming stopped\n" +
                    "• Device connection closed\n\n" +
                    "You can reconnect the device at any time.",
                    "Device Disconnected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    $"Partially disconnected {device.Name}.\n\n" +
                    "Some components may still be active. If you experience issues, " +
                    "please restart iMonitor and try again.",
                    "Partial Disconnection",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            return success;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error disconnecting {device.Name}:\n\n{ex.Message}\n\n" +
                "The device may still be partially connected. Please restart iMonitor if issues persist.",
                "Disconnection Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
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

    public List<VirtualMonitor> GetActiveVirtualMonitors()
    {
        return _iddCxDisplayService.GetActiveVirtualMonitors();
    }

    public List<StreamingSession> GetActiveStreamingSessions()
    {
        return _streamingService.GetActiveStreams();
    }

    public async Task<bool> InitializeVirtualDisplayAsync()
    {
        return await _iddCxDisplayService.InitializeIddCxFrameworkAsync();
    }

    public async Task<bool> StartIOSCommunicationServiceAsync()
    {
        return await _iosCommService.StartServiceAsync();
    }

    #region Event Handlers

    private void OnVirtualMonitorError(object? sender, string error)
    {
        System.Diagnostics.Debug.WriteLine($"Virtual Monitor Error: {error}");
    }

    private void OnIOSDeviceConnected(object? sender, IOSDeviceEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"iOS Device Connected: {e.Device?.Name}");
    }

    private void OnIOSDeviceDisconnected(object? sender, IOSDeviceEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"iOS Device Disconnected: {e.Device?.DeviceId}");
    }

    private void OnIOSDevicePaired(object? sender, IOSDeviceEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"iOS Device Paired: {e.Device?.Name}");
    }

    private void OnTouchInputReceived(object? sender, IOSTouchEventArgs e)
    {
        // Forward touch input to Windows (would implement touch injection here)
        System.Diagnostics.Debug.WriteLine($"Touch Input: {e.TouchType} at ({e.X}, {e.Y})");
    }

    private void OnIOSCommunicationError(object? sender, string error)
    {
        System.Diagnostics.Debug.WriteLine($"iOS Communication Error: {error}");
    }

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _streamingService?.Dispose();
            _iddCxDisplayService?.Dispose();
            _iosCommService?.Dispose();
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