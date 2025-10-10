#!/usr/bin/env node
/**
 * iMonitor Server-Only Mode
 * Runs the core server functionality without Electron GUI
 */

const WebSocket = require('ws');
const http = require('http');
const path = require('path');
const fs = require('fs');

// Mock screen capture for demo mode
const mockScreenshot = () => {
    // Create a simple colored rectangle as mock screenshot
    const width = 1920;
    const height = 1080;
    const canvas = Buffer.alloc(width * height * 4);
    
    // Fill with gradient colors
    for (let y = 0; y < height; y++) {
        for (let x = 0; x < width; x++) {
            const offset = (y * width + x) * 4;
            canvas[offset] = Math.floor((x / width) * 255);     // Red
            canvas[offset + 1] = Math.floor((y / height) * 255); // Green  
            canvas[offset + 2] = 100;                           // Blue
            canvas[offset + 3] = 255;                           // Alpha
        }
    }
    
    return {
        data: canvas,
        width,
        height
    };
};

class iMonitorServer {
    constructor() {
        this.port = process.env.PORT || 8080;
        this.clients = new Set();
        this.isStreaming = false;
        this.setupServer();
    }

    setupServer() {
        // Create HTTP server for status and API
        this.httpServer = http.createServer((req, res) => {
            if (req.url === '/status') {
                res.writeHead(200, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({
                    status: 'running',
                    clients: this.clients.size,
                    streaming: this.isStreaming,
                    mode: 'server-only'
                }));
            } else if (req.url === '/') {
                res.writeHead(200, { 'Content-Type': 'text/html' });
                res.end(`
                    <html>
                    <head><title>iMonitor Server</title></head>
                    <body>
                        <h1>🖥️ iMonitor Server Running</h1>
                        <p>Status: <strong>Active</strong></p>
                        <p>Connected clients: <span id="clients">${this.clients.size}</span></p>
                        <p>Mode: Server-Only (No GUI)</p>
                        <p>WebSocket Port: ${this.port}</p>
                        <hr>
                        <p>Connect your mobile device to: <strong>http://localhost:3000</strong></p>
                        <p>Or scan QR code from mobile app</p>
                        <script>
                            // Auto-refresh client count
                            setInterval(() => {
                                fetch('/status').then(r => r.json()).then(data => {
                                    document.getElementById('clients').textContent = data.clients;
                                });
                            }, 2000);
                        </script>
                    </body>
                    </html>
                `);
            } else {
                res.writeHead(404);
                res.end('Not Found');
            }
        });

        // Create WebSocket server
        this.wss = new WebSocket.Server({ 
            server: this.httpServer,
            path: '/ws'
        });

        this.wss.on('connection', (ws, req) => {
            console.log(`📱 Client connected from ${req.socket.remoteAddress}`);
            this.clients.add(ws);

            // Send welcome message
            ws.send(JSON.stringify({
                type: 'welcome',
                message: 'Connected to iMonitor Server',
                serverTime: new Date().toISOString()
            }));

            // Handle messages
            ws.on('message', (data) => {
                try {
                    const message = JSON.parse(data.toString());
                    this.handleMessage(ws, message);
                } catch (error) {
                    console.error('Invalid message:', error);
                }
            });

            // Handle disconnect
            ws.on('close', () => {
                console.log('📱 Client disconnected');
                this.clients.delete(ws);
            });

            // Handle errors
            ws.on('error', (error) => {
                console.error('WebSocket error:', error);
                this.clients.delete(ws);
            });
        });
    }

    handleMessage(ws, message) {
        console.log('📨 Received:', message.type);

        switch (message.type) {
            case 'start_stream':
                this.startStreaming(ws);
                break;
            case 'stop_stream':
                this.stopStreaming(ws);
                break;
            case 'input_event':
                this.handleInput(message.data);
                break;
            case 'ping':
                ws.send(JSON.stringify({ type: 'pong', timestamp: Date.now() }));
                break;
            default:
                ws.send(JSON.stringify({ 
                    type: 'error', 
                    message: `Unknown message type: ${message.type}` 
                }));
        }
    }

    startStreaming(ws) {
        console.log('🎬 Starting screen stream (demo mode)');
        this.isStreaming = true;

        // Send initial screen data
        const screenshot = mockScreenshot();
        ws.send(JSON.stringify({
            type: 'screen_frame',
            width: screenshot.width,
            height: screenshot.height,
            data: screenshot.data.toString('base64')
        }));

        // Start streaming loop (demo with static image)
        this.streamInterval = setInterval(() => {
            if (ws.readyState === WebSocket.OPEN) {
                const screenshot = mockScreenshot();
                ws.send(JSON.stringify({
                    type: 'screen_frame',
                    width: screenshot.width,
                    height: screenshot.height,
                    timestamp: Date.now()
                }));
            }
        }, 1000 / 10); // 10 FPS for demo

        ws.send(JSON.stringify({
            type: 'stream_started',
            fps: 10,
            resolution: { width: 1920, height: 1080 }
        }));
    }

    stopStreaming(ws) {
        console.log('⏹️ Stopping screen stream');
        this.isStreaming = false;
        
        if (this.streamInterval) {
            clearInterval(this.streamInterval);
            this.streamInterval = null;
        }

        ws.send(JSON.stringify({
            type: 'stream_stopped'
        }));
    }

    handleInput(inputData) {
        console.log('🖱️ Input event:', inputData.type, inputData);
        // In a real implementation, this would forward to robotjs or similar
        // For demo mode, just log the events
    }

    start() {
        this.httpServer.listen(this.port, () => {
            console.log('\n🚀 iMonitor Server Started!');
            console.log('===============================');
            console.log(`📡 Server running on: http://localhost:${this.port}`);
            console.log(`🔌 WebSocket endpoint: ws://localhost:${this.port}/ws`);
            console.log(`📱 Web client: http://localhost:3000`);
            console.log('💡 Mode: Server-Only (Demo Screenshots)');
            console.log('\n📋 Available endpoints:');
            console.log(`   GET  /       - Server dashboard`);
            console.log(`   GET  /status - Server status JSON`);
            console.log(`   WS   /ws     - WebSocket connection`);
            console.log('\n🔧 To enable real screen capture:');
            console.log('   - Install desktop environment');
            console.log('   - Run with proper display setup');
            console.log('   - Use ./start.sh for full GUI mode');
            console.log('===============================\n');
        });
    }
}

// Start the server
const server = new iMonitorServer();
server.start();

// Handle graceful shutdown
process.on('SIGINT', () => {
    console.log('\n👋 Shutting down iMonitor Server...');
    process.exit(0);
});