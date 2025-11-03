# iMonitor - External Device Display Monitor

A Windows application that detects external phones, tablets, and other devices and allows you to use them as additional monitors.

![iMonitor Logo](Resources/logo.jpg)

## Features

- **Automatic Device Detection**: Detects when external devices (phones, tablets, monitors) are connected via USB
- **Real Virtual Monitors**: Creates actual virtual monitors that appear in Windows Display Settings
- **Live Display Streaming**: Streams display content to connected devices in real-time
- **System Tray Integration**: Runs quietly in the system tray with easy access
- **One-Click Monitor Creation**: Simple button to create virtual monitors for detected devices
- **Windows Integration**: Virtual monitors work with Windows display arrangement, resolution settings, and multi-monitor features
- **Device Management**: View and manage all connected devices from a central interface
- **HTTP Streaming**: Serves display content via HTTP for mobile devices to connect through web browsers
- **Auto-Start Support**: Option to start automatically with Windows
- **Notification System**: Get notified when devices are connected or disconnected

## System Requirements

- Windows 10/11 (64-bit)
- .NET 8.0 Runtime
- **Administrator privileges (REQUIRED for virtual monitor creation)**
- Network connectivity for device streaming (Wi-Fi recommended)

## Installation

### Option 1: Build from Source

1. **Prerequisites**:
   - Install [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
   - Install Visual Studio 2022 or Visual Studio Code with C# extension

2. **Clone and Build**:
   ```bash
   git clone <repository-url>
   cd iMonitor
   dotnet restore
   dotnet build --configuration Release
   ```

3. **Run**:
   ```bash
   dotnet run --project iMonitor.csproj
   ```

### Option 2: Publish Self-Contained Executable

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

The executable will be in `bin/Release/net8.0-windows/win-x64/publish/`

## Usage

### Getting Started

1. **Launch the Application**:
   - Run the executable or use `dotnet run`
   - The application will start minimized to the system tray

2. **Access the Interface**:
   - Double-click the system tray icon to open the main window
   - Right-click the system tray icon for quick actions

### Using External Devices as Virtual Monitors

1. **Connect Your Device**:
   - Connect your phone, tablet, or external monitor via USB
   - Wait for the device to be detected (may take a few seconds)

2. **Create Virtual Monitor**:
   - **IMPORTANT**: Run iMonitor as Administrator for virtual monitor creation
   - Open the main iMonitor window
   - Go to the "Connected Devices" tab
   - Find your device in the list
   - Click "Connect as Monitor" button
   - A virtual monitor will be created and appear in Windows Display Settings

3. **Connect Device to Stream**:
   - For mobile devices, connect to the same Wi-Fi network
   - Open a web browser on your device
   - Navigate to the provided IP address (displayed in the connection dialog)
   - Your device will now display the content of the virtual monitor

4. **Configure Display in Windows**:
   - Open Windows Display Settings (Win + P or Settings > System > Display)
   - You'll see the new "iMonitor Virtual Display" listed
   - Arrange, extend, duplicate, or set resolution as with any physical monitor
   - Drag windows to the virtual monitor area to display them on your connected device

### System Tray Features

- **Double-click**: Open main window
- **Right-click menu**:
  - Show iMonitor: Open the main interface
  - Exit: Close the application

### Settings

Access settings from the "Settings" tab in the main window:

- **Start with Windows**: Automatically start iMonitor when Windows boots
- **Show Notifications**: Display notifications when devices connect/disconnect
- **Minimize to Tray**: Minimize to system tray instead of taskbar

## Device Compatibility

### Supported Device Types
- **Tablets**: iPads, Android tablets, Windows tablets
- **Phones**: iPhones, Android phones (with display output capability)
- **External Monitors**: USB-C monitors, DisplayPort monitors
- **Laptops**: Secondary laptops or computers

### Connection Methods
- **Virtual Monitor + HTTP Streaming**: Creates real Windows monitors with web-based display streaming
- **Wi-Fi Network**: Devices connect via web browser to stream content
- **USB Device Detection**: Automatic discovery of connected devices
- **Registry-based Virtual Display Driver**: Creates actual monitor entries in Windows

## Technical Details

### Architecture
- **WPF Application**: Modern Windows desktop interface
- **Device Detection**: Uses Windows Management Instrumentation (WMI)
- **Display Management**: Windows Display APIs
- **System Integration**: Registry settings, system tray

### Key Components
- **DeviceMonitorService**: Monitors USB device connections
- **VirtualDisplayDriverService**: Creates and manages virtual display devices
- **DisplayStreamingService**: Handles real-time display streaming to connected devices
- **DisplayManagementService**: Manages display configuration and virtual monitor integration
- **SystemTrayService**: Handles system tray functionality

## Troubleshooting

### Common Issues

1. **Devices Not Detected**:
   - Ensure USB debugging is enabled on mobile devices
   - Try different USB ports or cables
   - Run as administrator for better device access

2. **Display Connection Fails**:
   - Check if the device supports display output
   - Install device-specific drivers if needed
   - Verify USB-C DisplayPort Alt Mode support

3. **Application Won't Start**:
   - Ensure .NET 8.0 Runtime is installed
   - Check Windows Event Viewer for error details
   - Try running as administrator

### Device-Specific Notes

- **iPhones/iPads**: Require Lightning to USB-C adapter with display support
- **Android Devices**: Must support USB-C DisplayPort Alt Mode or use apps like Duet Display
- **USB-C Monitors**: Should work directly with compatible ports

## Development

### Project Structure
```
iMonitor/
├── Models/           # Data models
├── Services/         # Business logic services
├── Views/           # WPF windows and controls
├── ViewModels/      # MVVM view models
├── Resources/       # Images, icons, resources
└── App.xaml/cs      # Application entry point
```

### Key Dependencies
- **System.Management**: WMI device detection
- **Microsoft.Win32.Registry**: Windows registry access
- **System.Drawing.Common**: Icon and image handling

### Building for Distribution

1. **Create Release Build**:
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
   ```

2. **Create Installer** (optional):
   - Use tools like WiX Toolset or Inno Setup
   - Include .NET runtime if not self-contained

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly on different Windows versions
5. Submit a pull request

## License

[Add your license information here]

## Support

For issues, questions, or feature requests:
- Create an issue in the repository
- Check the troubleshooting section
- Review Windows Event Viewer for error details

---

**Note**: This application provides a framework for device detection and display management. Actual display extension to mobile devices requires additional software or hardware support depending on the device type and connection method.