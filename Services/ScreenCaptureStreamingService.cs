using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WinForms = System.Windows.Forms;
using iMonitor.Models;

namespace iMonitor.Services;

/// <summary>
/// High-performance screen capture and streaming service for virtual displays.
/// Captures screen content and streams it to connected iOS devices with low latency.
/// </summary>
public class ScreenCaptureStreamingService : IDisposable
{
    #region Windows API for Screen Capture

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hGdiObj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
        IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private const uint SRCCOPY = 0x00CC0020;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    #endregion

    #region DirectX/DXGI for Hardware Acceleration

    // Note: In a production environment, this would use DirectX/DXGI for hardware acceleration
    // For demonstration, we use GDI+ with plans to upgrade to DXGI

    #endregion

    private readonly Dictionary<string, Models.StreamingSession> _streamingSessions = new();
    private readonly Dictionary<string, VirtualMonitorInfo> _virtualMonitors = new();
    private readonly IOSDeviceCommunicationService _iosCommService;
    private readonly System.Threading.Timer _captureTimer;
    private readonly object _lockObject = new object();
    private bool _isCapturing = false;
    private bool _disposed = false;

    // Streaming configuration
    private const int DEFAULT_TARGET_FPS = 60;
    private const int DEFAULT_QUALITY = 85;
    private const int MAX_CONCURRENT_STREAMS = 8;

    public event EventHandler<StreamingSessionEventArgs>? StreamingStarted;
    public event EventHandler<StreamingSessionEventArgs>? StreamingStopped;
    public event EventHandler<StreamingErrorEventArgs>? StreamingError;
    public event EventHandler<PerformanceMetrics>? PerformanceUpdate;

    public ScreenCaptureStreamingService(IOSDeviceCommunicationService iosCommService)
    {
        _iosCommService = iosCommService ?? throw new ArgumentNullException(nameof(iosCommService));
        
        // Initialize capture timer (will be started when needed)
        _captureTimer = new System.Threading.Timer(CaptureAndStreamFrames, null, Timeout.Infinite, Timeout.Infinite);
    }

    public async Task<bool> StartStreamingAsync(VirtualMonitor virtualMonitor, ExternalDevice targetDevice)
    {
        try
        {
            var sessionId = $"{virtualMonitor.Id}_{targetDevice.DeviceId}";
            
            lock (_lockObject)
            {
                if (_streamingSessions.ContainsKey(sessionId))
                {
                    StreamingError?.Invoke(this, new StreamingErrorEventArgs
                    {
                        SessionId = sessionId,
                        Error = "Streaming session already exists"
                    });
                    return false;
                }

                if (_streamingSessions.Count >= MAX_CONCURRENT_STREAMS)
                {
                    StreamingError?.Invoke(this, new StreamingErrorEventArgs
                    {
                        SessionId = sessionId,
                        Error = $"Maximum concurrent streams ({MAX_CONCURRENT_STREAMS}) reached"
                    });
                    return false;
                }
            }

            Debug.WriteLine($"Starting streaming session: {sessionId}");

            // Get virtual monitor information
            var monitorInfo = GetVirtualMonitorInfo(virtualMonitor);
            if (monitorInfo == null)
            {
                StreamingError?.Invoke(this, new StreamingErrorEventArgs
                {
                    SessionId = sessionId,
                    Error = "Virtual monitor not found or invalid"
                });
                return false;
            }

            // Start display stream on iOS device
            if (!await _iosCommService.StartDisplayStreamAsync(targetDevice.DeviceId, virtualMonitor.Width, virtualMonitor.Height))
            {
                StreamingError?.Invoke(this, new StreamingErrorEventArgs
                {
                    SessionId = sessionId,
                    Error = "Failed to start display stream on iOS device"
                });
                return false;
            }

            // Create streaming session
            var session = new Models.StreamingSession
            {
                SessionId = sessionId,
                VirtualMonitor = virtualMonitor,
                TargetDevice = targetDevice,
                MonitorInfo = monitorInfo,
                IsActive = true,
                StartTime = DateTime.Now,
                TargetFps = DEFAULT_TARGET_FPS,
                Quality = DEFAULT_QUALITY,
                FramesSent = 0,
                BytesSent = 0,
                LastFrameTime = DateTime.Now
            };

            lock (_lockObject)
            {
                _streamingSessions[sessionId] = session;
                _virtualMonitors[virtualMonitor.Id] = monitorInfo;
            }

            // Start capture timer if this is the first session
            if (_streamingSessions.Count == 1)
            {
                StartCapture();
            }

            StreamingStarted?.Invoke(this, new StreamingSessionEventArgs { Session = session });
            
            Debug.WriteLine($"Streaming session started successfully: {sessionId}");
            return true;
        }
        catch (Exception ex)
        {
            StreamingError?.Invoke(this, new StreamingErrorEventArgs
            {
                SessionId = $"{virtualMonitor.Id}_{targetDevice.DeviceId}",
                Error = $"Failed to start streaming: {ex.Message}"
            });
            return false;
        }
    }

