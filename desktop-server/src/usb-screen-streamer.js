const { EventEmitter } = require('events');

class USBScreenStreamer extends EventEmitter {
  constructor() {
    super();
    this.isStreaming = false;
    this.connectedDevices = new Map();
    this.streamingDevices = new Map();
    this.currentDisplay = null;
    this.options = {
      quality: 90,
      fps: 60,
      format: 'jpeg',
      compression: 'fast'
    };
  }

  async startUSBStreaming(display, usbDeviceId, options = {}) {
    if (this.streamingDevices.has(usbDeviceId)) {
      throw new Error(`Already streaming to device ${usbDeviceId}`);
    }

    try {
      this.currentDisplay = display;
      this.options = { ...this.options, ...options };

      console.log(`Starting USB streaming to device ${usbDeviceId}`);
      console.log('Streaming options:', this.options);

      // Create streaming session for this device
      const streamSession = {
        deviceId: usbDeviceId,
        display: display,
        options: this.options,
        frameCount: 0,
        startTime: Date.now(),
        lastFrameTime: 0
      };

      this.streamingDevices.set(usbDeviceId, streamSession);

      // Start the streaming loop for this device
      await this.startUSBStreamingLoop(usbDeviceId);

      this.isStreaming = true;
      this.emit('streaming-started', { deviceId: usbDeviceId, display });

      return true;

    } catch (error) {
      console.error('Failed to start USB streaming:', error);
      this.streamingDevices.delete(usbDeviceId);
      throw error;
    }
  }

  async startUSBStreamingLoop(deviceId) {
    const session = this.streamingDevices.get(deviceId);
    if (!session) return;

    const streamFrame = async () => {
      if (!this.streamingDevices.has(deviceId)) return;

      try {
        const now = Date.now();
        const frameInterval = 1000 / session.options.fps;
        
        // Skip frame if we're ahead of schedule
        if (now - session.lastFrameTime < frameInterval) {
          setTimeout(streamFrame, 1);
          return;
        }

        // Capture screen frame
        const frameData = await this.captureFrame(session.display, session.options);
        
        if (frameData) {
          // Send frame via USB
          await this.sendFrameViaUSB(deviceId, frameData);
          
          session.frameCount++;
          session.lastFrameTime = now;
          
          // Emit frame statistics periodically
          if (session.frameCount % 60 === 0) {
            const avgFps = session.frameCount / ((now - session.startTime) / 1000);
            this.emit('streaming-stats', {
              deviceId,
              frameCount: session.frameCount,
              avgFps: Math.round(avgFps * 10) / 10,
              currentFps: Math.round(1000 / (now - session.lastFrameTime))
            });
          }
        }

        // Schedule next frame
        setTimeout(streamFrame, Math.max(1, frameInterval - (Date.now() - now)));

      } catch (error) {
        console.error(`USB streaming error for device ${deviceId}:`, error);
        this.emit('streaming-error', { deviceId, error });
        
        // Continue streaming despite errors
        setTimeout(streamFrame, 100);
      }
    };

    // Start the streaming loop
    streamFrame();
  }

  async captureFrame(display, options) {
    try {
      const screenshot = require('screenshot-desktop');
      
      // Capture screenshot with optimized settings for USB transfer
      const imageBuffer = await screenshot({
        format: options.format,
        quality: options.quality,
        screen: display.id,
        // Add optimization for USB transfer
        width: Math.min(display.bounds.width, 2560), // Limit max resolution
        height: Math.min(display.bounds.height, 1600)
      });

      return {
        image: imageBuffer,
        format: options.format,
        timestamp: Date.now(),
        displayId: display.id,
        bounds: display.bounds,
        size: imageBuffer.length
      };

    } catch (error) {
      console.error('Screen capture error:', error);
      return null;
    }
  }

  async sendFrameViaUSB(deviceId, frameData) {
    try {
      // Get USB device manager instance
      const usbManager = this.getUSBManager();
      if (!usbManager) {
        throw new Error('USB manager not available');
      }

      // Create frame packet for USB transmission
      const framePacket = {
        type: 'SCREEN_FRAME_USB',
        data: {
          image: frameData.image.toString('base64'),
          format: frameData.format,
          timestamp: frameData.timestamp,
          displayId: frameData.displayId,
          bounds: frameData.bounds,
          compression: 'jpeg',
          size: frameData.size
        }
      };

      // Send frame data to USB device
      await usbManager.sendDataToDevice(deviceId, framePacket);

    } catch (error) {
      console.error(`Failed to send frame via USB to device ${deviceId}:`, error);
      throw error;
    }
  }

  stopUSBStreaming(deviceId) {
    if (!this.streamingDevices.has(deviceId)) {
      return false;
    }

    console.log(`Stopping USB streaming to device ${deviceId}`);
    
    const session = this.streamingDevices.get(deviceId);
    this.streamingDevices.delete(deviceId);

    // Check if any devices are still streaming
    if (this.streamingDevices.size === 0) {
      this.isStreaming = false;
    }

    this.emit('streaming-stopped', { deviceId });
    
    console.log(`USB streaming stopped for device ${deviceId}`);
    return true;
  }

  stopAllUSBStreaming() {
    const deviceIds = Array.from(this.streamingDevices.keys());
    
    for (const deviceId of deviceIds) {
      this.stopUSBStreaming(deviceId);
    }

    this.isStreaming = false;
    console.log('All USB streaming stopped');
  }

  updateStreamingOptions(deviceId, newOptions) {
    const session = this.streamingDevices.get(deviceId);
    if (!session) {
      throw new Error(`No streaming session found for device ${deviceId}`);
    }

    session.options = { ...session.options, ...newOptions };
    console.log(`Updated streaming options for device ${deviceId}:`, session.options);
    
    this.emit('streaming-options-updated', { deviceId, options: session.options });
  }

  getStreamingStats(deviceId) {
    const session = this.streamingDevices.get(deviceId);
    if (!session) {
      return null;
    }

    const now = Date.now();
    const duration = now - session.startTime;
    const avgFps = session.frameCount / (duration / 1000);

    return {
      deviceId,
      frameCount: session.frameCount,
      duration,
      avgFps: Math.round(avgFps * 10) / 10,
      currentFps: session.lastFrameTime ? Math.round(1000 / (now - session.lastFrameTime)) : 0,
      options: session.options
    };
  }

  getAllStreamingStats() {
    const stats = [];
    
    for (const deviceId of this.streamingDevices.keys()) {
      const deviceStats = this.getStreamingStats(deviceId);
      if (deviceStats) {
        stats.push(deviceStats);
      }
    }

    return stats;
  }

  isDeviceStreaming(deviceId) {
    return this.streamingDevices.has(deviceId);
  }

  getStreamingDevices() {
    return Array.from(this.streamingDevices.keys());
  }

  // This should be injected or set by the main application
  setUSBManager(usbManager) {
    this.usbManager = usbManager;
  }

  getUSBManager() {
    return this.usbManager;
  }

  cleanup() {
    console.log('Cleaning up USB screen streamer...');
    this.stopAllUSBStreaming();
    this.removeAllListeners();
  }
}

module.exports = USBScreenStreamer;