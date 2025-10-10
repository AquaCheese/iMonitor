const screenshot = require('node-screenshot-desktop');
const { EventEmitter } = require('events');

class ScreenCaptureServer extends EventEmitter {
  constructor() {
    super();
    this.isCapturing = false;
    this.captureInterval = null;
    this.display = null;
    this.options = {
      quality: 80,
      fps: 30,
      format: 'jpeg'
    };
    this.clients = new Set();
  }

  async start(display, options = {}) {
    if (this.isCapturing) {
      throw new Error('Screen capture already running');
    }

    this.display = display;
    this.options = { ...this.options, ...options };
    this.isCapturing = true;

    console.log('Starting screen capture for display:', display.id);
    console.log('Capture options:', this.options);

    // Start continuous capture
    this.startCapture();
  }

  stop() {
    if (!this.isCapturing) return;

    console.log('Stopping screen capture');
    this.isCapturing = false;
    
    if (this.captureInterval) {
      clearInterval(this.captureInterval);
      this.captureInterval = null;
    }
    
    this.clients.clear();
    this.emit('stopped');
  }

  addClient(client) {
    this.clients.add(client);
    console.log('Client added to screen capture. Total clients:', this.clients.size);
  }

  removeClient(client) {
    this.clients.delete(client);
    console.log('Client removed from screen capture. Total clients:', this.clients.size);
    
    // Stop capture if no clients
    if (this.clients.size === 0) {
      this.stop();
    }
  }

  async startCapture() {
    const captureFrame = async () => {
      if (!this.isCapturing) return;

      try {
        const screenshot = require('screenshot-desktop');
        
        const imageBuffer = await screenshot({
          format: this.options.format,
          quality: this.options.quality,
          screen: this.display.id
        });

        if (imageBuffer && this.clients.size > 0) {
          // Broadcast frame to all connected clients
          const frameData = {
            type: 'SCREEN_FRAME',
            data: {
              image: imageBuffer.toString('base64'),
              format: this.options.format,
              timestamp: Date.now(),
              displayId: this.display.id,
              bounds: this.display.bounds
            }
          };

          this.clients.forEach(client => {
            if (client.readyState === 1) { // WebSocket.OPEN
              client.send(JSON.stringify(frameData));
            }
          });
        }
      } catch (error) {
        console.error('Screen capture error:', error);
        this.emit('error', error);
      }
    };

    // Initial capture
    await captureFrame();

    // Set up interval for continuous capture
    const intervalMs = 1000 / this.options.fps;
    this.captureInterval = setInterval(captureFrame, intervalMs);
  }

  updateOptions(newOptions) {
    const oldOptions = { ...this.options };
    this.options = { ...this.options, ...newOptions };

    // Restart capture if FPS changed
    if (oldOptions.fps !== this.options.fps && this.isCapturing) {
      if (this.captureInterval) {
        clearInterval(this.captureInterval);
      }
      const intervalMs = 1000 / this.options.fps;
      this.captureInterval = setInterval(() => this.startCapture(), intervalMs);
    }

    console.log('Screen capture options updated:', this.options);
  }

  getStats() {
    return {
      isCapturing: this.isCapturing,
      clientCount: this.clients.size,
      display: this.display,
      options: this.options
    };
  }
}

module.exports = ScreenCaptureServer;