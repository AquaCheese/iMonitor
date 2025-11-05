using System;

namespace iMonitor.Models;

/// <summary>
/// Enhanced VirtualMonitor model with additional properties for IddCx support
/// </summary>
public class VirtualMonitor
{
    public string Id { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string WindowsDeviceName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public int RefreshRate { get; set; } = 60;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Status { get; set; } = "Inactive";
}