class iMonitorClient {
    constructor() {
        this.ws = null;
        this.canvas = null;
        this.ctx = null;
        this.isConnected = false;
        this.isStreaming = false;
        this.serverURL = null;
        this.touchHandler = null;
        this.displayImage = null;
        
        this.init();
    }

    init() {
        this.setupDOM();
        this.setupEventListeners();
        this.detectServerURL();
        this.setupTouchHandler();
    }

    setupDOM() {
        this.canvas = document.getElementById('display-canvas');
        this.ctx = this.canvas.getContext('2d');
        
        // Set up canvas for high DPI displays
        const dpr = window.devicePixelRatio || 1;
        this.canvas.style.width = '100vw';
        this.canvas.style.height = '100vh';
    }

    setupEventListeners() {
        // Connection button
        document.getElementById('connect-btn').addEventListener('click', () => {
            this.connect();
        });

        // Disconnect button
        document.getElementById('disconnect-btn').addEventListener('click', () => {
            this.disconnect();
        });

        // Fullscreen button
        document.getElementById('fullscreen-btn').addEventListener('click', () => {
            this.toggleFullscreen();
        });

        // Handle orientation changes
        window.addEventListener('orientationchange', () => {
            setTimeout(() => {
                this.resizeCanvas();
            }, 100);
        });

        // Handle resize
        window.addEventListener('resize', () => {
            this.resizeCanvas();
        });

        // Prevent default behaviors
        document.addEventListener('contextmenu', (e) => e.preventDefault());
        document.addEventListener('selectstart', (e) => e.preventDefault());
        document.addEventListener('dragstart', (e) => e.preventDefault());
    }

    detectServerURL() {
        // Try to detect server URL from current location or search params
        const urlParams = new URLSearchParams(window.location.search);
        const serverParam = urlParams.get('server');
        
        if (serverParam) {
            this.serverURL = serverParam;
        } else {
            // Use current host with WebSocket port
            const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
            const host = window.location.hostname || 'localhost';
            const port = 8081; // Default WebSocket port
            this.serverURL = `${protocol}//${host}:${port}`;
        }

        console.log('Detected server URL:', this.serverURL);
    }

    async connect() {
        if (this.isConnected) return;

        this.showLoading();
        this.updateStatus('connecting');

        try {
            this.ws = new WebSocket(this.serverURL);
            
            this.ws.onopen = () => {
                console.log('WebSocket connected');
                this.isConnected = true;
                this.hideLoading();
                this.updateStatus('connected');
                this.showDisplayCanvas();
                this.requestScreenStream();
            };

            this.ws.onmessage = (event) => {
                this.handleMessage(JSON.parse(event.data));
            };

            this.ws.onclose = () => {
                console.log('WebSocket disconnected');
                this.isConnected = false;
                this.isStreaming = false;
                this.updateStatus('disconnected');
                this.showConnectionScreen();
            };

            this.ws.onerror = (error) => {
                console.error('WebSocket error:', error);
                this.showError('Failed to connect to desktop server');
                this.updateStatus('disconnected');
                this.hideLoading();
            };

        } catch (error) {
            console.error('Connection error:', error);
            this.showError('Connection failed: ' + error.message);
            this.updateStatus('disconnected');
            this.hideLoading();
        }
    }

    disconnect() {
        if (this.ws) {
            this.ws.close();
            this.ws = null;
        }
        this.isConnected = false;
        this.isStreaming = false;
        this.showConnectionScreen();
    }

    handleMessage(message) {
        switch (message.type) {
            case 'WELCOME':
                console.log('Welcome message received:', message.data);
                break;
            case 'SCREEN_FRAME':
                this.handleScreenFrame(message.data);
                break;
            case 'PONG':
                // Handle ping/pong for connection keepalive
                break;
            default:
                console.log('Unknown message type:', message.type);
        }
    }

    handleScreenFrame(frameData) {
        if (!frameData.image) return;

        // Create image from base64 data
        const img = new Image();
        img.onload = () => {
            this.displayImage = img;
            this.drawFrame();
            
            if (!this.isStreaming) {
                this.isStreaming = true;
                this.showControls();
            }
        };
        
        img.src = `data:image/${frameData.format};base64,${frameData.image}`;
    }

    drawFrame() {
        if (!this.displayImage || !this.canvas) return;

        const canvasWidth = this.canvas.width;
        const canvasHeight = this.canvas.height;
        const imgWidth = this.displayImage.width;
        const imgHeight = this.displayImage.height;

        // Clear canvas
        this.ctx.clearRect(0, 0, canvasWidth, canvasHeight);

        // Calculate scaling to fit image in canvas while maintaining aspect ratio
        const scale = Math.min(canvasWidth / imgWidth, canvasHeight / imgHeight);
        const scaledWidth = imgWidth * scale;
        const scaledHeight = imgHeight * scale;

        // Center the image
        const x = (canvasWidth - scaledWidth) / 2;
        const y = (canvasHeight - scaledHeight) / 2;

        // Draw the image
        this.ctx.drawImage(this.displayImage, x, y, scaledWidth, scaledHeight);
    }

    requestScreenStream() {
        if (!this.ws || !this.isConnected) return;

        this.sendMessage({
            type: 'REQUEST_SCREEN_STREAM',
            data: {
                clientType: 'web',
                capabilities: {
                    touch: 'ontouchstart' in window,
                    mouse: true,
                    keyboard: true
                }
            }
        });
    }

    sendMessage(message) {
        if (this.ws && this.ws.readyState === WebSocket.OPEN) {
            this.ws.send(JSON.stringify(message));
        }
    }

