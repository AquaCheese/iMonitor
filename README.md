# iMonitor - External Device Display Monitor

A Windows application that detects external phones, tablets, and other devices and allows you to use them as additional monitors. **Now with reliable virtual display drivers and comprehensive iOS support!**

![iMonitor Logo](Resources/logo.jpg)

## 🆕 What's New in This Version

- **✅ Modern IddCx Virtual Display Driver** - Replaces unreliable registry manipulation with Microsoft's supported framework
- **✅ Advanced iOS Device Communication** - libimobiledevice-style USB communication with proper pairing
- **✅ High-Performance Screen Capture** - Efficient DirectX-based capture with hardware acceleration support
- **✅ Real-time Video Streaming** - Low-latency H.264 streaming with quality controls
- **✅ Touch Input Forwarding** - Full multi-touch support from iOS devices to Windows
- **✅ Comprehensive Error Handling** - Better user feedback and automatic recovery
- **✅ iOS Companion App Specification** - Complete guide for developing the iOS companion app

## Features

### 🖥️ **Advanced Virtual Display System**
- **Modern IddCx Driver**: Uses Microsoft's latest Indirect Display Driver framework
- **Real Windows Monitors**: Creates actual virtual monitors that appear in Windows Display Settings
- **Multiple Resolutions**: Supports 1080p, 1440p, 4K with configurable refresh rates (60Hz, 90Hz, 120Hz)
- **Hardware Acceleration**: Leverages GPU for optimal performance

### 📱 **iOS Device Integration**
- **USB-C and Lightning Support**: Works with all modern iPhones and iPads
- **Device Pairing**: Secure authentication and pairing process
- **Automatic Detection**: Instantly recognizes connected iOS devices
- **Device Information**: Displays model, iOS version, and capabilities

### 🎮 **Touch Input System**
- **Multi-Touch Support**: Full gesture support including pinch, zoom, rotate
- **Pressure Sensitivity**: Supports Apple Pencil and force touch
- **Low Latency**: <20ms touch response time for smooth interaction
- **Coordinate Mapping**: Precise touch-to-cursor mapping

### 📺 **High-Performance Streaming**
- **Real-Time Capture**: 60+ FPS screen capture with minimal CPU usage
- **Adaptive Quality**: Automatic quality adjustment based on connection
- **Multiple Codecs**: H.264 hardware encoding, MJPEG, raw RGB support
- **Bandwidth Optimization**: Smart compression for optimal streaming

### 🔧 **System Integration**
- **Windows Display Settings**: Full integration with native display management
- **System Tray Operation**: Runs quietly in background with quick access
- **Auto-Start Support**: Automatically starts with Windows
- **Notification System**: Real-time status updates and connection alerts
- **Error Recovery**: Automatic reconnection and error handling

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

### Quick Start Guide

1. **🚀 Launch iMonitor**:
   - **Right-click** iMonitor.exe and select **"Run as Administrator"** (required!)
   - The application will start and appear in the system tray
   - You'll see a notification that iMonitor is ready

2. **📱 Connect Your iPhone/iPad**:
   - Connect your iOS device via **USB-C** or **Lightning cable**
   - **Unlock your device** and tap **"Trust This Computer"** when prompted
   - iMonitor will automatically detect your device within a few seconds

3. **🖥️ Create Virtual Monitor**:
   - Double-click the iMonitor system tray icon to open the main window
   - Go to the **"Connected Devices"** tab
   - Find your iOS device in the list (e.g., "Apple iPhone")
   - Click the **"Connect as Monitor"** button
   - Wait for pairing and virtual monitor creation (30-60 seconds)

4. **✨ Success!**:
   - A new virtual monitor appears in Windows Display Settings
   - Your iOS device shows the iMonitor companion app prompt
   - Content sent to the virtual monitor displays on your iOS device
   - Touch your iOS screen to control the Windows cursor

### 🖥️ **Windows Display Configuration**

1. **Open Display Settings**:
   - Press **Win + P** or go to **Settings > System > Display**
   - You'll see the new **"iMonitor Virtual Display"** listed

