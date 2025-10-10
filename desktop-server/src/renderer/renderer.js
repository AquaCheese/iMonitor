const { ipcRenderer } = require('electron');

class DesktopRenderer {
  constructor() {
    this.displays = [];
    this.selectedDisplay = null;
    this.isStreaming = false;
    this.clients = [];
    this.usbDevices = [];
    this.serverInfo = null;

    this.init();
  }

  async init() {
    this.setupEventListeners();
    await this.loadDisplays();
    await this.loadServerInfo();
    await this.loadUSBDevices();
    this.updateUI();
  }

  setupEventListeners() {
    // Control buttons
    document.getElementById('start-btn').addEventListener('click', () => {
      this.startStreaming();
    });

    document.getElementById('stop-btn').addEventListener('click', () => {
      this.stopStreaming();
    });

    // Settings
    const qualitySlider = document.getElementById('quality-slider');
    const qualityValue = document.getElementById('quality-value');
    qualitySlider.addEventListener('input', (e) => {
      qualityValue.textContent = e.target.value + '%';
    });

    const fpsSlider = document.getElementById('fps-slider');
    const fpsValue = document.getElementById('fps-value');
    fpsSlider.addEventListener('input', (e) => {
      fpsValue.textContent = e.target.value + ' FPS';
    });

    // IPC events from main process
    ipcRenderer.on('client-connected', (event, clientInfo) => {
      this.addClient(clientInfo);
    });

    ipcRenderer.on('client-disconnected', (event, clientId) => {
      this.removeClient(clientId);
    });

    ipcRenderer.on('usb-device-connected', (event, deviceInfo) => {
      this.addUSBDevice(deviceInfo);
    });

    ipcRenderer.on('usb-device-disconnected', (event, deviceId) => {
      this.removeUSBDevice(deviceId);
    });

    // Auto-refresh server info and USB devices
    setInterval(() => {
      this.loadServerInfo();
      this.loadUSBDevices();
    }, 5000);
  }

  async loadDisplays() {
    try {
      this.displays = await ipcRenderer.invoke('get-displays');
      this.renderDisplays();
      
      // Select primary display by default
      const primaryDisplay = this.displays.find(d => d.primary);
      if (primaryDisplay) {
        this.selectDisplay(primaryDisplay.id);
      }
    } catch (error) {
      console.error('Failed to load displays:', error);
    }
  }

  renderDisplays() {
    const displayList = document.getElementById('display-list');
    displayList.innerHTML = '';

    this.displays.forEach(display => {
      const displayItem = document.createElement('div');
      displayItem.className = 'display-item';
      displayItem.dataset.displayId = display.id;
      
      const resolution = `${display.bounds.width} × ${display.bounds.height}`;
      const position = `(${display.bounds.x}, ${display.bounds.y})`;
      const primaryLabel = display.primary ? ' (Primary)' : '';
      
      displayItem.innerHTML = `
        <h4>${display.label}${primaryLabel}</h4>
        <div class="info">
          ${resolution} at ${position}<br>
          Scale: ${Math.round(display.scaleFactor * 100)}%
        </div>
      `;

      displayItem.addEventListener('click', () => {
        this.selectDisplay(display.id);
      });

      displayList.appendChild(displayItem);
    });
  }

  selectDisplay(displayId) {
    // Remove previous selection
    document.querySelectorAll('.display-item').forEach(item => {
      item.classList.remove('selected');
    });

    // Add selection to clicked display
    const displayItem = document.querySelector(`[data-display-id="${displayId}"]`);
    if (displayItem) {
      displayItem.classList.add('selected');
      this.selectedDisplay = this.displays.find(d => d.id === displayId);
    }
  }

  async startStreaming() {
    if (!this.selectedDisplay) {
      alert('Please select a display to stream');
      return;
    }

    const quality = parseInt(document.getElementById('quality-slider').value);
    const fps = parseInt(document.getElementById('fps-slider').value);

    const options = {
      quality,
      fps,
      format: 'jpeg'
    };

    try {
      const result = await ipcRenderer.invoke('start-streaming', this.selectedDisplay.id, options);
      
      if (result.success) {
        this.isStreaming = true;
        this.serverInfo = result.serverInfo;
        this.updateUI();
        this.updateConnectionInfo();
      } else {
        alert('Failed to start streaming: ' + result.error);
      }
    } catch (error) {
      console.error('Failed to start streaming:', error);
      alert('Failed to start streaming: ' + error.message);
    }
  }

