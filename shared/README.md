# Shared Protocol Definitions

This directory contains shared protocols, utilities, and types used across all iMonitor components.

## Message Protocol

All communication between desktop server and clients uses JSON messages over WebSocket/WebRTC.

### Message Types

- `SCREEN_STREAM`: Video stream data
- `INPUT_EVENT`: Mouse/touch/keyboard events
- `DISPLAY_INFO`: Display configuration and capabilities
- `CONNECTION_STATUS`: Connection state updates
- `SETTINGS`: Quality and configuration settings

### Input Events

Input events are normalized across platforms:

```javascript
{
  type: 'INPUT_EVENT',
  data: {
    eventType: 'mouse' | 'touch' | 'keyboard',
    action: 'down' | 'move' | 'up' | 'scroll',
    x: number,
    y: number,
    button?: number,
    key?: string,
    timestamp: number
  }
}
```

### Display Configuration

```javascript
{
  type: 'DISPLAY_INFO',
  data: {
    displays: [{
      id: string,
      name: string,
      width: number,
      height: number,
      x: number,
      y: number,
      primary: boolean
    }]
  }
}
```