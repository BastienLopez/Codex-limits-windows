using System.Text.Json;

namespace CodexLimits.Core;

public sealed class UsageHistoryStore
{
    private readonly string _directory;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public UsageHistoryStore(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexLimitsWindows",
            "History");
    }

    public async Task<IReadOnlyList<UsageSample>> LoadAsync(int retentionDays = 90, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_directory);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);
        var samples = new List<UsageSample>();

        foreach (var file in Directory.EnumerateFiles(_directory, "*.jsonl").OrderBy(path => path, StringComparer.Ordinal))
        {
            var lines = await File.ReadAllLinesAsync(file, cancellationToken);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var sample = JsonSerializer.Deserialize<UsageSample>(line, _jsonOptions);
                    if (sample is not null && sample.ObservedAt >= cutoff)
                    {
                        samples.Add(sample);
                    }
                }
                catch (JsonException)
                {
                    // Keep the rest of the local history usable if one line is damaged.
                }
            }
        }

        return samples
            .OrderBy(sample => sample.ObservedAt)
            .ToArray();
    }

    public async Task RecordAsync(UsageSample sample, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, $"{sample.ObservedAt.UtcDateTime:yyyy-MM-dd}.jsonl");
        var json = JsonSerializer.Serialize(sample, _jsonOptions);
        await File.AppendAllTextAsync(file, json + Environment.NewLine, cancellationToken);
    }
}
