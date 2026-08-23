using CBP.Caching.Interfaces;
using CBP.Caching.Models;
using Microsoft.Extensions.Logging;

namespace PassPlat.Aplicacion.Services;

public class MfaCodeEntry
{
    public string Code { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}

public interface IMfaCodeStore
{
    Task StoreAsync(int idUsuario, int idTenant, string code, TimeSpan expiry, CancellationToken ct = default);
    Task<bool> ValidateAndConsumeAsync(int idUsuario, int idTenant, string code, CancellationToken ct = default);
}

public class MfaCodeStore : IMfaCodeStore
{
    private const int OverwriteWarnThresholdSeconds = 30;
    private const string KeyPrefix = "mfa:";

    private readonly ICacheService _cache;
    private readonly ILogger<MfaCodeStore> _logger;

    public MfaCodeStore(ICacheService cache, ILogger<MfaCodeStore> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task StoreAsync(int idUsuario, int idTenant, string code, TimeSpan expiry, CancellationToken ct = default)
    {
        var key = BuildKey(idUsuario, idTenant);

        var existing = await _cache.GetAsync<MfaCodeEntry>(key, ct);
        if (existing is not null)
        {
            var remaining = existing.ExpiresAt - DateTime.UtcNow;
            if (remaining > TimeSpan.FromSeconds(OverwriteWarnThresholdSeconds))
            {
                _logger.LogWarning(
                    "MFA code overwrite for Usuario={IdUsuario}, Tenant={IdTenant}. Existing code still valid for {RemainingSeconds}s",
                    idUsuario, idTenant, (int)remaining.TotalSeconds);
            }
        }

        await _cache.SetAsync(key, new MfaCodeEntry { Code = code, ExpiresAt = DateTime.UtcNow.Add(expiry) }, new CacheEntryOptions(expiry), ct);
    }

    public async Task<bool> ValidateAndConsumeAsync(int idUsuario, int idTenant, string code, CancellationToken ct = default)
    {
        var key = BuildKey(idUsuario, idTenant);

        var entry = await _cache.GetAsync<MfaCodeEntry>(key, ct);
        if (entry is null)
            return false;

        await _cache.RemoveAsync(key, ct);

        if (entry.ExpiresAt < DateTime.UtcNow)
            return false;

        return entry.Code == code;
    }

    private static string BuildKey(int idUsuario, int idTenant) => $"{KeyPrefix}{idUsuario}:{idTenant}";
}
