using CBP.Results;
using Microsoft.IdentityModel.Tokens;

namespace PassPlat.Aplicacion.OAuth;

public interface IJwksStore
{
    Task<Result<ICollection<SecurityKey>>> GetSigningKeysAsync(string jwksUri, CancellationToken ct = default);
    Task<Result<SecurityKey?>> GetSigningKeyAsync(string jwksUri, string kid, CancellationToken ct = default);
    Task<Result> RefreshAsync(string jwksUri, bool force = false, CancellationToken ct = default);
    Task<Result> InvalidateAsync(string jwksUri, CancellationToken ct = default);
    Task<Result> WarmupAsync(IEnumerable<string> jwksUris, CancellationToken ct = default);
    Task<JwksStoreStatistics> GetStatisticsAsync(CancellationToken ct = default);
}

public sealed class JwksStoreStatistics
{
    public long Hits { get; init; }
    public long Misses { get; init; }
    public long Refreshes { get; init; }
    public long Errors { get; init; }
    public long StaleFallbacks { get; init; }
    public long KidRotations { get; init; }
    public int CachedProviders { get; init; }
    public DateTimeOffset? OldestEntry { get; init; }
    public DateTimeOffset? NewestEntry { get; init; }
}
