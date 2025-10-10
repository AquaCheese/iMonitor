const { app, BrowserWindow, screen, ipcMain, Menu, Tray, nativeImage } = require('electron');
const path = require('path');
const ScreenCaptureServer = require('./screen-capture-server');
const InputHandler = require('./input-handler');
const NetworkManager = require('./network-manager');
const USBDeviceManager = require('./usb-device-manager');
const USBScreenStreamer = require('./usb-screen-streamer');

class DesktopApp {
  constructor() {
    this.mainWindow = null;
    this.tray = null;
    this.screenCaptureServer = null;
    this.inputHandler = null;
    this.networkManager = null;
    this.usbDeviceManager = null;
    this.usbScreenStreamer = null;
    this.isStreaming = false;
    this.connectedUSBDevices = new Map();
  }

  async init() {
    await app.whenReady();
    this.createWindow();
    this.createTray();
    this.setupIPC();
    this.initServices();
  }

  createWindow() {
    this.mainWindow = new BrowserWindow({
      width: 800,
      height: 600,
      webPreferences: {
        nodeIntegration: true,
        contextIsolation: false
      },
      icon: path.join(__dirname, '../assets/icon.png')
    });

    this.mainWindow.loadFile('src/renderer/index.html');

    // Hide to tray instead of closing
    this.mainWindow.on('close', (event) => {
      if (!app.isQuiting) {
        event.preventDefault();
        this.mainWindow.hide();
      }
    });
  }

  createTray() {
    const icon = nativeImage.createFromPath(path.join(__dirname, '../assets/tray-icon.png'));
    this.tray = new Tray(icon.resize({ width: 16, height: 16 }));
    
    const contextMenu = Menu.buildFromTemplate([
      {
        label: 'Show iMonitor',
        click: () => {
          this.mainWindow.show();
        }
      },
      {
        label: 'Start Streaming',
        click: () => {
          this.startStreaming();
        },
        enabled: !this.isStreaming
      },
      {
        label: 'Stop Streaming',
        click: () => {
          this.stopStreaming();
        },
        enabled: this.isStreaming
      },
      { type: 'separator' },
      {
        label: 'Quit',
        click: () => {
          app.isQuiting = true;
          app.quit();
        }
      }
    ]);

    this.tray.setContextMenu(contextMenu);
    this.tray.setToolTip('iMonitor Desktop Server');
    
    this.tray.on('double-click', () => {
      this.mainWindow.show();
    });
  }

  setupIPC() {
    ipcMain.handle('get-displays', () => {
      return screen.getAllDisplays().map(display => ({
        id: display.id,
        label: display.label || `Display ${display.id}`,
        bounds: display.bounds,
        workArea: display.workArea,
        scaleFactor: display.scaleFactor,
        primary: display.id === screen.getPrimaryDisplay().id
      }));
    });

    ipcMain.handle('start-streaming', async (event, displayId, options) => {
      return await this.startStreaming(displayId, options);
    });

    ipcMain.handle('stop-streaming', () => {
      return this.stopStreaming();
    });

    ipcMain.handle('get-server-info', () => {
      return this.networkManager ? this.networkManager.getServerInfo() : null;
    });

    ipcMain.handle('get-usb-devices', () => {
      return this.usbDeviceManager ? this.usbDeviceManager.getConnectedDevices() : [];
    });

    ipcMain.handle('start-usb-streaming', async (event, displayId, deviceId, options) => {
      return await this.startUSBStreaming(displayId, deviceId, options);
    });

    ipcMain.handle('stop-usb-streaming', (event, deviceId) => {
      return this.stopUSBStreaming(deviceId);
    });

    ipcMain.handle('get-usb-streaming-stats', (event, deviceId) => {
      return this.usbScreenStreamer ? this.usbScreenStreamer.getStreamingStats(deviceId) : null;
    });
  }

