# iMonitor

A cross-platform solution for using mobile devices (iPad, iPhone, Android) as extended displays for your computer. Similar to Spacedesk and Duet Display, iMonitor allows you to wirelessly extend your desktop to mobile devices with full touch input support.

## Features

- 🖥️ **Cross-platform desktop server** - Works on Windows, macOS, and Linux
- 📱 **Mobile client support** - Native apps for iOS and Android
- 🌐 **Web client** - Use any device with a modern browser
- 🔄 **Real-time streaming** - Low-latency screen mirroring
- 👆 **Touch input forwarding** - Use your mobile device as a touchscreen
- 🎯 **Multi-display support** - Extend or mirror multiple displays
- ⚙️ **Configurable quality** - Adjust streaming quality for your network
- 🔌 **USB-C wired connection** - Connect iPad directly via USB-C for ultra-low latency
- ⚡ **High performance** - Up to 60fps streaming with hardware acceleration
- 🔋 **Power delivery** - Charge your iPad while using it as a display

## Project Structure

```
iMonitor/
├── desktop-server/     # Electron-based desktop application
├── web-client/         # Browser-based client application
├── mobile-client/      # React Native mobile application
├── ios-usb-client/     # Native iOS app for USB-C connection
├── shared/            # Shared utilities and protocols
└── docs/              # Documentation and guides
```

## Quick Start

### Desktop Server (Host Computer)
```bash
cd desktop-server
npm install
npm start
```

### Option 1: USB-C Connection (iPad Only - Recommended)
1. Connect your iPad to your computer using a USB-C cable
2. The desktop app will automatically detect your iPad
3. Select your display and click "Start Streaming" next to your iPad
4. Enjoy ultra-low latency extended display with charging!

### Option 2: Web Client (Any Mobile Device)
Open your mobile browser and navigate to the IP address shown by the desktop server.

### Option 3: Native Mobile App
Install the iMonitor app from the App Store (iOS) or Google Play Store (Android).

## How It Works

### Wireless Mode
1. **Screen Capture**: The desktop server captures your screen content
2. **Video Encoding**: Screen content is encoded into a video stream
3. **Network Streaming**: Stream is sent over your local network using WebSocket
4. **Display**: Mobile/web client receives and displays the stream
5. **Input Forwarding**: Touch/mouse events are sent back to the desktop

### USB-C Wired Mode (iPad)
1. **USB Detection**: Desktop server automatically detects connected iPad
2. **High-Speed Transfer**: Screen data is sent directly via USB 3.0/Thunderbolt
3. **Hardware Acceleration**: Uses GPU encoding/decoding for 60fps performance
4. **Low Latency**: Direct connection eliminates network latency
5. **Power Delivery**: iPad charges while in use

## Development

Each component can be developed independently:

- **Desktop Server**: Electron + Node.js + USB Device Management
- **Web Client**: HTML5 + WebSocket + Canvas
- **Mobile Client**: React Native
- **iOS USB Client**: Native iOS + External Accessory Framework
- **Shared**: Common protocols and utilities

## License

MIT License - see LICENSE file for details.