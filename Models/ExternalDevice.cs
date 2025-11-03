using System.ComponentModel;

namespace iMonitor.Models;

public class ExternalDevice : INotifyPropertyChanged
{
    private bool _isConnectedAsMonitor;
    private string _status = "Disconnected";

    public string DeviceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public DeviceType Type { get; set; }
    public bool IsConnected { get; set; }
    public DateTime LastSeen { get; set; }

    public bool IsConnectedAsMonitor
    {
        get => _isConnectedAsMonitor;
        set
        {
            if (_isConnectedAsMonitor != value)
            {
                _isConnectedAsMonitor = value;
                Status = value ? "Connected as Monitor" : "Available";
                OnPropertyChanged(nameof(IsConnectedAsMonitor));
            }
        }
    }

    public string Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }
    }

    public bool CanBeUsedAsMonitor => Type == DeviceType.Tablet || Type == DeviceType.Phone || Type == DeviceType.Monitor;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override bool Equals(object? obj)
    {
        return obj is ExternalDevice device && DeviceId == device.DeviceId;
    }

    public override int GetHashCode()
    {
        return DeviceId.GetHashCode();
    }
}

public enum DeviceType
{
    Unknown,
    Phone,
    Tablet,
    Monitor,
    Computer,
    Storage,
    Other
}