  async stopStreaming() {
    try {
      const result = await ipcRenderer.invoke('stop-streaming');
      
      if (result) {
        this.isStreaming = false;
        this.clients = [];
        this.updateUI();
      }
    } catch (error) {
      console.error('Failed to stop streaming:', error);
    }
  }

  async loadServerInfo() {
    try {
      const info = await ipcRenderer.invoke('get-server-info');
      if (info) {
        this.serverInfo = info;
        this.clients = info.clients || [];
        this.updateConnectionInfo();
        this.updateClientsList();
      }
    } catch (error) {
      console.error('Failed to load server info:', error);
    }
  }

  async updateConnectionInfo() {
    if (!this.serverInfo) return;

    const urlElement = document.getElementById('connection-url');
    const qrCodeImg = document.getElementById('qr-code-img');
    
    const url = `http://${this.serverInfo.ip}:${this.serverInfo.httpPort}`;
    urlElement.textContent = url;

    // Load QR code
    try {
      const response = await fetch(`http://localhost:${this.serverInfo.httpPort}/api/qr-code`);
      const data = await response.json();
      
      qrCodeImg.src = data.qrCode;
      qrCodeImg.style.display = 'block';
    } catch (error) {
      console.error('Failed to load QR code:', error);
      qrCodeImg.style.display = 'none';
    }
  }

  updateClientsList() {
    const clientsList = document.getElementById('clients-list');
    const clientCount = document.getElementById('client-count');
    
    clientCount.textContent = this.clients.length;

    if (this.clients.length === 0) {
      clientsList.innerHTML = `
        <div class="client-item" style="opacity: 0.5; text-align: center;">
          No clients connected
        </div>
      `;
      return;
    }

    clientsList.innerHTML = '';
    this.clients.forEach(client => {
      const clientItem = document.createElement('div');
      clientItem.className = 'client-item';
      
      const connectedTime = new Date(client.connectedAt).toLocaleTimeString();
      const userAgent = this.parseUserAgent(client.userAgent);
      
      clientItem.innerHTML = `
        <div class="client-info">
          <strong>${client.id}</strong>
          <span style="color: #4CAF50;">●</span>
        </div>
        <div class="client-details">
          ${client.ip} • ${userAgent} • ${connectedTime}
        </div>
      `;
      
      clientsList.appendChild(clientItem);
    });
  }

  parseUserAgent(userAgent) {
    if (!userAgent) return 'Unknown Device';
    
    if (userAgent.includes('iPhone')) return 'iPhone';
    if (userAgent.includes('iPad')) return 'iPad';
    if (userAgent.includes('Android')) return 'Android';
    if (userAgent.includes('Chrome')) return 'Chrome';
    if (userAgent.includes('Safari')) return 'Safari';
    if (userAgent.includes('Firefox')) return 'Firefox';
    
    return 'Web Browser';
  }

  addClient(clientInfo) {
    if (!this.clients.find(c => c.id === clientInfo.id)) {
      this.clients.push(clientInfo);
      this.updateClientsList();
    }
  }

  removeClient(clientId) {
    this.clients = this.clients.filter(c => c.id !== clientId);
    this.updateClientsList();
  }

  async loadUSBDevices() {
    try {
      this.usbDevices = await ipcRenderer.invoke('get-usb-devices');
      this.updateUSBDevicesList();
    } catch (error) {
      console.error('Failed to load USB devices:', error);
    }
  }

  addUSBDevice(deviceInfo) {
    if (!this.usbDevices.find(d => d.id === deviceInfo.id)) {
      this.usbDevices.push(deviceInfo);
      this.updateUSBDevicesList();
    }
  }

  removeUSBDevice(deviceId) {
    this.usbDevices = this.usbDevices.filter(d => d.id !== deviceId);
    this.updateUSBDevicesList();
  }

