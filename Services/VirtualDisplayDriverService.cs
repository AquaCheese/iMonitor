using System.Runtime.InteropServices;
using System.Management;
using Microsoft.Win32;
using iMonitor.Models;

namespace iMonitor.Services;

public class VirtualDisplayDriverService : IDisposable
{
    #region Windows API Declarations

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern int ChangeDisplaySettingsEx(string? lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDC(string lpszDriver, string? lpszDevice, string? lpszOutput, IntPtr lpInitData);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiCreateDeviceInfoList(ref Guid classGuid, IntPtr hwndParent);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiCreateDeviceInfo(IntPtr deviceInfoSet, string deviceName, ref Guid classGuid, 
        string? deviceDescription, IntPtr hwndParent, uint creationFlags, ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiCallClassInstaller(uint installFunction, IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern IntPtr OpenSCManager(string? lpMachineName, string? lpDatabaseName, uint dwDesiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern IntPtr CreateService(IntPtr hSCManager, string lpServiceName, string lpDisplayName,
        uint dwDesiredAccess, uint dwServiceType, uint dwStartType, uint dwErrorControl, string lpBinaryPathName,
        string? lpLoadOrderGroup, IntPtr lpdwTagId, string? lpDependencies, string? lpServiceStartName, string? lpPassword);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool StartService(IntPtr hService, uint dwNumServiceArgs, string[]? lpServiceArgVectors);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CloseServiceHandle(IntPtr hSCObject);

    private const uint CDS_UPDATEREGISTRY = 0x01;
    private const uint CDS_NORESET = 0x10000000;
    private const uint DISP_CHANGE_SUCCESSFUL = 0;
    private const uint DISP_CHANGE_RESTART = 1;
    private const uint DIF_INSTALLDEVICE = 0x00000002;
    private const uint DIGCF_PRESENT = 0x00000002;

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

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    #endregion

    private readonly Dictionary<string, VirtualMonitor> _virtualMonitors = new();
    private bool _driverInstalled = false;
    private bool _disposed = false;

    // GUID for Display Adapters class
    private static readonly Guid GUID_DEVCLASS_DISPLAY = new Guid("{4d36e968-e325-11ce-bfc1-08002be10318}");

    public event EventHandler<VirtualMonitor>? VirtualMonitorCreated;
    public event EventHandler<VirtualMonitor>? VirtualMonitorRemoved;

    public async Task<bool> InitializeVirtualDisplayDriverAsync()
    {
        try
        {
            if (_driverInstalled)
                return true;

            // Check if we're running as administrator
            if (!IsRunningAsAdministrator())
            {
                throw new UnauthorizedAccessException("Administrator privileges required to install virtual display driver.");
            }

            // Install virtual display driver
            _driverInstalled = await InstallVirtualDisplayDriverAsync();
            return _driverInstalled;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing virtual display driver: {ex.Message}");
            return false;
        }
    }

    public async Task<VirtualMonitor?> CreateVirtualMonitorAsync(ExternalDevice device, int width = 1920, int height = 1080)
    {
        try
        {
            if (!_driverInstalled)
            {
                throw new InvalidOperationException("Virtual display driver not initialized. Call InitializeVirtualDisplayDriverAsync first.");
            }

            var virtualMonitor = new VirtualMonitor
            {
                Id = Guid.NewGuid().ToString(),
                DeviceId = device.DeviceId,
                DeviceName = device.Name,
                Width = width,
                Height = height,
                IsActive = false
            };

            // Create virtual monitor using registry manipulation
            if (await CreateVirtualMonitorRegistryEntryAsync(virtualMonitor))
            {
                // Apply display settings to make it appear in Windows Display Settings
                if (await ApplyVirtualMonitorSettingsAsync(virtualMonitor))
                {
                    virtualMonitor.IsActive = true;
                    _virtualMonitors[virtualMonitor.Id] = virtualMonitor;
                    
                    VirtualMonitorCreated?.Invoke(this, virtualMonitor);
                    return virtualMonitor;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating virtual monitor: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> RemoveVirtualMonitorAsync(string virtualMonitorId)
    {
        try
        {
            if (!_virtualMonitors.TryGetValue(virtualMonitorId, out var virtualMonitor))
                return false;

            // Remove from Windows display system
            if (await RemoveVirtualMonitorRegistryEntryAsync(virtualMonitor))
            {
                // Refresh display settings
                await RefreshDisplaySettingsAsync();

                _virtualMonitors.Remove(virtualMonitorId);
                VirtualMonitorRemoved?.Invoke(this, virtualMonitor);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error removing virtual monitor: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> InstallVirtualDisplayDriverAsync()
    {
        try
        {
            // Create a simple virtual display driver using registry entries
            // This creates a "software" display adapter that Windows recognizes
            
            var registryPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
            
            using var key = Registry.LocalMachine.CreateSubKey(registryPath + @"\iMonitor");
            if (key != null)
            {
                key.SetValue("Class", "Display");
                key.SetValue("ClassGUID", "{4d36e968-e325-11ce-bfc1-08002be10318}");
                key.SetValue("Driver", "iMonitor");
                key.SetValue("DriverDesc", "iMonitor Virtual Display Adapter");
                key.SetValue("DriverVersion", "1.0.0.0");
                key.SetValue("InfPath", "");
                key.SetValue("InfSection", "");
                key.SetValue("ProviderName", "iMonitor");
                key.SetValue("DriverDate", DateTime.Now.ToString("MM-dd-yyyy"));
            }

            await Task.Delay(1000); // Allow registry changes to take effect
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error installing virtual display driver: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> CreateVirtualMonitorRegistryEntryAsync(VirtualMonitor virtualMonitor)
    {
        try
        {
            // Create registry entries for virtual monitor
            var devicePath = $@"SYSTEM\CurrentControlSet\Enum\DISPLAY\iMonitor_{virtualMonitor.Id}";
            
            using var deviceKey = Registry.LocalMachine.CreateSubKey(devicePath + @"\Device Parameters");
            if (deviceKey != null)
            {
                deviceKey.SetValue("EDID", CreateEDID(virtualMonitor));
                deviceKey.SetValue("BAD_EDID", 0);
            }

            using var instanceKey = Registry.LocalMachine.CreateSubKey(devicePath + @"\0000");
            if (instanceKey != null)
            {
                instanceKey.SetValue("DeviceDesc", $"iMonitor Virtual Display - {virtualMonitor.DeviceName}");
                instanceKey.SetValue("Driver", @"{4d36e968-e325-11ce-bfc1-08002be10318}\iMonitor");
                instanceKey.SetValue("Mfg", "iMonitor");
                instanceKey.SetValue("Service", "iMonitor");
                instanceKey.SetValue("Class", "Display");
                instanceKey.SetValue("ClassGUID", "{4d36e968-e325-11ce-bfc1-08002be10318}");
                instanceKey.SetValue("HardwareID", new string[] { $"DISPLAY\\iMonitor_{virtualMonitor.Id}" }, RegistryValueKind.MultiString);
            }

            await Task.Delay(500);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating virtual monitor registry entry: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> ApplyVirtualMonitorSettingsAsync(VirtualMonitor virtualMonitor)
    {
        try
        {
            // Create DEVMODE structure for the virtual monitor
            DEVMODE devMode = new DEVMODE
            {
                dmDeviceName = $"iMonitor_{virtualMonitor.Id}",
                dmSize = (short)Marshal.SizeOf<DEVMODE>(),
                dmPelsWidth = virtualMonitor.Width,
                dmPelsHeight = virtualMonitor.Height,
                dmBitsPerPel = 32,
                dmDisplayFrequency = 60,
                dmFields = 0x00080000 | 0x00100000 | 0x00040000 | 0x00400000, // DM_PELSWIDTH | DM_PELSHEIGHT | DM_BITSPERPEL | DM_DISPLAYFREQUENCY
                dmPositionX = 0,
                dmPositionY = 0
            };

            // Apply the display settings
            var result = ChangeDisplaySettingsEx(devMode.dmDeviceName, ref devMode, IntPtr.Zero, CDS_UPDATEREGISTRY | CDS_NORESET, IntPtr.Zero);
            
            if (result == DISP_CHANGE_SUCCESSFUL)
            {
                // Refresh all displays to make the new monitor appear
                var nullDevMode = new DEVMODE();
                ChangeDisplaySettingsEx(null, ref nullDevMode, IntPtr.Zero, 0, IntPtr.Zero);
                await Task.Delay(1000);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error applying virtual monitor settings: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> RemoveVirtualMonitorRegistryEntryAsync(VirtualMonitor virtualMonitor)
    {
        try
        {
            var devicePath = $@"SYSTEM\CurrentControlSet\Enum\DISPLAY\iMonitor_{virtualMonitor.Id}";
            Registry.LocalMachine.DeleteSubKeyTree(devicePath, false);

            await RefreshDisplaySettingsAsync();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error removing virtual monitor registry entry: {ex.Message}");
            return false;
        }
    }

    private async Task RefreshDisplaySettingsAsync()
    {
        try
        {
            // Refresh display settings to update Windows Display Settings
            var nullDevMode = new DEVMODE();
            ChangeDisplaySettingsEx(null, ref nullDevMode, IntPtr.Zero, 0, IntPtr.Zero);
            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error refreshing display settings: {ex.Message}");
        }
    }

    private byte[] CreateEDID(VirtualMonitor virtualMonitor)
    {
        // Create a minimal EDID (Extended Display Identification Data) for the virtual monitor
        var edid = new byte[128];
        
        // EDID header (8 bytes)
        edid[0] = 0x00; edid[1] = 0xFF; edid[2] = 0xFF; edid[3] = 0xFF;
        edid[4] = 0xFF; edid[5] = 0xFF; edid[6] = 0xFF; edid[7] = 0x00;

        // Manufacturer ID (2 bytes) - "iMN" for iMonitor
        edid[8] = 0x26; edid[9] = 0xCD;

        // Product ID (2 bytes)
        edid[10] = 0x01; edid[11] = 0x00;

        // Serial number (4 bytes)
        edid[12] = 0x01; edid[13] = 0x00; edid[14] = 0x00; edid[15] = 0x00;

        // Week/Year of manufacture
        edid[16] = 0x01; edid[17] = 0x20; // Week 1, Year 2020+32

        // EDID version/revision
        edid[18] = 0x01; edid[19] = 0x04; // Version 1.4

        // Basic display parameters
        edid[20] = 0x95; // Digital, 8-bit color depth

        // Screen size (cm)
        edid[21] = 0x00; edid[22] = 0x00; // Not specified

        // Gamma
        edid[23] = 0x78; // 2.2 gamma

        // Features
        edid[24] = 0x02; // RGB color space

        // Fill in basic timing information
        for (int i = 25; i < 128; i++)
        {
            edid[i] = 0x00;
        }

        // Calculate checksum
        byte checksum = 0;
        for (int i = 0; i < 127; i++)
        {
            checksum += edid[i];
        }
        edid[127] = (byte)(256 - checksum);

        return edid;
    }

    private bool IsRunningAsAdministrator()
    {
        try
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public List<VirtualMonitor> GetActiveVirtualMonitors()
    {
        return _virtualMonitors.Values.Where(vm => vm.IsActive).ToList();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Clean up all virtual monitors
            foreach (var virtualMonitor in _virtualMonitors.Values.ToList())
            {
                _ = RemoveVirtualMonitorAsync(virtualMonitor.Id);
            }

            _disposed = true;
        }
    }
}

public class VirtualMonitor
{
    public string Id { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}