using System.Text.RegularExpressions;

namespace CodexLimits.Core;

public readonly record struct CodexSemanticVersion(
    int Major,
    int Minor,
    int Patch,
    string? Prerelease = null) : IComparable<CodexSemanticVersion>
{
    public int CompareTo(CodexSemanticVersion other)
    {
        var comparison = Major.CompareTo(other.Major);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = Minor.CompareTo(other.Minor);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = Patch.CompareTo(other.Patch);
        if (comparison != 0)
        {
            return comparison;
        }

        if (string.IsNullOrWhiteSpace(Prerelease) && string.IsNullOrWhiteSpace(other.Prerelease))
        {
            return 0;
        }

        if (string.IsNullOrWhiteSpace(Prerelease))
        {
            return 1;
        }

        if (string.IsNullOrWhiteSpace(other.Prerelease))
        {
            return -1;
        }

        return ComparePrerelease(Prerelease!, other.Prerelease!);
    }

    public override string ToString() =>
        string.IsNullOrWhiteSpace(Prerelease)
            ? $"{Major}.{Minor}.{Patch}"
            : $"{Major}.{Minor}.{Patch}-{Prerelease}";

    private static int ComparePrerelease(string left, string right)
    {
        var leftParts = left.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var rightParts = right.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var count = Math.Max(leftParts.Length, rightParts.Length);

        for (var index = 0; index < count; index++)
        {
            if (index >= leftParts.Length)
            {
                return -1;
            }

            if (index >= rightParts.Length)
            {
                return 1;
            }

            var leftNumeric = int.TryParse(leftParts[index], out var leftNumber);
            var rightNumeric = int.TryParse(rightParts[index], out var rightNumber);

            int comparison;
            if (leftNumeric && rightNumeric)
            {
                comparison = leftNumber.CompareTo(rightNumber);
            }
            else if (leftNumeric)
            {
                comparison = -1;
            }
            else if (rightNumeric)
            {
                comparison = 1;
            }
            else
            {
                comparison = string.Compare(leftParts[index], rightParts[index], StringComparison.OrdinalIgnoreCase);
            }

            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }
}

public static partial class CodexVersionPolicy
{
    public const string MinimumSupportedVersionText = "0.120.0";

    public static CodexSemanticVersion MinimumSupportedVersion { get; } = new(0, 120, 0);

    public static bool IsSupported(CodexSemanticVersion version) =>
        version.CompareTo(MinimumSupportedVersion) >= 0;

    public static bool TryParse(string? output, out CodexSemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(output))
        {
            return false;
        }

        var matches = VersionRegex().Matches(output);
        if (matches.Count == 0)
        {
            return false;
        }

        var match = matches[matches.Count - 1];
        if (!int.TryParse(match.Groups[1].Value, out var major) ||
            !int.TryParse(match.Groups[2].Value, out var minor) ||
            !int.TryParse(match.Groups[3].Value, out var patch))
        {
            return false;
        }

        var prerelease = match.Groups[4].Success ? match.Groups[4].Value : null;
        version = new CodexSemanticVersion(major, minor, patch, prerelease);
        return true;
    }

    [GeneratedRegex(@"(?<!\d)(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.-]+))?", RegexOptions.CultureInvariant)]
    private static partial Regex VersionRegex();
}