  updateUSBDevicesList() {
    const usbDevicesList = document.getElementById('usb-devices-list');
    const usbDeviceCount = document.getElementById('usb-device-count');
    
    usbDeviceCount.textContent = this.usbDevices.length;

    if (this.usbDevices.length === 0) {
      usbDevicesList.innerHTML = `
        <div class="client-item" style="opacity: 0.5; text-align: center;">
          No USB devices connected
        </div>
      `;
      return;
    }

    usbDevicesList.innerHTML = '';
    this.usbDevices.forEach(device => {
      const deviceItem = document.createElement('div');
      deviceItem.className = 'usb-device-item';
      deviceItem.dataset.deviceId = device.id;
      
      const connectedTime = new Date(device.connectedAt).toLocaleTimeString();
      
      deviceItem.innerHTML = `
        <div class="usb-streaming-indicator" style="display: none;"></div>
        <div class="device-info">
          <div>
            <div class="device-name">${device.name}</div>
            <div style="font-size: 0.8rem; opacity: 0.8;">
              Serial: ${device.serialNumber} • Connected: ${connectedTime}
            </div>
          </div>
          <span style="color: #4CAF50; font-size: 0.9rem;">USB-C</span>
        </div>
        <div class="device-controls">
          <button class="start-usb-streaming" data-device-id="${device.id}">
            Start Streaming
          </button>
          <button class="stop-usb-streaming" data-device-id="${device.id}" style="display: none;">
            Stop Streaming
          </button>
        </div>
      `;

      // Add event listeners for device controls
      const startBtn = deviceItem.querySelector('.start-usb-streaming');
      const stopBtn = deviceItem.querySelector('.stop-usb-streaming');

      startBtn.addEventListener('click', () => {
        this.startUSBStreaming(device.id);
      });

      stopBtn.addEventListener('click', () => {
        this.stopUSBStreaming(device.id);
      });
      
      usbDevicesList.appendChild(deviceItem);
    });
  }

  async startUSBStreaming(deviceId) {
    if (!this.selectedDisplay) {
      alert('Please select a display to stream');
      return;
    }

    const quality = parseInt(document.getElementById('quality-slider').value);
    const fps = parseInt(document.getElementById('fps-slider').value);

    const options = {
      quality,
      fps: Math.min(fps, 60), // USB can handle higher FPS
      format: 'jpeg'
    };

    try {
      const result = await ipcRenderer.invoke('start-usb-streaming', this.selectedDisplay.id, deviceId, options);
      
      if (result.success) {
        console.log(`USB streaming started to device ${deviceId}`);
        this.updateUSBDeviceStreamingState(deviceId, true);
      } else {
        alert('Failed to start USB streaming: ' + result.error);
      }
    } catch (error) {
      console.error('Failed to start USB streaming:', error);
      alert('Failed to start USB streaming: ' + error.message);
    }
  }

  async stopUSBStreaming(deviceId) {
    try {
      const result = await ipcRenderer.invoke('stop-usb-streaming', deviceId);
      
      if (result.success) {
        console.log(`USB streaming stopped for device ${deviceId}`);
        this.updateUSBDeviceStreamingState(deviceId, false);
      } else {
        console.error('Failed to stop USB streaming:', result.error);
      }
    } catch (error) {
      console.error('Failed to stop USB streaming:', error);
    }
  }

  updateUSBDeviceStreamingState(deviceId, isStreaming) {
    const deviceItem = document.querySelector(`[data-device-id="${deviceId}"]`);
    if (!deviceItem) return;

    const startBtn = deviceItem.querySelector('.start-usb-streaming');
    const stopBtn = deviceItem.querySelector('.stop-usb-streaming');
    const indicator = deviceItem.querySelector('.usb-streaming-indicator');

    if (isStreaming) {
      deviceItem.classList.add('streaming');
      startBtn.style.display = 'none';
      stopBtn.style.display = 'inline-block';
      indicator.style.display = 'block';
    } else {
      deviceItem.classList.remove('streaming');
      startBtn.style.display = 'inline-block';
      stopBtn.style.display = 'none';
      indicator.style.display = 'none';
    }
  }

  updateUI() {
    const startBtn = document.getElementById('start-btn');
    const stopBtn = document.getElementById('stop-btn');
    const statusText = document.getElementById('status-text');
    const streamingIndicator = document.getElementById('streaming-indicator');

    if (this.isStreaming) {
      startBtn.disabled = true;
      stopBtn.disabled = false;
      statusText.textContent = 'Streaming active';
      streamingIndicator.classList.remove('stopped');
    } else {
      startBtn.disabled = false;
      stopBtn.disabled = true;
      statusText.textContent = 'Ready to stream';
      streamingIndicator.classList.add('stopped');
    }

    // Update controls based on display selection
    const hasSelectedDisplay = !!this.selectedDisplay;
    startBtn.disabled = startBtn.disabled || !hasSelectedDisplay;
  }
}

// Initialize the renderer when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
  new DesktopRenderer();
});