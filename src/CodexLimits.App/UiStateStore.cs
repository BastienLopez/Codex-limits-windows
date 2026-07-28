using System.IO;
using System.Text.Json;

namespace CodexLimits.App;

internal sealed record UiState
{
    public bool BackgroundNoticeShown { get; init; }
}

internal sealed class UiStateStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public UiStateStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexLimitsWindows",
            "ui-state.json");
    }

    public UiState Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new UiState();
            }

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<UiState>(json, _jsonOptions) ?? new UiState();
        }
        catch (IOException)
        {
            return new UiState();
        }
        catch (UnauthorizedAccessException)
        {
            return new UiState();
        }
        catch (JsonException)
        {
            return new UiState();
        }
    }

    public void TrySave(UiState state)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);
            var temporaryPath = _path + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state, _jsonOptions));
            File.Move(temporaryPath, _path, overwrite: true);
        }
        catch (IOException)
        {
            // The notice is informational. A storage failure must never stop the app.
        }
        catch (UnauthorizedAccessException)
        {
            // The notice is informational. A storage failure must never stop the app.
        }
    }
}
