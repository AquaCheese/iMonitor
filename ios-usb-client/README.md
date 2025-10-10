# iMonitor iOS USB Client

This directory contains the iOS application that receives display data via USB-C connection.

## Requirements

- Xcode 14.0 or later
- iOS 13.0 or later
- iPad with USB-C port

## Features

- **USB-C Communication**: Direct high-speed connection to desktop
- **Low Latency Display**: Optimized for real-time screen mirroring
- **Touch Input**: Full touch support with gesture recognition
- **Auto-detection**: Automatic connection when plugged in
- **Power Delivery**: Charges iPad while in use

## Technical Implementation

### USB Communication Protocol

The iOS app implements a custom protocol over USB using the External Accessory Framework:

1. **Device Registration**: App registers as an MFi accessory protocol handler
2. **Session Management**: Establishes secure communication channel
3. **Data Streaming**: Receives compressed video frames via USB
4. **Input Forwarding**: Sends touch events back to desktop

### Key Components

- `USBConnectionManager`: Handles USB session lifecycle
- `DisplayRenderer`: Optimized video frame rendering
- `TouchInputHandler`: Touch event capture and forwarding
- `CompressionEngine`: Hardware-accelerated decompression

## Setup Instructions

1. Open the project in Xcode
2. Configure your Apple Developer account
3. Update the Bundle Identifier
4. Build and run on a physical iPad device
5. Connect iPad to computer via USB-C

## USB Protocol Specification

### Frame Data Format
```
Header (8 bytes):
- Magic Number (4 bytes): 0x494D4F4E ('IMON')
- Frame Size (4 bytes): Size of compressed data

Payload:
- Compressed JPEG/H.264 data
- Timestamp (8 bytes)
- Display metadata (variable)
```

### Touch Input Format
```
Touch Event (24 bytes):
- Event Type (4 bytes): Touch/Gesture type
- X Coordinate (4 bytes): Normalized 0.0-1.0
- Y Coordinate (4 bytes): Normalized 0.0-1.0
- Pressure (4 bytes): Touch pressure
- Timestamp (8 bytes): Event timestamp
```

## Building

```bash
# Build for device
xcodebuild -scheme iMonitor -destination 'generic/platform=iOS' build

# Build for simulator (limited functionality)
xcodebuild -scheme iMonitor -destination 'platform=iOS Simulator,name=iPad Pro (12.9-inch)' build
```

## Deployment

1. Connect iPad via USB-C to computer
2. Run desktop server application
3. Launch iOS app on iPad
4. App will automatically detect and connect

## Performance Optimization

- Hardware-accelerated video decoding
- Metal-based rendering pipeline
- Adaptive quality based on connection speed
- Frame buffering and prediction
- Touch event batching and smoothing

## Troubleshooting

### Connection Issues
- Ensure USB-C cable supports data transfer
- Check that iPad is unlocked and app is in foreground
- Verify desktop server is running and detecting device

### Performance Issues
- Reduce quality settings on desktop server
- Close other apps on iPad
- Use original Apple USB-C cable for best performance

### Touch Input Issues
- Calibrate touch in app settings
- Check coordinate mapping in desktop server
- Ensure proper display selection on desktop

## Known Limitations

- Requires MFi certification for App Store distribution
- USB-C iPads only (Lightning not supported)
- Requires macOS 10.15+ or Windows 10+ for desktop server
- Maximum resolution limited by USB 3.0 bandwidth

## Future Enhancements

- HDR display support
- Multi-touch gesture recognition
- Wireless fallback capability
- Apple Pencil support
- Keyboard shortcut forwarding