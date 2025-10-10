// Shared protocol definitions for iMonitor
// Used by desktop server, web client, and mobile client

// Message types
export const MESSAGE_TYPES = {
  // Connection management
  WELCOME: 'WELCOME',
  PING: 'PING',
  PONG: 'PONG',
  
  // Screen streaming
  REQUEST_SCREEN_STREAM: 'REQUEST_SCREEN_STREAM',
  SCREEN_FRAME: 'SCREEN_FRAME',
  STREAM_SETTINGS: 'STREAM_SETTINGS',
  
  // Input events
  INPUT_EVENT: 'INPUT_EVENT',
  
  // Display management
  DISPLAY_INFO: 'DISPLAY_INFO',
  
  // Status and control
  CONNECTION_STATUS: 'CONNECTION_STATUS',
  ERROR: 'ERROR'
};

// Input event types
export const INPUT_TYPES = {
  MOUSE: 'mouse',
  TOUCH: 'touch',
  KEYBOARD: 'keyboard',
  SCROLL: 'scroll'
};

// Input actions
export const INPUT_ACTIONS = {
  DOWN: 'down',
  UP: 'up',
  MOVE: 'move',
  START: 'start',
  END: 'end',
  SCROLL: 'scroll'
};

// Connection states
export const CONNECTION_STATES = {
  DISCONNECTED: 'disconnected',
  CONNECTING: 'connecting',
  CONNECTED: 'connected',
  ERROR: 'error'
};

// Stream quality presets
export const QUALITY_PRESETS = {
  LOW: {
    quality: 60,
    fps: 15,
    format: 'jpeg'
  },
  MEDIUM: {
    quality: 75,
    fps: 24,
    format: 'jpeg'
  },
  HIGH: {
    quality: 85,
    fps: 30,
    format: 'jpeg'
  },
  ULTRA: {
    quality: 95,
    fps: 60,
    format: 'jpeg'
  }
};

// Protocol validation functions
export const validateMessage = (message) => {
  if (!message || typeof message !== 'object') {
    return { valid: false, error: 'Message must be an object' };
  }
  
  if (!message.type || !Object.values(MESSAGE_TYPES).includes(message.type)) {
    return { valid: false, error: 'Invalid or missing message type' };
  }
  
  return { valid: true };
};

export const validateInputEvent = (event) => {
  if (!event || typeof event !== 'object') {
    return { valid: false, error: 'Input event must be an object' };
  }
  
  if (!event.eventType || !Object.values(INPUT_TYPES).includes(event.eventType)) {
    return { valid: false, error: 'Invalid or missing event type' };
  }
  
  if (!event.action || !Object.values(INPUT_ACTIONS).includes(event.action)) {
    return { valid: false, error: 'Invalid or missing action' };
  }
  
  if (typeof event.x !== 'number' || typeof event.y !== 'number') {
    return { valid: false, error: 'Invalid coordinates' };
  }
  
  if (event.x < 0 || event.x > 1 || event.y < 0 || event.y > 1) {
    return { valid: false, error: 'Coordinates must be normalized (0-1)' };
  }
  
  return { valid: true };
};

// Message factory functions
export const createWelcomeMessage = (clientId, serverInfo) => ({
  type: MESSAGE_TYPES.WELCOME,
  data: {
    clientId,
    serverInfo,
    timestamp: Date.now()
  }
});

export const createScreenFrameMessage = (imageData, format, displayInfo) => ({
  type: MESSAGE_TYPES.SCREEN_FRAME,
  data: {
    image: imageData,
    format,
    timestamp: Date.now(),
    displayId: displayInfo.id,
    bounds: displayInfo.bounds
  }
});

export const createInputEventMessage = (eventType, action, x, y, additionalData = {}) => ({
  type: MESSAGE_TYPES.INPUT_EVENT,
  data: {
    eventType,
    action,
    x,
    y,
    timestamp: Date.now(),
    ...additionalData
  }
});

export const createErrorMessage = (error, context = null) => ({
  type: MESSAGE_TYPES.ERROR,
  data: {
    error: error.message || error,
    context,
    timestamp: Date.now()
  }
});

// Utility functions
export const normalizeCoordinates = (x, y, containerWidth, containerHeight) => ({
  x: Math.max(0, Math.min(1, x / containerWidth)),
  y: Math.max(0, Math.min(1, y / containerHeight))
});

export const denormalizeCoordinates = (normalizedX, normalizedY, targetWidth, targetHeight) => ({
  x: Math.round(normalizedX * targetWidth),
  y: Math.round(normalizedY * targetHeight)
});

// Configuration constants
export const CONFIG = {
  DEFAULT_HTTP_PORT: 8080,
  DEFAULT_WS_PORT: 8081,
  MAX_RECONNECT_ATTEMPTS: 5,
  RECONNECT_DELAY: 2000,
  PING_INTERVAL: 30000,
  STREAM_TIMEOUT: 5000,
  MAX_FRAME_SIZE: 5 * 1024 * 1024, // 5MB
  SUPPORTED_FORMATS: ['jpeg', 'png', 'webp']
};