  async initServices() {
    this.networkManager = new NetworkManager();
    this.screenCaptureServer = new ScreenCaptureServer();
    this.inputHandler = new InputHandler();
    this.usbDeviceManager = new USBDeviceManager();
    this.usbScreenStreamer = new USBScreenStreamer();

    // Connect USB components
    this.usbScreenStreamer.setUSBManager(this.usbDeviceManager);

    await this.networkManager.init();
    
    // Initialize USB device manager
    try {
      await this.usbDeviceManager.init();
      console.log('USB device manager initialized successfully');
    } catch (error) {
      console.warn('USB device manager initialization failed:', error.message);
      console.warn('USB functionality will be disabled');
    }
    
    // Connect services
    this.networkManager.on('client-connected', (clientInfo) => {
      this.mainWindow.webContents.send('client-connected', clientInfo);
    });

    this.networkManager.on('client-disconnected', (clientId) => {
      this.mainWindow.webContents.send('client-disconnected', clientId);
    });

    this.networkManager.on('client-stream-request', ({ clientId, client, options }) => {
      this.screenCaptureServer.addClient(client);
    });

    this.networkManager.on('input-event', (inputEvent) => {
      this.inputHandler.handleInputEvent(inputEvent);
    });

    // USB device events
    this.usbDeviceManager.on('device-connected', (deviceInfo) => {
      console.log('USB device connected:', deviceInfo.name);
      this.connectedUSBDevices.set(deviceInfo.id, deviceInfo);
      this.mainWindow.webContents.send('usb-device-connected', deviceInfo);
      
      // Show notification
      const { Notification } = require('electron');
      new Notification({
        title: 'iPad Connected',
        body: `${deviceInfo.name} is now available as an extended display`,
        icon: path.join(__dirname, '../assets/icon.png')
      }).show();
    });

    this.usbDeviceManager.on('device-disconnected', (deviceInfo) => {
      console.log('USB device disconnected:', deviceInfo.id);
      this.connectedUSBDevices.delete(deviceInfo.id);
      this.mainWindow.webContents.send('usb-device-disconnected', deviceInfo.id);
      
      // Stop streaming to this device if active
      if (this.usbScreenStreamer.isDeviceStreaming(deviceInfo.id)) {
        this.usbScreenStreamer.stopUSBStreaming(deviceInfo.id);
      }
    });
  }

  async startStreaming(displayId, options = {}) {
    if (this.isStreaming) return false;

    try {
      const display = screen.getAllDisplays().find(d => d.id === displayId) || screen.getPrimaryDisplay();
      
      // Set display for input handler
      this.inputHandler.setDisplay(display);
      
      await this.screenCaptureServer.start(display, options);
      await this.networkManager.startServer();
      
      this.isStreaming = true;
      this.updateTrayMenu();
      
      console.log('Streaming started on display:', display.id);
      return {
        success: true,
        serverInfo: this.networkManager.getServerInfo()
      };
    } catch (error) {
      console.error('Failed to start streaming:', error);
      return { success: false, error: error.message };
    }
  }

  stopStreaming() {
    if (!this.isStreaming) return false;

    this.screenCaptureServer.stop();
    this.networkManager.stopServer();
    
    // Stop all USB streaming
    if (this.usbScreenStreamer) {
      this.usbScreenStreamer.stopAllUSBStreaming();
    }
    
    this.isStreaming = false;
    this.updateTrayMenu();
    
    console.log('Streaming stopped');
    return true;
  }

  async startUSBStreaming(displayId, deviceId, options = {}) {
    try {
      if (!this.usbScreenStreamer || !this.usbDeviceManager) {
        throw new Error('USB functionality not available');
      }

      const display = screen.getAllDisplays().find(d => d.id === displayId) || screen.getPrimaryDisplay();
      const device = this.usbDeviceManager.getDevice(deviceId);
      
      if (!device) {
        throw new Error(`USB device ${deviceId} not found`);
      }

      // Set display for input handler
      this.inputHandler.setDisplay(display);
      
      // Start USB streaming
      await this.usbScreenStreamer.startUSBStreaming(display, deviceId, {
        quality: 90,
        fps: 60,
        format: 'jpeg',
        ...options
      });

      console.log(`USB streaming started to ${device.info.name} on display ${display.id}`);
      
      return {
        success: true,
        deviceId: deviceId,
        displayId: display.id,
        deviceName: device.info.name
      };

    } catch (error) {
      console.error('Failed to start USB streaming:', error);
      return { 
        success: false, 
        error: error.message 
      };
    }
  }

  stopUSBStreaming(deviceId) {
    try {
      if (!this.usbScreenStreamer) {
        return { success: false, error: 'USB functionality not available' };
      }

      const result = this.usbScreenStreamer.stopUSBStreaming(deviceId);
      
      console.log(`USB streaming ${result ? 'stopped' : 'was not active'} for device ${deviceId}`);
      
      return { 
        success: result,
        deviceId: deviceId
      };

    } catch (error) {
      console.error('Failed to stop USB streaming:', error);
      return { 
        success: false, 
        error: error.message 
      };
    }
  }

  updateTrayMenu() {
    const contextMenu = Menu.buildFromTemplate([
      {
        label: 'Show iMonitor',
        click: () => this.mainWindow.show()
      },
      {
        label: 'Start Streaming',
        click: () => this.startStreaming(),
        enabled: !this.isStreaming
      },
      {
        label: 'Stop Streaming',
        click: () => this.stopStreaming(),
        enabled: this.isStreaming
      },
      { type: 'separator' },
      {
        label: 'Quit',
        click: () => {
          app.isQuiting = true;
          app.quit();
        }
      }
    ]);

    this.tray.setContextMenu(contextMenu);
  }
}

// Create and initialize the app
const desktopApp = new DesktopApp();
desktopApp.init().catch(console.error);

// App event handlers
app.on('window-all-closed', () => {
  // Keep app running in tray on all platforms
});

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) {
    desktopApp.createWindow();
  }
});