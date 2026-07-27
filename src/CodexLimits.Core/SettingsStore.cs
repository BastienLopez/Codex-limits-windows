using System.Text.Json;

namespace CodexLimits.Core;

public sealed class SettingsStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public SettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexLimitsWindows",
            "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new AppSettings();
            var json = File.ReadAllText(_path);
            return (JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings()).Normalize();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("Chemin de paramètres invalide.");
        Directory.CreateDirectory(directory);
        var temporaryPath = _path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings.Normalize(), _jsonOptions));
        File.Move(temporaryPath, _path, overwrite: true);
    }
}
