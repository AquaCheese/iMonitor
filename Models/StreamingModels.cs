using System;
using System.Drawing;

namespace iMonitor.Models;

public class StreamingSession
{
    public string SessionId { get; set; } = string.Empty;
    public VirtualMonitor VirtualMonitor { get; set; } = new();
    public ExternalDevice TargetDevice { get; set; } = new();
    public VirtualMonitorInfo MonitorInfo { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime LastFrameTime { get; set; }
    public int TargetFps { get; set; } = 60;
    public int Quality { get; set; } = 85;
    public long FramesSent { get; set; }
    public long BytesSent { get; set; }
}

public class VirtualMonitorInfo
{
    public string MonitorId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public Rectangle Bounds { get; set; }
    public Rectangle WorkingArea { get; set; }
    public bool IsPrimary { get; set; }
    public int BitsPerPixel { get; set; }
}

public class StreamingSessionEventArgs : EventArgs
{
    public StreamingSession Session { get; set; } = new();
}

public class StreamingErrorEventArgs : EventArgs
{
    public string SessionId { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

public class PerformanceMetrics : EventArgs
{
    public double CaptureTime { get; set; }
    public int FramesCaptured { get; set; }
    public int ActiveSessions { get; set; }
    public long BytesPerSecond { get; set; }
    public DateTime Timestamp { get; set; }
}