    setupTouchHandler() {
        this.touchHandler = new TouchHandler(this.canvas, (event) => {
            this.sendInputEvent(event);
        });
    }

    sendInputEvent(event) {
        if (!this.isConnected) return;

        // Convert canvas coordinates to relative coordinates (0-1)
        const rect = this.canvas.getBoundingClientRect();
        const x = (event.x - rect.left) / rect.width;
        const y = (event.y - rect.top) / rect.height;

        this.sendMessage({
            type: 'INPUT_EVENT',
            data: {
                ...event,
                x: Math.max(0, Math.min(1, x)),
                y: Math.max(0, Math.min(1, y)),
                timestamp: Date.now()
            }
        });
    }

    resizeCanvas() {
        const dpr = window.devicePixelRatio || 1;
        const rect = this.canvas.getBoundingClientRect();
        
        this.canvas.width = rect.width * dpr;
        this.canvas.height = rect.height * dpr;
        
        this.ctx.scale(dpr, dpr);
        
        // Redraw current frame
        if (this.displayImage) {
            this.drawFrame();
        }
    }

    toggleFullscreen() {
        if (!document.fullscreenElement) {
            document.documentElement.requestFullscreen().catch(err => {
                console.log('Fullscreen request failed:', err);
            });
        } else {
            document.exitFullscreen();
        }
    }

    showConnectionScreen() {
        document.getElementById('connection-screen').style.display = 'flex';
        document.getElementById('display-canvas').style.display = 'none';
        document.getElementById('controls').classList.remove('visible');
        document.getElementById('fullscreen-btn').classList.remove('visible');
    }

    showDisplayCanvas() {
        document.getElementById('connection-screen').style.display = 'none';
        document.getElementById('display-canvas').style.display = 'block';
        document.getElementById('fullscreen-btn').classList.add('visible');
        this.resizeCanvas();
    }

    showControls() {
        document.getElementById('controls').classList.add('visible');
    }

    showLoading() {
        document.getElementById('loading').style.display = 'block';
        document.getElementById('connect-btn').disabled = true;
    }

    hideLoading() {
        document.getElementById('loading').style.display = 'none';
        document.getElementById('connect-btn').disabled = false;
    }

    showError(message) {
        document.getElementById('error-text').textContent = message;
        document.getElementById('error-message').style.display = 'block';
    }

    updateStatus(status) {
        const indicator = document.getElementById('status-indicator');
        indicator.className = `status-indicator ${status}`;
    }
}

class TouchHandler {
    constructor(element, onInputEvent) {
        this.element = element;
        this.onInputEvent = onInputEvent;
        this.isMouseDown = false;
        this.lastTouchTime = 0;
        
        this.setupEventListeners();
    }

    setupEventListeners() {
        // Touch events
        this.element.addEventListener('touchstart', (e) => this.handleTouchStart(e), { passive: false });
        this.element.addEventListener('touchmove', (e) => this.handleTouchMove(e), { passive: false });
        this.element.addEventListener('touchend', (e) => this.handleTouchEnd(e), { passive: false });

        // Mouse events (for desktop testing)
        this.element.addEventListener('mousedown', (e) => this.handleMouseDown(e));
        this.element.addEventListener('mousemove', (e) => this.handleMouseMove(e));
        this.element.addEventListener('mouseup', (e) => this.handleMouseUp(e));

        // Prevent default behaviors
        this.element.addEventListener('contextmenu', (e) => e.preventDefault());
    }

    handleTouchStart(e) {
        e.preventDefault();
        this.lastTouchTime = Date.now();
        
        const touch = e.touches[0];
        this.onInputEvent({
            eventType: 'touch',
            action: 'start',
            x: touch.clientX,
            y: touch.clientY,
            pressure: touch.force || 1
        });
    }

    handleTouchMove(e) {
        e.preventDefault();
        
        const touch = e.touches[0];
        this.onInputEvent({
            eventType: 'touch',
            action: 'move',
            x: touch.clientX,
            y: touch.clientY,
            pressure: touch.force || 1
        });
    }

    handleTouchEnd(e) {
        e.preventDefault();
        
        // Check for tap gesture
        const touchDuration = Date.now() - this.lastTouchTime;
        const wasTap = touchDuration < 200;
        
        this.onInputEvent({
            eventType: 'touch',
            action: 'end',
            x: e.changedTouches[0].clientX,
            y: e.changedTouches[0].clientY,
            wasTap: wasTap
        });
    }

    handleMouseDown(e) {
        e.preventDefault();
        this.isMouseDown = true;
        
        this.onInputEvent({
            eventType: 'mouse',
            action: 'down',
            x: e.clientX,
            y: e.clientY,
            button: e.button
        });
    }

    handleMouseMove(e) {
        if (!this.isMouseDown) return;
        
        this.onInputEvent({
            eventType: 'mouse',
            action: 'move',
            x: e.clientX,
            y: e.clientY
        });
    }

    handleMouseUp(e) {
        e.preventDefault();
        this.isMouseDown = false;
        
        this.onInputEvent({
            eventType: 'mouse',
            action: 'up',
            x: e.clientX,
            y: e.clientY,
            button: e.button
        });
    }
}

// Initialize the client when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    new iMonitorClient();
});

// Service Worker registration for PWA functionality
if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
        navigator.serviceWorker.register('/sw.js')
            .then((registration) => {
                console.log('SW registered: ', registration);
            })
            .catch((registrationError) => {
                console.log('SW registration failed: ', registrationError);
            });
    });
}