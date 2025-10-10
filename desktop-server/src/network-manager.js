const WebSocket = require('ws');
const express = require('express');
const cors = require('cors');
const path = require('path');
const { EventEmitter } = require('events');
const os = require('os');
const QRCode = require('qrcode');

class NetworkManager extends EventEmitter {
  constructor() {
    super();
    this.httpServer = null;
    this.wsServer = null;
    this.app = null;
    this.port = 8080;
    this.wsPort = 8081;
    this.clients = new Map();
    this.serverInfo = null;
  }

  async init() {
    this.setupHttpServer();
    this.setupWebSocketServer();
    this.serverInfo = this.generateServerInfo();
    console.log('Network manager initialized');
  }

  setupHttpServer() {
    this.app = express();
    this.app.use(cors());
    this.app.use(express.json());
    this.app.use(express.static(path.join(__dirname, '../../web-client/dist')));

    // API endpoints
    this.app.get('/api/server-info', (req, res) => {
      res.json(this.serverInfo);
    });

    this.app.get('/api/qr-code', async (req, res) => {
      try {
        const url = `http://${this.getLocalIP()}:${this.port}`;
        const qrCodeDataURL = await QRCode.toDataURL(url);
        res.json({ qrCode: qrCodeDataURL, url });
      } catch (error) {
        res.status(500).json({ error: 'Failed to generate QR code' });
      }
    });

    // Serve web client
    this.app.get('*', (req, res) => {
      res.sendFile(path.join(__dirname, '../../web-client/dist/index.html'));
    });
  }

  setupWebSocketServer() {
    this.wsServer = new WebSocket.Server({ port: this.wsPort });

    this.wsServer.on('connection', (ws, req) => {
      const clientId = this.generateClientId();
      const clientInfo = {
        id: clientId,
        ip: req.socket.remoteAddress,
        userAgent: req.headers['user-agent'],
        connectedAt: new Date(),
        ws: ws
      };

      this.clients.set(clientId, clientInfo);
      console.log(`Client connected: ${clientId} from ${clientInfo.ip}`);

      // Send welcome message
      ws.send(JSON.stringify({
        type: 'WELCOME',
        data: {
          clientId,
          serverInfo: this.serverInfo
        }
      }));

      ws.on('message', (message) => {
        this.handleClientMessage(clientId, message);
      });

      ws.on('close', () => {
        this.clients.delete(clientId);
        console.log(`Client disconnected: ${clientId}`);
        this.emit('client-disconnected', clientId);
      });

      ws.on('error', (error) => {
        console.error(`WebSocket error for client ${clientId}:`, error);
        this.clients.delete(clientId);
      });

      this.emit('client-connected', {
        id: clientId,
        ip: clientInfo.ip,
        userAgent: clientInfo.userAgent
      });
    });

    console.log(`WebSocket server listening on port ${this.wsPort}`);
  }

  handleClientMessage(clientId, message) {
    try {
      const data = JSON.parse(message);
      
      switch (data.type) {
        case 'INPUT_EVENT':
          this.emit('input-event', data.data);
          break;
        case 'REQUEST_SCREEN_STREAM':
          this.handleScreenStreamRequest(clientId, data.data);
          break;
        case 'PING':
          this.sendToClient(clientId, { type: 'PONG', data: { timestamp: Date.now() } });
          break;
        default:
          console.warn(`Unknown message type from client ${clientId}:`, data.type);
      }
    } catch (error) {
      console.error(`Error parsing message from client ${clientId}:`, error);
    }
  }

  handleScreenStreamRequest(clientId, options) {
    const client = this.clients.get(clientId);
    if (!client) return;

    // Add client to screen capture
    this.emit('client-stream-request', {
      clientId,
      client: client.ws,
      options
    });
  }

  sendToClient(clientId, message) {
    const client = this.clients.get(clientId);
    if (client && client.ws.readyState === WebSocket.OPEN) {
      client.ws.send(JSON.stringify(message));
    }
  }

  broadcastToClients(message) {
    this.clients.forEach((client, clientId) => {
      if (client.ws.readyState === WebSocket.OPEN) {
        client.ws.send(JSON.stringify(message));
      }
    });
  }

  async startServer() {
    return new Promise((resolve, reject) => {
      this.httpServer = this.app.listen(this.port, () => {
        console.log(`HTTP server listening on port ${this.port}`);
        console.log(`Access URL: http://${this.getLocalIP()}:${this.port}`);
        resolve();
      });

      this.httpServer.on('error', (error) => {
        if (error.code === 'EADDRINUSE') {
          console.log(`Port ${this.port} is busy, trying ${this.port + 1}`);
          this.port++;
          this.startServer().then(resolve).catch(reject);
        } else {
          reject(error);
        }
      });
    });
  }

  stopServer() {
    if (this.httpServer) {
      this.httpServer.close();
      this.httpServer = null;
      console.log('HTTP server stopped');
    }

    if (this.wsServer) {
      this.wsServer.close();
      this.wsServer = null;
      console.log('WebSocket server stopped');
    }

    this.clients.clear();
  }

  getLocalIP() {
    const interfaces = os.networkInterfaces();
    for (const name of Object.keys(interfaces)) {
      for (const interface of interfaces[name]) {
        if (interface.family === 'IPv4' && !interface.internal) {
          return interface.address;
        }
      }
    }
    return 'localhost';
  }

  generateClientId() {
    return Math.random().toString(36).substring(2, 15) + Math.random().toString(36).substring(2, 15);
  }

  generateServerInfo() {
    return {
      name: 'iMonitor Desktop Server',
      version: '1.0.0',
      platform: process.platform,
      hostname: os.hostname(),
      ip: this.getLocalIP(),
      httpPort: this.port,
      wsPort: this.wsPort,
      startedAt: new Date()
    };
  }

  getServerInfo() {
    return {
      ...this.serverInfo,
      clientCount: this.clients.size,
      clients: Array.from(this.clients.values()).map(client => ({
        id: client.id,
        ip: client.ip,
        userAgent: client.userAgent,
        connectedAt: client.connectedAt
      }))
    };
  }

  getConnectedClients() {
    return Array.from(this.clients.values()).map(client => ({
      id: client.id,
      ip: client.ip,
      userAgent: client.userAgent,
      connectedAt: client.connectedAt
    }));
  }
}

module.exports = NetworkManager;