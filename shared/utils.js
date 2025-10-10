// Utility functions for iMonitor clients and server
// Shared across all platforms

export class EventEmitter {
  constructor() {
    this.events = {};
  }

  on(event, callback) {
    if (!this.events[event]) {
      this.events[event] = [];
    }
    this.events[event].push(callback);
    
    // Return unsubscribe function
    return () => {
      this.off(event, callback);
    };
  }

  off(event, callback) {
    if (!this.events[event]) return;
    
    const index = this.events[event].indexOf(callback);
    if (index > -1) {
      this.events[event].splice(index, 1);
    }
  }

  emit(event, ...args) {
    if (!this.events[event]) return;
    
    this.events[event].forEach(callback => {
      try {
        callback(...args);
      } catch (error) {
        console.error(`Error in event handler for '${event}':`, error);
      }
    });
  }

  removeAllListeners(event = null) {
    if (event) {
      delete this.events[event];
    } else {
      this.events = {};
    }
  }
}

export class Logger {
  constructor(prefix = '') {
    this.prefix = prefix;
    this.logLevel = 'info'; // 'debug', 'info', 'warn', 'error'
  }

  setLevel(level) {
    this.logLevel = level;
  }

  debug(...args) {
    if (this.shouldLog('debug')) {
      console.log(`[DEBUG]${this.prefix}`, ...args);
    }
  }

  info(...args) {
    if (this.shouldLog('info')) {
      console.log(`[INFO]${this.prefix}`, ...args);
    }
  }

  warn(...args) {
    if (this.shouldLog('warn')) {
      console.warn(`[WARN]${this.prefix}`, ...args);
    }
  }

  error(...args) {
    if (this.shouldLog('error')) {
      console.error(`[ERROR]${this.prefix}`, ...args);
    }
  }

  shouldLog(level) {
    const levels = ['debug', 'info', 'warn', 'error'];
    return levels.indexOf(level) >= levels.indexOf(this.logLevel);
  }
}

export class ConnectionManager extends EventEmitter {
  constructor(url, options = {}) {
    super();
    this.url = url;
    this.ws = null;
    this.isConnected = false;
    this.reconnectAttempts = 0;
    this.maxReconnectAttempts = options.maxReconnectAttempts || 5;
    this.reconnectDelay = options.reconnectDelay || 2000;
    this.pingInterval = options.pingInterval || 30000;
    this.pingTimer = null;
    this.logger = new Logger(' [ConnectionManager]');
  }

  async connect() {
    if (this.isConnected) {
      this.logger.warn('Already connected');
      return;
    }

    try {
      this.ws = new WebSocket(this.url);
      
      this.ws.onopen = () => {
        this.logger.info('Connected to', this.url);
        this.isConnected = true;
        this.reconnectAttempts = 0;
        this.startPing();
        this.emit('connected');
      };

      this.ws.onmessage = (event) => {
        try {
          const message = JSON.parse(event.data);
          this.handleMessage(message);
        } catch (error) {
          this.logger.error('Failed to parse message:', error);
        }
      };

      this.ws.onclose = (event) => {
        this.logger.info('Connection closed:', event.code, event.reason);
        this.isConnected = false;
        this.stopPing();
        this.emit('disconnected', event);
        
        if (event.code !== 1000 && this.reconnectAttempts < this.maxReconnectAttempts) {
          this.scheduleReconnect();
        }
      };

      this.ws.onerror = (error) => {
        this.logger.error('WebSocket error:', error);
        this.emit('error', error);
      };

    } catch (error) {
      this.logger.error('Failed to connect:', error);
      this.emit('error', error);
      throw error;
    }
  }

  disconnect() {
    if (this.ws) {
      this.ws.close(1000, 'Client disconnect');
      this.ws = null;
    }
    this.isConnected = false;
    this.stopPing();
  }

  send(message) {
    if (!this.isConnected || !this.ws) {
      this.logger.warn('Cannot send message: not connected');
      return false;
    }

    try {
      this.ws.send(JSON.stringify(message));
      return true;
    } catch (error) {
      this.logger.error('Failed to send message:', error);
      return false;
    }
  }

  handleMessage(message) {
    if (message.type === 'PONG') {
      this.emit('pong', message.data);
    } else {
      this.emit('message', message);
    }
  }

  startPing() {
    this.stopPing();
    this.pingTimer = setInterval(() => {
      this.send({ type: 'PING', data: { timestamp: Date.now() } });
    }, this.pingInterval);
  }

  stopPing() {
    if (this.pingTimer) {
      clearInterval(this.pingTimer);
      this.pingTimer = null;
    }
  }

