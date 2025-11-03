using System.IO;
using System.Text.Json;

namespace iMonitor.Services;

public class ConfigurationService
{
    private readonly string _configPath;
    private AppSettings _settings;

    public ConfigurationService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "iMonitor");
        Directory.CreateDirectory(appFolder);
        
        _configPath = Path.Combine(appFolder, "settings.json");
        _settings = LoadSettings();
    }

    public AppSettings Settings => _settings;

    public void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
        }
    }

    private AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
        }

        return new AppSettings();
    }

    public void UpdateSetting<T>(string key, T value)
    {
        var property = typeof(AppSettings).GetProperty(key);
        property?.SetValue(_settings, value);
        SaveSettings();
    }
}

public class AppSettings
{
    public bool StartWithWindows { get; set; } = false;
    public bool ShowNotifications { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool AutoConnectKnownDevices { get; set; } = false;
    public int RefreshInterval { get; set; } = 30; // seconds
    public List<string> KnownDeviceIds { get; set; } = new();
    public Dictionary<string, DevicePreferences> DevicePreferences { get; set; } = new();
}

public class DevicePreferences
{
    public bool AutoConnect { get; set; } = false;
    public string PreferredResolution { get; set; } = "1920x1080";
    public string DisplayPosition { get; set; } = "Right"; // Left, Right, Above, Below
    public bool RememberSettings { get; set; } = true;
}