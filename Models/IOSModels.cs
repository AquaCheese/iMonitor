using System;

namespace iMonitor.Models;

#region iOS Communication Models

/// <summary>
/// iOS device communication protocol messages
/// </summary>
public enum IOSMessageType : uint
{
    DeviceInfo = 0x00010001,
    PairingRequest = 0x00010002,
    PairingResponse = 0x00010003,
    DisplayStreamStart = 0x00020001,
    DisplayStreamStop = 0x00020002,
    DisplayFrame = 0x00020003,
    TouchInput = 0x00030001,
    Heartbeat = 0x00040001,
    Error = 0x00FF0001
}

public struct IOSDeviceInfo
{
    public string DeviceName;
    public string DeviceModel;
    public string IOSVersion;
    public uint ScreenWidth;
    public uint ScreenHeight;
    public uint ScreenScale;        // 1x, 2x, 3x for Retina displays
    public uint SupportedFormats;   // Bitfield for supported video formats
}

public struct IOSTouchInput
{
    public uint TouchId;
    public uint TouchType;          // 0=Begin, 1=Move, 2=End
    public float X;                 // Normalized 0.0-1.0
    public float Y;                 // Normalized 0.0-1.0
    public float Pressure;          // 0.0-1.0
    public ulong Timestamp;
}

public class IOSDeviceConnection : IDisposable
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public bool IsPaired { get; set; }
    public bool IsStreaming { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastHeartbeat { get; set; }

    public void Dispose()
    {
        // Close actual connection resources here
        IsConnected = false;
        IsStreaming = false;
    }
}

public class IOSMessage
{
    public IOSMessageType MessageType { get; set; }
    public byte[]? Data { get; set; }
    public DateTime Timestamp { get; set; }
}

public class IOSDeviceEventArgs : EventArgs
{
    public ExternalDevice? Device { get; set; }
    public IOSDeviceInfo? DeviceInfo { get; set; }
}

public class IOSTouchEventArgs : EventArgs
{
    public string DeviceId { get; set; } = string.Empty;
    public uint TouchId { get; set; }
    public uint TouchType { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Pressure { get; set; }
    public DateTime Timestamp { get; set; }
}

#endregion