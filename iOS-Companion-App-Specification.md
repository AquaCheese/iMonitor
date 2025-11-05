# iMonitor iOS Companion App

This document outlines the iOS companion app needed to receive and display content from the Windows iMonitor application.

## App Overview

The iOS companion app receives display frames from the Windows iMonitor application and displays them full-screen on the iOS device. It also forwards touch input back to the Windows computer.

## Key Features

### 1. **Display Reception**
- Receives compressed video frames via USB or Wi-Fi
- Decodes H.264, MJPEG, or raw RGB frames
- Displays frames at 60 FPS with low latency
- Handles resolution scaling and aspect ratio

### 2. **Touch Input Forwarding**
- Captures all touch events (begin, move, end)
- Normalizes coordinates to 0.0-1.0 range
- Sends touch data to Windows with precise timing
- Supports multi-touch gestures

### 3. **Connection Management**
- Auto-discovers Windows iMonitor instances
- Handles pairing and authentication
- Maintains connection reliability
- Reconnects automatically on disconnection

## Technical Implementation

### Communication Protocol

The app communicates with Windows using the IOSMessageType protocol defined in the Windows service:

```
Message Types:
- DeviceInfo (0x00010001): Device capabilities exchange
- PairingRequest/Response (0x00010002/0x00010003): Device pairing
- DisplayStreamStart/Stop (0x00020001/0x00020002): Stream control
- DisplayFrame (0x00020003): Video frame data
- TouchInput (0x00030001): Touch event data
- Heartbeat (0x00040001): Connection keepalive
```

### iOS App Structure

```
iMonitor iOS App/
├── AppDelegate.swift
├── ViewController.swift          # Main display controller
├── CommunicationManager.swift    # Handles Windows communication
├── DisplayRenderer.swift         # Video frame rendering
├── TouchHandler.swift           # Touch input processing
├── ConnectionSetup.swift        # Pairing and setup UI
└── Info.plist                  # USB accessory configuration
```

### Key Components

#### 1. **CommunicationManager.swift**
```swift
class CommunicationManager {
    // USB communication using External Accessory framework
    // Network communication using CFSocket or NSURLSession
    // Protocol message handling
    // Connection state management
}
```

#### 2. **DisplayRenderer.swift**
```swift
class DisplayRenderer {
    // Metal-based video rendering for performance
    // H.264 hardware decoding using VideoToolbox
    // Frame timing and synchronization
    // Aspect ratio handling
}
```

#### 3. **TouchHandler.swift**
```swift
class TouchHandler {
    // UITouch event capture
    // Coordinate normalization
    // Multi-touch support
    // Gesture recognition
}
```

## Required iOS Frameworks

- **ExternalAccessory**: For USB communication
- **VideoToolbox**: For hardware H.264 decoding
- **Metal**: For high-performance video rendering
- **CoreFoundation**: For low-level networking
- **UIKit**: For touch input and UI

## USB Accessory Configuration

The app needs to declare USB accessory support in Info.plist:

```xml
<key>UISupportedExternalAccessoryProtocols</key>
<array>
    <string>com.imonitor.display</string>
</array>
```

## App Store Considerations

### MFi (Made for iPhone) Program
For USB communication, the app may need to work with MFi-certified accessories or use alternative approaches:

1. **Network-only approach**: Use Wi-Fi for all communication
2. **MFi partnership**: Work with accessory manufacturers
3. **Developer/Enterprise distribution**: Bypass App Store restrictions

### Alternative Distribution
- **TestFlight**: For beta testing and development
- **Enterprise distribution**: For corporate use
- **Developer installation**: For personal use via Xcode

## Development Phases

### Phase 1: Basic Connectivity
- [x] USB device detection on Windows
- [x] Basic communication protocol
- [ ] iOS app skeleton with USB connectivity
- [ ] Simple pairing process

### Phase 2: Video Streaming
- [x] Windows screen capture
- [x] Frame compression and streaming
- [ ] iOS video decoding and display
- [ ] Performance optimization

### Phase 3: Touch Input
- [ ] iOS touch capture
- [ ] Touch coordinate normalization  
- [ ] Windows touch injection
- [ ] Multi-touch support

### Phase 4: Production Polish
- [ ] Error handling and recovery
- [ ] User interface improvements
- [ ] Performance monitoring
- [ ] App Store submission (if applicable)

## Sample iOS Code Structure

### Main Display View Controller
```swift
class DisplayViewController: UIViewController {
    private let metalView = MTKView()
    private let communicationManager = CommunicationManager()
    private let displayRenderer = DisplayRenderer()
    private let touchHandler = TouchHandler()
    
    override func viewDidLoad() {
        super.viewDidLoad()
        setupDisplay()
        setupCommunication()
        setupTouchHandling()
    }
    
    private func setupDisplay() {
        // Configure Metal view for video rendering
        // Set up full-screen display
    }
    
    private func setupCommunication() {
        // Initialize USB/network communication
        // Handle pairing and authentication
    }
    
    private func setupTouchHandling() {
        // Configure touch input forwarding
        // Set up gesture recognizers
    }
}
```

### Communication Manager
```swift
class CommunicationManager: NSObject {
    private var accessory: EAAccessory?
    private var session: EASession?
    
    func startUSBCommunication() {
        // Initialize External Accessory session
        // Handle protocol-specific communication
    }
    
    func sendTouchInput(_ touch: TouchInput) {
        // Encode and send touch data
    }
    
    func handleReceivedFrame(_ frameData: Data) {
        // Process received video frame
        // Pass to display renderer
    }
}
```

## Next Steps

To complete the iOS companion app:

1. **Set up iOS project** with required frameworks
2. **Implement USB communication** using External Accessory
3. **Add video decoding** with VideoToolbox
4. **Create Metal renderer** for display
5. **Implement touch forwarding**
6. **Test with actual iPhone**

The Windows side is now fully implemented and ready to communicate with an iOS companion app following this specification.