namespace CodexLimits.Core;

public interface IUsageProvider
{
    Task<UsageSnapshot> FetchAsync(CancellationToken cancellationToken = default);
}