    public async Task<bool> StopStreamingAsync(string sessionId)
    {
        try
        {
            StreamingSession? session;
            
            lock (_lockObject)
            {
                if (!_streamingSessions.TryGetValue(sessionId, out session))
                {
                    return false;
                }

                _streamingSessions.Remove(sessionId);
            }

            session.IsActive = false;
            
            // Stop display stream on iOS device
            await _iosCommService.StopDisplayStreamAsync(session.TargetDevice.DeviceId);
            
            // Stop capture timer if no more sessions
            if (_streamingSessions.Count == 0)
            {
                StopCapture();
            }

            StreamingStopped?.Invoke(this, new StreamingSessionEventArgs { Session = session });
            
            Debug.WriteLine($"Streaming session stopped: {sessionId}");
            return true;
        }
        catch (Exception ex)
        {
            StreamingError?.Invoke(this, new StreamingErrorEventArgs
            {
                SessionId = sessionId,
                Error = $"Failed to stop streaming: {ex.Message}"
            });
            return false;
        }
    }

    public List<Models.StreamingSession> GetActiveStreams()
    {
        lock (_lockObject)
        {
            return _streamingSessions.Values.Where(s => s.IsActive).ToList();
        }
    }

    public StreamingSession? GetStreamingSession(string sessionId)
    {
        lock (_lockObject)
        {
            _streamingSessions.TryGetValue(sessionId, out var session);
            return session;
        }
    }

