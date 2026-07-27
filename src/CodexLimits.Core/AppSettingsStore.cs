using System.Text.Json;

namespace CodexLimits.Core;

public sealed class AppSettingsStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public AppSettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexLimitsWindows",
            "settings.json");
    }

    public async Task<WorkScheduleSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return WorkScheduleSettings.Default;
        }

        try
        {
            await using var stream = File.OpenRead(_path);
            var settings = await JsonSerializer.DeserializeAsync<WorkScheduleSettings>(
                stream,
                _jsonOptions,
                cancellationToken);
            return settings is { IsValid: true } ? settings : WorkScheduleSettings.Default;
        }
        catch (JsonException)
        {
            return WorkScheduleSettings.Default;
        }
        catch (IOException)
        {
            return WorkScheduleSettings.Default;
        }
    }

    public async Task SaveAsync(WorkScheduleSettings settings, CancellationToken cancellationToken = default)
    {
        if (!settings.IsValid)
        {
            throw new ArgumentException("Les horaires de travail sont invalides.", nameof(settings));
        }

        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("Le dossier de configuration est introuvable.");
        Directory.CreateDirectory(directory);

        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions, cancellationToken);
    }
}
