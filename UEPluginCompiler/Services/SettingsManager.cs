using System.Text.Json;

namespace UEPluginCompiler.Services;

/// <summary>
/// Persists user settings and custom engine paths to exe directory.
/// </summary>
public class SettingsManager
{
    private readonly string _dataDir;
    private readonly string _settingsPath;
    private readonly string _customEnginesPath;

    public SettingsManager()
    {
        _dataDir = Path.Combine(AppContext.BaseDirectory, "Saved");
        _settingsPath = Path.Combine(_dataDir, "settings.json");
        _customEnginesPath = Path.Combine(_dataDir, "custom_engines.json");
        Directory.CreateDirectory(_dataDir);
    }

    // ─── App settings ─────────────────────────────────────────

    public AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
        }

        return new AppSettings();
    }

    public void SaveSettings(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    // ─── Custom engines ───────────────────────────────────────

    public List<CustomEngineEntry> LoadCustomEngines()
    {
        try
        {
            if (File.Exists(_customEnginesPath))
            {
                var json = File.ReadAllText(_customEnginesPath);
                return JsonSerializer.Deserialize<List<CustomEngineEntry>>(json) ?? [];
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load custom engines: {ex.Message}");
        }

        return [];
    }

    public void SaveCustomEngines(List<CustomEngineEntry> engines)
    {
        try
        {
            var json = JsonSerializer.Serialize(engines, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_customEnginesPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save custom engines: {ex.Message}");
        }
    }
}

public class AppSettings
{
    /// <summary>Recently opened .uflow file paths.</summary>
    public List<string> RecentFlows { get; set; } = [];
}

public class CustomEngineEntry
{
    public string DisplayName { get; set; } = "";
    public string RootPath { get; set; } = "";
}
