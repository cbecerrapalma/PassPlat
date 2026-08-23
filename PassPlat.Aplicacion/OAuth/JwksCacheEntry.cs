using Microsoft.IdentityModel.Tokens;

namespace PassPlat.Aplicacion.OAuth;

public sealed class JwksCacheEntry
{
    public required string Provider { get; init; }
    public required JsonWebKeySet KeySet { get; init; }
    public string? Kid { get; init; }
    public string? ETag { get; init; }
    public DateTimeOffset? LastModified { get; init; }
    public DateTimeOffset FetchedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset RefreshAfter { get; init; }
    public bool IsStale => DateTimeOffset.UtcNow >= RefreshAfter;
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;

    public ICollection<SecurityKey> Keys => KeySet.GetSigningKeys();
}
