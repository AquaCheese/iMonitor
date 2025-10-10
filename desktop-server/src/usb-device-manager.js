const { EventEmitter } = require('events');
const crypto = require('crypto');

// Mock USB functionality for environments where USB libraries aren't available
let usb;
try {
  usb = require('usb');
} catch (error) {
  console.warn('USB library not available, using mock implementation');
  usb = {
    on: () => {},
    getDeviceList: () => [],
    _isMock: true
  };
}

class USBDeviceManager extends EventEmitter {
  constructor() {
    super();
    this.connectedDevices = new Map();
    this.isInitialized = false;
    this.supportedDevices = [
      // iPad device identifiers
      { vendorId: 0x05ac, productId: 0x12a8, name: 'iPad Pro 11-inch' },
      { vendorId: 0x05ac, productId: 0x12aa, name: 'iPad Pro 12.9-inch' },
      { vendorId: 0x05ac, productId: 0x12ab, name: 'iPad Air' },
      { vendorId: 0x05ac, productId: 0x1302, name: 'iPad' },
      { vendorId: 0x05ac, productId: 0x1303, name: 'iPad mini' },
      // Add more iPad variants as needed
    ];
  }

  async init() {
    if (this.isInitialized) return;

    try {
      console.log('Initializing USB device manager...');
      
      if (usb._isMock) {
        console.log('Using mock USB implementation - USB features will be simulated');
        
        // Simulate an iPad connection for demo purposes
        setTimeout(() => {
          this.simulateDeviceConnection();
        }, 2000);
      } else {
        // Set up USB event listeners
        usb.on('attach', (device) => {
          this.handleDeviceAttached(device);
        });

        usb.on('detach', (device) => {
          this.handleDeviceDetached(device);
        });

        // Scan for already connected devices
        await this.scanConnectedDevices();
      }
      
      this.isInitialized = true;
      console.log('USB device manager initialized successfully');
      
    } catch (error) {
      console.error('Failed to initialize USB device manager:', error);
      // Don't throw error, just log it
      console.warn('USB functionality will be limited');
    }
  }

  simulateDeviceConnection() {
    const mockDevice = {
      id: 'mock-ipad-001',
      info: {
        name: 'iPad Pro (Simulated)',
        serialNumber: 'MOCK123456',
        vendorId: 0x05ac,
        productId: 0x12a8
      },
      connection: {
        cleanup: () => console.log('Mock device connection cleaned up')
      },
      type: 'usb',
      connectedAt: new Date()
    };

    this.connectedDevices.set(mockDevice.id, mockDevice);

    this.emit('device-connected', {
      id: mockDevice.id,
      type: 'usb',
      name: mockDevice.info.name,
      capabilities: {
        touch: true,
        highResolution: true,
        lowLatency: true,
        wired: true
      }
    });

    console.log('Simulated iPad connection for demo purposes');
  }

  async scanConnectedDevices() {
    try {
      const devices = usb.getDeviceList();
      
      for (const device of devices) {
        if (this.isSupportedDevice(device)) {
          await this.handleDeviceAttached(device);
        }
      }
      
      console.log(`Found ${this.connectedDevices.size} supported USB devices`);
      
    } catch (error) {
      console.error('Error scanning USB devices:', error);
    }
  }

  isSupportedDevice(device) {
    return this.supportedDevices.some(supported => 
      device.deviceDescriptor.idVendor === supported.vendorId &&
      device.deviceDescriptor.idProduct === supported.productId
    );
  }

  async handleDeviceAttached(device) {
    if (!this.isSupportedDevice(device)) return;

    try {
      const deviceInfo = await this.getDeviceInfo(device);
      const deviceId = this.generateDeviceId(device);
      
      console.log(`iPad connected via USB: ${deviceInfo.name} (${deviceId})`);
      
      // Initialize device communication
      const connection = await this.initializeDeviceConnection(device, deviceInfo);
      
      this.connectedDevices.set(deviceId, {
        device,
        info: deviceInfo,
        connection,
        type: 'usb',
        connectedAt: new Date()
      });

      this.emit('device-connected', {
        id: deviceId,
        type: 'usb',
        name: deviceInfo.name,
        capabilities: {
          touch: true,
          highResolution: true,
          lowLatency: true,
          wired: true
        }
      });

    } catch (error) {
      console.error('Failed to handle device attachment:', error);
    }
  }

  handleDeviceDetached(device) {
    const deviceId = this.generateDeviceId(device);
    const deviceData = this.connectedDevices.get(deviceId);
    
    if (deviceData) {
      console.log(`iPad disconnected: ${deviceData.info.name} (${deviceId})`);
      
      // Clean up device connection
      if (deviceData.connection) {
        deviceData.connection.cleanup();
      }
      
      this.connectedDevices.delete(deviceId);
      
      this.emit('device-disconnected', {
        id: deviceId,
        type: 'usb'
      });
    }
  }