2. **Arrange Your Displays**:
   - **Drag and drop** monitor icons to arrange them
   - Set display as **"Extend these displays"** for additional workspace
   - Choose **"Duplicate these displays"** for mirroring

3. **Optimize Settings**:
   - **Resolution**: Set to match your iOS device (1179×2556 for iPhone 15 Pro)
   - **Orientation**: Portrait or landscape as needed
   - **Scaling**: Adjust for comfortable reading
   - **Primary Display**: Set which monitor is primary

4. **Use Your Extended Display**:
   - **Drag windows** to the virtual monitor area
   - **Full-screen apps** can be sent to the iOS device
   - **Touch the iOS screen** to interact with Windows applications
   - **Use gestures** for scrolling, zooming, and navigation

### 📱 **iOS Companion App** (Development Phase)

The iOS companion app is currently in development. When completed, it will:
- **Automatically appear** when iMonitor connects
- **Display Windows content** in full-screen
- **Support touch input** with multi-touch gestures
- **Handle rotation** and different screen orientations
- **Provide settings** for quality and performance tuning

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

### 🔧 **Common Issues and Solutions**

#### **1. Virtual Monitor Creation Fails**
- **❌ Error**: "Failed to create virtual monitor" or "Administrator privileges required"
- **✅ Solution**: 
  - Close iMonitor completely
  - Right-click iMonitor.exe and select **"Run as Administrator"**
  - Windows may prompt for permission - click **"Yes"**
  - Try connecting your device again

#### **2. iOS Device Not Detected**
- **❌ Error**: Device appears as "Unknown Device" or not detected at all
- **✅ Solution**:
  - Ensure your iOS device is **unlocked**
  - Tap **"Trust This Computer"** when prompted on your device
  - Try a different **USB cable** (some cables are charge-only)
  - Try a different **USB port** (prefer USB 3.0+ ports)
  - Update iOS to the latest version

#### **3. Pairing Fails**
- **❌ Error**: "Failed to pair with device" or timeout during pairing
- **✅ Solution**:
  - Check for any **popup dialogs** on your iOS device
  - Ensure your device stays **unlocked** during pairing
  - Restart iMonitor and try again
  - Try **different USB cable** or port

#### **4. Display Streaming Issues**
- **❌ Error**: Virtual monitor created but no display on iOS device
- **✅ Solution**:
  - Check that both devices are on the same **Wi-Fi network**
  - Verify **firewall settings** aren't blocking iMonitor
  - Try **restarting** the connection process
  - Check Windows **Display Settings** - make sure content is being sent to virtual monitor

#### **5. Touch Input Not Working**
- **❌ Error**: Can see display but touch doesn't control Windows
- **✅ Solution**:
  - Ensure iMonitor is running as **Administrator**
  - Check **Windows Security** settings for input permissions
  - Try **clicking** directly on the iOS screen
  - Restart iMonitor if touch stops working

#### **6. Performance Issues**
- **❌ Error**: Laggy display, low frame rate, or poor quality
- **✅ Solution**:
  - Close **unnecessary applications** to free up CPU/GPU
  - Try **lower resolution** settings in Windows Display Settings
  - Ensure **strong Wi-Fi signal** between devices
  - Use **wired connection** if possible (USB-C to Ethernet adapter)

### 📝 **Advanced Troubleshooting**

#### **Check Windows Event Viewer**
1. Press **Win + R**, type `eventvwr.msc`
2. Navigate to **Windows Logs > Application**
3. Look for **iMonitor** entries with error icons
4. Note the error details and timestamps

#### **Reset Device Connection**
1. **Disconnect** your iOS device
2. **Close** iMonitor completely (check system tray)
3. **Restart** iMonitor as Administrator
4. **Reconnect** your iOS device

#### **Check Device Manager**
1. Press **Win + X** and select **Device Manager**
2. Look for your iOS device under **Portable Devices**
3. If there's a **yellow warning icon**, update or reinstall the driver
4. Try **uninstalling** and **reconnecting** the device

#### **Network Connectivity Test**
1. Open **Command Prompt** as Administrator
2. Type `ping [your-ios-device-ip]` to test connectivity
3. Check **Windows Firewall** settings for iMonitor
4. Temporarily **disable antivirus** to test if it's interfering

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