  scheduleReconnect() {
    const delay = this.reconnectDelay * Math.pow(2, this.reconnectAttempts);
    this.reconnectAttempts++;
    
    this.logger.info(`Reconnecting in ${delay}ms (attempt ${this.reconnectAttempts}/${this.maxReconnectAttempts})`);
    
    setTimeout(() => {
      if (!this.isConnected) {
        this.connect();
      }
    }, delay);
  }
}

export class TouchGestureRecognizer {
  constructor() {
    this.touches = new Map();
    this.gestureThreshold = 10; // pixels
    this.tapTimeout = 300; // ms
    this.longPressTimeout = 500; // ms
  }

  handleTouchStart(touches) {
    const now = Date.now();
    
    for (const touch of touches) {
      this.touches.set(touch.identifier, {
        startX: touch.clientX,
        startY: touch.clientY,
        currentX: touch.clientX,
        currentY: touch.clientY,
        startTime: now,
        moved: false
      });
    }

    return this.analyzeGesture();
  }

  handleTouchMove(touches) {
    for (const touch of touches) {
      const trackingData = this.touches.get(touch.identifier);
      if (trackingData) {
        const deltaX = Math.abs(touch.clientX - trackingData.startX);
        const deltaY = Math.abs(touch.clientY - trackingData.startY);
        
        trackingData.currentX = touch.clientX;
        trackingData.currentY = touch.clientY;
        
        if (deltaX > this.gestureThreshold || deltaY > this.gestureThreshold) {
          trackingData.moved = true;
        }
      }
    }

    return this.analyzeGesture();
  }

  handleTouchEnd(touches) {
    const now = Date.now();
    let gesture = null;

    for (const touch of touches) {
      const trackingData = this.touches.get(touch.identifier);
      if (trackingData) {
        const duration = now - trackingData.startTime;
        
        if (!trackingData.moved && duration < this.tapTimeout) {
          gesture = {
            type: 'tap',
            x: touch.clientX,
            y: touch.clientY,
            duration
          };
        } else if (!trackingData.moved && duration >= this.longPressTimeout) {
          gesture = {
            type: 'longpress',
            x: touch.clientX,
            y: touch.clientY,
            duration
          };
        }
        
        this.touches.delete(touch.identifier);
      }
    }

    return gesture || this.analyzeGesture();
  }

  analyzeGesture() {
    const activeTouches = Array.from(this.touches.values());
    
    if (activeTouches.length === 0) {
      return null;
    }
    
    if (activeTouches.length === 1) {
      const touch = activeTouches[0];
      return {
        type: touch.moved ? 'drag' : 'hold',
        x: touch.currentX,
        y: touch.currentY,
        startX: touch.startX,
        startY: touch.startY
      };
    }
    
    if (activeTouches.length === 2) {
      const [touch1, touch2] = activeTouches;
      const distance = Math.sqrt(
        Math.pow(touch2.currentX - touch1.currentX, 2) +
        Math.pow(touch2.currentY - touch1.currentY, 2)
      );
      
      return {
        type: 'pinch',
        centerX: (touch1.currentX + touch2.currentX) / 2,
        centerY: (touch1.currentY + touch2.currentY) / 2,
        distance
      };
    }
    
    return null;
  }

  reset() {
    this.touches.clear();
  }
}

export const debounce = (func, wait) => {
  let timeout;
  return function executedFunction(...args) {
    const later = () => {
      clearTimeout(timeout);
      func(...args);
    };
    clearTimeout(timeout);
    timeout = setTimeout(later, wait);
  };
};

export const throttle = (func, limit) => {
  let inThrottle;
  return function executedFunction(...args) {
    if (!inThrottle) {
      func.apply(this, args);
      inThrottle = true;
      setTimeout(() => (inThrottle = false), limit);
    }
  };
};

export const formatBytes = (bytes, decimals = 2) => {
  if (bytes === 0) return '0 Bytes';
  
  const k = 1024;
  const dm = decimals < 0 ? 0 : decimals;
  const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
  
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  
  return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
};

export const getDeviceInfo = () => {
  const userAgent = navigator.userAgent;
  const platform = navigator.platform;
  
  return {
    userAgent,
    platform,
    isMobile: /Android|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(userAgent),
    isTablet: /iPad|Android(?!.*Mobile)/i.test(userAgent),
    isIOS: /iPad|iPhone|iPod/.test(userAgent),
    isAndroid: /Android/.test(userAgent),
    screen: {
      width: screen.width,
      height: screen.height,
      pixelRatio: window.devicePixelRatio || 1
    },
    viewport: {
      width: window.innerWidth,
      height: window.innerHeight
    }
  };
};