  async getDeviceInfo(device) {
    try {
      const descriptor = device.deviceDescriptor;
      const supportedDevice = this.supportedDevices.find(s => 
        s.vendorId === descriptor.idVendor && s.productId === descriptor.idProduct
      );
      
      // Try to get device strings
      let deviceName = supportedDevice ? supportedDevice.name : 'Unknown iPad';
      let serialNumber = 'Unknown';
      
      try {
        device.open();
        
        if (descriptor.iProduct) {
          deviceName = await this.getStringDescriptor(device, descriptor.iProduct);
        }
        
        if (descriptor.iSerialNumber) {
          serialNumber = await this.getStringDescriptor(device, descriptor.iSerialNumber);
        }
        
        device.close();
      } catch (error) {
        console.warn('Could not read device strings:', error.message);
      }
      
      return {
        name: deviceName,
        serialNumber,
        vendorId: descriptor.idVendor,
        productId: descriptor.idProduct,
        usbVersion: descriptor.bcdUSB,
        deviceClass: descriptor.bDeviceClass
      };
      
    } catch (error) {
      console.error('Error getting device info:', error);
      return {
        name: 'Unknown iPad',
        serialNumber: 'Unknown'
      };
    }
  }

  async getStringDescriptor(device, index) {
    return new Promise((resolve, reject) => {
      device.getStringDescriptor(index, (error, data) => {
        if (error) {
          reject(error);
        } else {
          resolve(data);
        }
      });
    });
  }

  async initializeDeviceConnection(device, deviceInfo) {
    try {
      // Create a custom connection handler for iPad communication
      const connection = new iPadUSBConnection(device, deviceInfo);
      await connection.initialize();
      return connection;
      
    } catch (error) {
      console.error('Failed to initialize device connection:', error);
      throw error;
    }
  }

  generateDeviceId(device) {
    const descriptor = device.deviceDescriptor;
    const identifier = `${descriptor.idVendor}-${descriptor.idProduct}-${device.busNumber}-${device.deviceAddress}`;
    return crypto.createHash('md5').update(identifier).digest('hex').substring(0, 8);
  }

  getConnectedDevices() {
    return Array.from(this.connectedDevices.entries()).map(([id, data]) => ({
      id,
      type: data.type,
      name: data.info.name,
      serialNumber: data.info.serialNumber,
      connectedAt: data.connectedAt,
      capabilities: {
        touch: true,
        highResolution: true,
        lowLatency: true,
        wired: true
      }
    }));
  }

  getDevice(deviceId) {
    return this.connectedDevices.get(deviceId);
  }

  async sendDataToDevice(deviceId, data) {
    const deviceData = this.connectedDevices.get(deviceId);
    if (!deviceData || !deviceData.connection) {
      throw new Error(`Device ${deviceId} not found or not connected`);
    }

    return await deviceData.connection.sendData(data);
  }

  cleanup() {
    console.log('Cleaning up USB device manager...');
    
    // Close all device connections
    for (const [deviceId, deviceData] of this.connectedDevices) {
      if (deviceData.connection) {
        deviceData.connection.cleanup();
      }
    }
    
    this.connectedDevices.clear();
    this.isInitialized = false;
  }
}

class iPadUSBConnection {
  constructor(device, deviceInfo) {
    this.device = device;
    this.deviceInfo = deviceInfo;
    this.isConnected = false;
    this.interface = null;
    this.inEndpoint = null;
    this.outEndpoint = null;
  }

  async initialize() {
    try {
      console.log(`Initializing connection to ${this.deviceInfo.name}...`);
      
      this.device.open();
      
      // Find the appropriate interface for communication
      // This is a simplified version - real implementation would need
      // to handle Apple's specific protocols and authentication
      const config = this.device.configDescriptor;
      if (config.interfaces && config.interfaces.length > 0) {
        this.interface = config.interfaces[0];
        
        // Find endpoints for communication
        if (this.interface.descriptor && this.interface.descriptor.endpoints) {
          for (const endpoint of this.interface.descriptor.endpoints) {
            if (endpoint.direction === 'in') {
              this.inEndpoint = endpoint;
            } else if (endpoint.direction === 'out') {
              this.outEndpoint = endpoint;
            }
          }
        }
      }
      
      this.isConnected = true;
      console.log(`Connection established with ${this.deviceInfo.name}`);
      
    } catch (error) {
      console.error('Failed to initialize iPad connection:', error);
      throw error;
    }
  }

  async sendData(data) {
    if (!this.isConnected || !this.outEndpoint) {
      throw new Error('Device not connected or no output endpoint available');
    }

    try {
      // Convert data to buffer if needed
      const buffer = Buffer.isBuffer(data) ? data : Buffer.from(JSON.stringify(data));
      
      // Send data to device
      return new Promise((resolve, reject) => {
        this.device.transferOut(this.outEndpoint.address, buffer, (error, transferred) => {
          if (error) {
            reject(error);
          } else {
            resolve(transferred);
          }
        });
      });
      
    } catch (error) {
      console.error('Failed to send data to iPad:', error);
      throw error;
    }
  }

  async receiveData() {
    if (!this.isConnected || !this.inEndpoint) {
      throw new Error('Device not connected or no input endpoint available');
    }

    try {
      return new Promise((resolve, reject) => {
        this.device.transferIn(this.inEndpoint.address, 1024, (error, data) => {
          if (error) {
            reject(error);
          } else {
            resolve(data);
          }
        });
      });
      
    } catch (error) {
      console.error('Failed to receive data from iPad:', error);
      throw error;
    }
  }

  cleanup() {
    try {
      if (this.device && this.isConnected) {
        this.device.close();
      }
      this.isConnected = false;
      console.log(`Connection to ${this.deviceInfo.name} closed`);
    } catch (error) {
      console.error('Error during connection cleanup:', error);
    }
  }
}

module.exports = USBDeviceManager;