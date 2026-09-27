using System.IO;
using System.Text.Json;
using MacFanControl.Core.Models;

namespace MacFanControl.Core.Services;

public class SettingsService
{
    private static readonly Lazy<SettingsService> _lazy = new(() => new SettingsService());
    public static SettingsService Instance => _lazy.Value;

    private readonly string _settingsFilePath;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public SettingsService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "MacFanControlBootCamp");
        _settingsFilePath = Path.Combine(folder, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
                if (loaded != null)
                {
                    DiagnosticLogger.Instance.Info($"Settings loaded successfully from {_settingsFilePath}");
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Instance.Warn($"Failed to load settings file: {ex.Message}. Using default settings.");
        }

        var defaultSettings = new AppSettings();
        Save(defaultSettings);
        return defaultSettings;
    }

    public bool Save(AppSettings settings)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(settings, _jsonOptions);
            File.WriteAllText(_settingsFilePath, json);
            DiagnosticLogger.Instance.Info("Settings saved successfully.");
            return true;
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Instance.Error($"Failed to save settings: {ex.Message}");
            return false;
        }
    }
}
