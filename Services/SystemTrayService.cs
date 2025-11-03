using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace iMonitor.Services;

public class SystemTrayService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private bool _disposed = false;

    public event Action? ShowMainWindow;
    public event Action? ExitApplication;

    public void Initialize()
    {
        try
        {
            // Create context menu
            _contextMenu = new ContextMenuStrip();
            
            var showMenuItem = new ToolStripMenuItem("Show iMonitor", null, (s, e) => ShowMainWindow?.Invoke());
            var separatorMenuItem = new ToolStripSeparator();
            var exitMenuItem = new ToolStripMenuItem("Exit", null, (s, e) => ExitApplication?.Invoke());

            _contextMenu.Items.AddRange(new ToolStripItem[] { showMenuItem, separatorMenuItem, exitMenuItem });

            // Create notify icon
            _notifyIcon = new NotifyIcon
            {
                Icon = LoadIcon(),
                Text = "iMonitor - External Device Monitor",
                Visible = true,
                ContextMenuStrip = _contextMenu
            };

            _notifyIcon.DoubleClick += (s, e) => ShowMainWindow?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing system tray: {ex.Message}");
        }
    }

    private Icon LoadIcon()
    {
        try
        {
            // Try to load from resources
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "iMonitor.Resources.logo.jpg";
            
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var bitmap = new Bitmap(stream);
                return Icon.FromHandle(bitmap.GetHicon());
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading icon from resources: {ex.Message}");
        }

        try
        {
            // Fallback to file system
            var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo.jpg");
            if (File.Exists(logoPath))
            {
                using var bitmap = new Bitmap(logoPath);
                return Icon.FromHandle(bitmap.GetHicon());
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading icon from file: {ex.Message}");
        }

        // Default system icon
        return SystemIcons.Application;
    }

    public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        try
        {
            _notifyIcon?.ShowBalloonTip(3000, title, message, icon);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error showing notification: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                _notifyIcon?.Dispose();
                _contextMenu?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing system tray service: {ex.Message}");
            }
            _disposed = true;
        }
    }
}