    public async Task<bool> UpdateStreamQualityAsync(string sessionId, int quality, int targetFps)
    {
        try
        {
            lock (_lockObject)
            {
                if (_streamingSessions.TryGetValue(sessionId, out var session))
                {
                    session.Quality = Math.Clamp(quality, 10, 100);
                    session.TargetFps = Math.Clamp(targetFps, 15, 120);
                    
                    Debug.WriteLine($"Updated stream quality for {sessionId}: Quality={session.Quality}, FPS={session.TargetFps}");
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to update stream quality: {ex.Message}");
            return false;
        }
    }

    #region Private Methods

    private void StartCapture()
    {
        if (!_isCapturing)
        {
            _isCapturing = true;
            
            // Calculate capture interval based on target FPS
            var maxFps = _streamingSessions.Values.Max(s => s.TargetFps);
            var captureInterval = 1000 / Math.Max(maxFps, DEFAULT_TARGET_FPS);
            
            _captureTimer.Change(0, captureInterval);
            Debug.WriteLine($"Screen capture started with {captureInterval}ms interval ({maxFps} FPS)");
        }
    }

    private void StopCapture()
    {
        if (_isCapturing)
        {
            _isCapturing = false;
            _captureTimer.Change(Timeout.Infinite, Timeout.Infinite);
            Debug.WriteLine("Screen capture stopped");
        }
    }

    private VirtualMonitorInfo? GetVirtualMonitorInfo(VirtualMonitor virtualMonitor)
    {
        try
        {
            // Get monitor bounds from Windows
            var screen = WinForms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == virtualMonitor.WindowsDeviceName);
            if (screen == null)
            {
                // If not found in screens, create virtual bounds
                return new VirtualMonitorInfo
                {
                    MonitorId = virtualMonitor.Id,
                    DeviceName = virtualMonitor.WindowsDeviceName,
                    Bounds = new Rectangle(0, 0, virtualMonitor.Width, virtualMonitor.Height),
                    WorkingArea = new Rectangle(0, 0, virtualMonitor.Width, virtualMonitor.Height),
                    IsPrimary = false,
                    BitsPerPixel = 32
                };
            }

            return new VirtualMonitorInfo
            {
                MonitorId = virtualMonitor.Id,
                DeviceName = screen.DeviceName,
                Bounds = screen.Bounds,
                WorkingArea = screen.WorkingArea,
                IsPrimary = screen.Primary,
                BitsPerPixel = 32
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get virtual monitor info: {ex.Message}");
            return null;
        }
    }

    private void CaptureAndStreamFrames(object? state)
    {
        if (!_isCapturing) return;

        try
        {
            var activeStreams = GetActiveStreams();
            if (activeStreams.Count == 0) return;

            var captureStart = DateTime.Now;
            var framesCaptured = 0;
            var totalBytesStreamed = 0L;

            // Group sessions by monitor to avoid duplicate captures
            var sessionsByMonitor = activeStreams.GroupBy(s => s.VirtualMonitor.Id);

            foreach (var monitorGroup in sessionsByMonitor)
            {
                var monitorSessions = monitorGroup.ToList();
                var firstSession = monitorSessions.First();
                var monitorInfo = firstSession.MonitorInfo;

                // Check if it's time to capture for any session in this monitor group
                var shouldCapture = monitorSessions.Any(session =>
                {
                    var frameInterval = 1000.0 / session.TargetFps;
                    var timeSinceLastFrame = (DateTime.Now - session.LastFrameTime).TotalMilliseconds;
                    return timeSinceLastFrame >= frameInterval;
                });

                if (!shouldCapture) continue;

                // Capture the screen for this virtual monitor
                var frameData = CaptureMonitorFrame(monitorInfo);
                if (frameData != null)
                {
                    framesCaptured++;

                    // Stream to all devices for this monitor
                    foreach (var session in monitorSessions)
                    {
                        var frameInterval = 1000.0 / session.TargetFps;
                        var timeSinceLastFrame = (DateTime.Now - session.LastFrameTime).TotalMilliseconds;
                        
                        if (timeSinceLastFrame >= frameInterval)
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    // Compress frame based on session quality
                                    var compressedFrame = CompressFrame(frameData, session.Quality);
                                    
                                    // Send frame to iOS device
                                    if (await _iosCommService.SendDisplayFrameAsync(
                                        session.TargetDevice.DeviceId, 
                                        compressedFrame, 
                                        monitorInfo.Bounds.Width, 
                                        monitorInfo.Bounds.Height))
                                    {
                                        session.FramesSent++;
                                        session.BytesSent += compressedFrame.Length;
                                        session.LastFrameTime = DateTime.Now;
                                        Interlocked.Add(ref totalBytesStreamed, compressedFrame.Length);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Failed to stream frame for session {session.SessionId}: {ex.Message}");
                                }
                            });
                        }
                    }
                }
            }

            // Update performance metrics
            var captureTime = (DateTime.Now - captureStart).TotalMilliseconds;
            
            PerformanceUpdate?.Invoke(this, new PerformanceMetrics
            {
                CaptureTime = captureTime,
                FramesCaptured = framesCaptured,
                ActiveSessions = activeStreams.Count,
                BytesPerSecond = totalBytesStreamed,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in capture and stream cycle: {ex.Message}");
        }
    }

    private byte[]? CaptureMonitorFrame(VirtualMonitorInfo monitorInfo)
    {
        IntPtr hDesk = IntPtr.Zero;
        IntPtr hSrce = IntPtr.Zero;
        IntPtr hDest = IntPtr.Zero;
        IntPtr hBmp = IntPtr.Zero;
        IntPtr hOldBmp = IntPtr.Zero;
        
        try
        {
            // Get desktop device context
            hDesk = GetDC(IntPtr.Zero);
            hSrce = CreateCompatibleDC(hDesk);
            hDest = CreateCompatibleDC(hDesk);
            
            // Create bitmap
            hBmp = CreateCompatibleBitmap(hDesk, monitorInfo.Bounds.Width, monitorInfo.Bounds.Height);
            hOldBmp = SelectObject(hDest, hBmp);
            
            // Copy screen content
            var success = BitBlt(hDest, 0, 0, monitorInfo.Bounds.Width, monitorInfo.Bounds.Height,
                                hSrce, monitorInfo.Bounds.X, monitorInfo.Bounds.Y, SRCCOPY);
            
            if (!success) return null;

            // Convert to managed bitmap
            using var bitmap = Image.FromHbitmap(hBmp);
            using var ms = new MemoryStream();
            
            bitmap.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to capture monitor frame: {ex.Message}");
            return null;
        }
        finally
        {
            // Clean up GDI resources
            if (hOldBmp != IntPtr.Zero) SelectObject(hDest, hOldBmp);
            if (hBmp != IntPtr.Zero) DeleteObject(hBmp);
            if (hDest != IntPtr.Zero) DeleteDC(hDest);
            if (hSrce != IntPtr.Zero) DeleteDC(hSrce);
            if (hDesk != IntPtr.Zero) ReleaseDC(IntPtr.Zero, hDesk);
        }
    }

    private byte[] CompressFrame(byte[] frameData, int quality)
    {
        try
        {
            // Load image and compress with specified quality
            using var originalStream = new MemoryStream(frameData);
            using var originalImage = Image.FromStream(originalStream);
            using var compressedStream = new MemoryStream();
            
            // Set up JPEG encoder with quality parameter
            var jpegEncoder = GetEncoder(ImageFormat.Jpeg);
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
            
            originalImage.Save(compressedStream, jpegEncoder, encoderParams);
            
            return compressedStream.ToArray();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to compress frame: {ex.Message}");
            return frameData; // Return original if compression fails
        }
    }

    private ImageCodecInfo GetEncoder(ImageFormat format)
    {
        var codecs = ImageCodecInfo.GetImageEncoders();
        return codecs.First(codec => codec.FormatID == format.Guid);
    }

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            StopCapture();
            _captureTimer?.Dispose();
            
            // Stop all active streaming sessions
            var activeSessions = GetActiveStreams();
            foreach (var session in activeSessions)
            {
                _ = StopStreamingAsync(session.SessionId);
            }

            lock (_lockObject)
            {
                _streamingSessions.Clear();
                _virtualMonitors.Clear();
            }

            _disposed = true;
        }
    }
}