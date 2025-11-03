using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using iMonitor.Models;

namespace iMonitor.Services;

public class DisplayStreamingService : IDisposable
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
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hGDIObj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
        IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private const uint SRCCOPY = 0x00CC0020;

    #endregion

    private readonly Dictionary<string, StreamingSession> _activeStreams = new();
    private readonly System.Threading.Timer _captureTimer;
    private bool _disposed = false;

    public DisplayStreamingService()
    {
        // Initialize capture timer for 30 FPS
        _captureTimer = new System.Threading.Timer(CaptureAndStreamDisplays, null, Timeout.Infinite, 33);
    }

    public event EventHandler<string>? StreamingStarted;
    public event EventHandler<string>? StreamingStopped;
    public event EventHandler<StreamingError>? StreamingError;

    public async Task<bool> StartStreamingAsync(VirtualMonitor virtualMonitor, ExternalDevice device)
    {
        try
        {
            if (_activeStreams.ContainsKey(virtualMonitor.Id))
            {
                return true; // Already streaming
            }

            var session = new StreamingSession
            {
                VirtualMonitor = virtualMonitor,
                Device = device,
                IsActive = false,
                StartTime = DateTime.Now
            };

            // Try to establish connection to device
            if (await EstablishConnectionAsync(session))
            {
                _activeStreams[virtualMonitor.Id] = session;
                
                // Start capture timer if this is the first stream
                if (_activeStreams.Count == 1)
                {
                    _captureTimer.Change(0, 33); // Start immediately, then every 33ms (30 FPS)
                }

                session.IsActive = true;
                StreamingStarted?.Invoke(this, virtualMonitor.Id);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            StreamingError?.Invoke(this, new StreamingError
            {
                VirtualMonitorId = virtualMonitor.Id,
                Message = ex.Message,
                Exception = ex
            });
            return false;
        }
    }

    public async Task<bool> StopStreamingAsync(string virtualMonitorId)
    {
        try
        {
            if (!_activeStreams.TryGetValue(virtualMonitorId, out var session))
            {
                return true; // Already stopped
            }

            session.IsActive = false;
            await DisconnectFromDeviceAsync(session);
            
            _activeStreams.Remove(virtualMonitorId);

            // Stop capture timer if no more streams
            if (_activeStreams.Count == 0)
            {
                _captureTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }

            StreamingStopped?.Invoke(this, virtualMonitorId);
            return true;
        }
        catch (Exception ex)
        {
            StreamingError?.Invoke(this, new StreamingError
            {
                VirtualMonitorId = virtualMonitorId,
                Message = ex.Message,
                Exception = ex
            });
            return false;
        }
    }

    private async Task<bool> EstablishConnectionAsync(StreamingSession session)
    {
        try
        {
            // For demonstration, we'll create a simple HTTP server that the device can connect to
            // In a real implementation, this would depend on the device type and capabilities
            
            switch (session.Device.Type)
            {
                case DeviceType.Phone:
                case DeviceType.Tablet:
                    return await SetupMobileDeviceConnectionAsync(session);
                
                case DeviceType.Monitor:
                    return await SetupExternalMonitorConnectionAsync(session);
                
                default:
                    // Generic connection attempt
                    return await SetupGenericConnectionAsync(session);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error establishing connection: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> SetupMobileDeviceConnectionAsync(StreamingSession session)
    {
        try
        {
            // Create HTTP server for mobile device streaming
            var httpListener = new HttpListener();
            var port = GetAvailablePort();
            httpListener.Prefixes.Add($"http://+:{port}/");
            
            httpListener.Start();
            session.HttpListener = httpListener;
            session.StreamingPort = port;

            // Start handling HTTP requests in background
            _ = Task.Run(() => HandleHttpStreamingAsync(session));

            // Show connection instructions to user
            var localIP = GetLocalIPAddress();
            System.Windows.MessageBox.Show(
                $"Virtual monitor created for {session.Device.Name}!\n\n" +
                $"To connect your device:\n" +
                $"1. Connect your {session.Device.Type.ToString().ToLower()} to the same Wi-Fi network\n" +
                $"2. Open a web browser on your device\n" +
                $"3. Navigate to: http://{localIP}:{port}\n" +
                $"4. The virtual monitor content will stream to your device\n\n" +
                $"The virtual monitor '{session.VirtualMonitor.DeviceName}' is now available in Windows Display Settings.",
                "Connection Instructions",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);

            await Task.Delay(1000); // Allow server to start
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error setting up mobile device connection: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> SetupExternalMonitorConnectionAsync(StreamingSession session)
    {
        // For external monitors, we assume direct connection capabilities
        await Task.Delay(100);
        return true;
    }

    private async Task<bool> SetupGenericConnectionAsync(StreamingSession session)
    {
        // Generic connection setup
        await Task.Delay(100);
        return true;
    }

    private async Task HandleHttpStreamingAsync(StreamingSession session)
    {
        try
        {
            while (session.IsActive && session.HttpListener?.IsListening == true)
            {
                var context = await session.HttpListener.GetContextAsync();
                var request = context.Request;
                var response = context.Response;

                if (request.Url?.AbsolutePath == "/")
                {
                    // Serve the streaming HTML page
                    var html = GenerateStreamingHTML(session);
                    var buffer = System.Text.Encoding.UTF8.GetBytes(html);
                    
                    response.ContentType = "text/html";
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    response.OutputStream.Close();
                }
                else if (request.Url?.AbsolutePath == "/stream")
                {
                    // Serve the video stream
                    response.ContentType = "multipart/x-mixed-replace; boundary=frame";
                    response.StatusCode = 200;

                    await StreamFramesToHttpResponseAsync(session, response);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in HTTP streaming: {ex.Message}");
        }
    }

    private string GenerateStreamingHTML(StreamingSession session)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <title>iMonitor - {session.Device.Name}</title>
    <style>
        body {{ margin: 0; padding: 0; background: black; overflow: hidden; }}
        img {{ width: 100vw; height: 100vh; object-fit: contain; }}
    </style>
</head>
<body>
    <img src=""/stream"" alt=""iMonitor Stream"" />
    <script>
        // Auto-refresh if connection is lost
        document.querySelector('img').onerror = function() {{
            setTimeout(() => location.reload(), 2000);
        }};
    </script>
</body>
</html>";
    }

    private async Task StreamFramesToHttpResponseAsync(StreamingSession session, HttpListenerResponse response)
    {
        try
        {
            var stream = response.OutputStream;
            
            while (session.IsActive)
            {
                var frameData = CaptureVirtualMonitorFrame(session.VirtualMonitor);
                if (frameData != null)
                {
                    var boundary = "\r\n--frame\r\n";
                    var header = $"Content-Type: image/jpeg\r\nContent-Length: {frameData.Length}\r\n\r\n";
                    
                    var boundaryBytes = System.Text.Encoding.UTF8.GetBytes(boundary);
                    var headerBytes = System.Text.Encoding.UTF8.GetBytes(header);
                    
                    await stream.WriteAsync(boundaryBytes, 0, boundaryBytes.Length);
                    await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
                    await stream.WriteAsync(frameData, 0, frameData.Length);
                    await stream.FlushAsync();
                }
                
                await Task.Delay(33); // 30 FPS
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error streaming frames: {ex.Message}");
        }
    }

    private void CaptureAndStreamDisplays(object? state)
    {
        try
        {
            foreach (var session in _activeStreams.Values.Where(s => s.IsActive))
            {
                // Frame capture is handled in the HTTP streaming loop
                // This timer could be used for other periodic tasks
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in capture timer: {ex.Message}");
        }
    }

    private byte[]? CaptureVirtualMonitorFrame(VirtualMonitor virtualMonitor)
    {
        try
        {
            // For now, capture the entire primary screen
            // In a real implementation, you would capture only the virtual monitor area
            var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            
            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using var graphics = Graphics.FromImage(bitmap);
            
            graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
            
            // Convert to JPEG
            using var stream = new System.IO.MemoryStream();
            bitmap.Save(stream, ImageFormat.Jpeg);
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error capturing frame: {ex.Message}");
            return null;
        }
    }

    private async Task DisconnectFromDeviceAsync(StreamingSession session)
    {
        try
        {
            session.HttpListener?.Stop();
            session.HttpListener?.Close();
            await Task.Delay(100);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error disconnecting from device: {ex.Message}");
        }
    }

    private int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Any, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ip = host.AddressList.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
            return ip?.ToString() ?? "localhost";
        }
        catch
        {
            return "localhost";
        }
    }

    public List<StreamingSession> GetActiveStreams()
    {
        return _activeStreams.Values.Where(s => s.IsActive).ToList();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _captureTimer?.Dispose();
            
            foreach (var session in _activeStreams.Values)
            {
                _ = DisconnectFromDeviceAsync(session);
            }
            
            _activeStreams.Clear();
            _disposed = true;
        }
    }
}

public class StreamingSession
{
    public VirtualMonitor VirtualMonitor { get; set; } = new();
    public ExternalDevice Device { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime StartTime { get; set; }
    public HttpListener? HttpListener { get; set; }
    public int StreamingPort { get; set; }
}

public class StreamingError
{
    public string VirtualMonitorId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
}