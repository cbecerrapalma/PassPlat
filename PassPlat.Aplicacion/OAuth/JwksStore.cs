using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using CBP.Caching.Interfaces;
using CBP.Caching.Models;
using CBP.Results;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using PassPlat.Aplicacion.Options;

namespace PassPlat.Aplicacion.OAuth;

public sealed class JwksStore : IJwksStore
{
    private readonly ICacheService _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JwksStore> _logger;
    private readonly OAuthMaintenanceOptions _options;

    private long _hits;
    private long _misses;
    private long _refreshes;
    private long _errors;
    private long _staleFallbacks;
    private long _kidRotations;

    private const string CacheKeyPrefix = "oauth:jwks:";

    public JwksStore(
        ICacheService cache,
        IHttpClientFactory httpClientFactory,
        ILogger<JwksStore> logger,
        Microsoft.Extensions.Options.IOptions<OAuthMaintenanceOptions> options)
    {
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<Result<ICollection<SecurityKey>>> GetSigningKeysAsync(string jwksUri, CancellationToken ct = default)
    {
        try
        {
            var cacheKey = CacheKeyPrefix + jwksUri;
            var entry = await _cache.GetAsync<JwksCacheEntry>(cacheKey, ct);
            if (entry != null && !entry.IsExpired)
            {
                Interlocked.Increment(ref _hits);
                if (entry.IsStale)
                    _ = RefreshAsync(jwksUri, force: false, ct);
                return Result<ICollection<SecurityKey>>.Success(entry.Keys);
            }

            Interlocked.Increment(ref _misses);
            return await FetchAndCacheAsync(jwksUri, ct);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _errors);
            _logger.LogError(ex, "Error al obtener claves JWKS para {JwksUri}", jwksUri);
            return Result<ICollection<SecurityKey>>.Failure("JWKS_ERROR", $"Error al obtener claves JWKS: {ex.Message}");
        }
    }

    public async Task<Result<SecurityKey?>> GetSigningKeyAsync(string jwksUri, string kid, CancellationToken ct = default)
    {
        try
        {
            var cacheKey = CacheKeyPrefix + jwksUri;
            var entry = await _cache.GetAsync<JwksCacheEntry>(cacheKey, ct);

            if (entry != null && !entry.IsExpired)
            {
                var jsonWebKey = entry.KeySet.Keys.FirstOrDefault(k => k.Kid == kid);
                if (jsonWebKey != null)
                {
                    Interlocked.Increment(ref _hits);
                    return Result<SecurityKey?>.Success(jsonWebKey, allowNull: false);
                }

                Interlocked.Increment(ref _kidRotations);
                _logger.LogInformation("Kid {Kid} no encontrado en JWKS {JwksUri}, forzando refresh", kid, jwksUri);
                await FetchAndCacheAsync(jwksUri, ct);

                entry = await _cache.GetAsync<JwksCacheEntry>(CacheKeyPrefix + jwksUri, ct);
                jsonWebKey = entry?.KeySet.Keys.FirstOrDefault(k => k.Kid == kid);
                if (jsonWebKey != null)
                    return Result<SecurityKey?>.Success(jsonWebKey, allowNull: false);

                return Result<SecurityKey?>.Failure("KEY_NOT_FOUND", $"Kid {kid} no encontrado tras refresh de JWKS");
            }

            Interlocked.Increment(ref _misses);
            var fetchResult = await FetchAndCacheAsync(jwksUri, ct);
            if (fetchResult.IsFailure)
                return Result<SecurityKey?>.Failure(fetchResult.Error!);

            entry = await _cache.GetAsync<JwksCacheEntry>(CacheKeyPrefix + jwksUri, ct);
            var foundKey = entry?.KeySet.Keys.FirstOrDefault(k => k.Kid == kid);

            return foundKey != null
                ? Result<SecurityKey?>.Success(foundKey, allowNull: false)
                : Result<SecurityKey?>.Failure("KEY_NOT_FOUND", $"Kid {kid} no encontrado en JWKS");
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _errors);
            _logger.LogError(ex, "Error al obtener signing key {Kid} de {JwksUri}", kid, jwksUri);
            return Result<SecurityKey?>.Failure("JWKS_ERROR", $"Error al obtener signing key: {ex.Message}");
        }
    }

    public async Task<Result> RefreshAsync(string jwksUri, bool force = false, CancellationToken ct = default)
    {
        try
        {
            if (!force)
            {
                var cacheKey = CacheKeyPrefix + jwksUri;
                var entry = await _cache.GetAsync<JwksCacheEntry>(cacheKey, ct);
                if (entry != null && !entry.IsStale)
                    return Result.Success();
            }

            var result = await FetchAndCacheAsync(jwksUri, ct);
            return result.IsSuccess ? Result.Success() : Result.Failure(result.Error!);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _errors);
            _logger.LogError(ex, "Error al refrescar JWKS para {JwksUri}", jwksUri);
            return Result.Failure("JWKS_REFRESH_ERROR", ex.Message);
        }
    }

    public async Task<Result> InvalidateAsync(string jwksUri, CancellationToken ct = default)
    {
        try
        {
            var cacheKey = CacheKeyPrefix + jwksUri;
            await _cache.RemoveAsync(cacheKey, ct);
            _logger.LogInformation("JWKS cache invalidated for {JwksUri}", jwksUri);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al invalidar JWKS para {JwksUri}", jwksUri);
            return Result.Failure("JWKS_INVALIDATE_ERROR", ex.Message);
        }
    }

    public async Task<Result> WarmupAsync(IEnumerable<string> jwksUris, CancellationToken ct = default)
    {
        var uris = jwksUris.Distinct().ToList();
        _logger.LogInformation("Precalentando JWKS para {Count} proveedores", uris.Count);

        var failed = 0;
        foreach (var uri in uris)
        {
            var result = await GetSigningKeysAsync(uri, ct);
            if (result.IsFailure)
            {
                failed++;
                _logger.LogWarning("Warmup falló para {JwksUri}: {Error}", uri, result.Error?.Message);
            }
        }

        return failed == 0
            ? Result.Success()
            : Result.Failure("JWKS_WARMUP_PARTIAL", $"Precalentamiento completado con {failed} fallos de {uris.Count}");
    }

    public Task<JwksStoreStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        var oldest = DateTimeOffset.MaxValue;
        var newest = DateTimeOffset.MinValue;
        var cachedCount = 0;
        var oldestSet = false;

        return Task.FromResult(new JwksStoreStatistics
        {
            Hits = Interlocked.Read(ref _hits),
            Misses = Interlocked.Read(ref _misses),
            Refreshes = Interlocked.Read(ref _refreshes),
            Errors = Interlocked.Read(ref _errors),
            StaleFallbacks = Interlocked.Read(ref _staleFallbacks),
            KidRotations = Interlocked.Read(ref _kidRotations),
            CachedProviders = cachedCount,
            OldestEntry = oldestSet ? oldest : null,
            NewestEntry = oldestSet ? newest : null,
        });
    }

    private async Task<Result<ICollection<SecurityKey>>> FetchAndCacheAsync(string jwksUri, CancellationToken ct)
    {
        try
        {
            Interlocked.Increment(ref _refreshes);

            var client = _httpClientFactory.CreateClient("OAuth.Jwks");
            var response = await client.GetAsync(jwksUri, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var eTag = response.Headers.ETag?.Tag;
            var lastModified = response.Content.Headers.LastModified;

            var jwksJson = await response.Content.ReadAsStringAsync(ct);
            var jwks = new JsonWebKeySet(jwksJson);

            var now = DateTimeOffset.UtcNow;
            var entry = new JwksCacheEntry
            {
                Provider = jwksUri,
                KeySet = jwks,
                Kid = jwks.Keys.FirstOrDefault()?.Kid,
                ETag = eTag,
                LastModified = lastModified,
                FetchedAt = now,
                ExpiresAt = now.AddMinutes(_options.JwksCacheTtlMinutes),
                RefreshAfter = now.AddMinutes(_options.JwksRefreshMinutes),
            };

            var cacheKey = CacheKeyPrefix + jwksUri;
            await _cache.SetAsync(cacheKey, entry, new CacheEntryOptions(TimeSpan.FromMinutes(_options.JwksCacheTtlMinutes)), ct);

            var keys = jwks.GetSigningKeys();
            _logger.LogDebug("JWKS actualizado para {JwksUri}: {KeyCount} claves", jwksUri, keys.Count);
            return Result<ICollection<SecurityKey>>.Success(keys);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _errors);

            var cacheKey = CacheKeyPrefix + jwksUri;
            var staleEntry = await _cache.GetAsync<JwksCacheEntry>(cacheKey, ct);
            if (staleEntry != null && staleEntry.Keys.Count > 0 && !staleEntry.IsExpired)
            {
                Interlocked.Increment(ref _staleFallbacks);
                var staleMaxAge = TimeSpan.FromHours(_options.JwksStaleMaxAgeHours);
                if (DateTimeOffset.UtcNow - staleEntry.FetchedAt < staleMaxAge)
                {
                    _logger.LogWarning(ex, "Error al obtener JWKS {JwksUri}, usando cache stale", jwksUri);
                    return Result<ICollection<SecurityKey>>.Success(staleEntry.Keys);
                }
            }

            _logger.LogError(ex, "Error al obtener JWKS de {JwksUri}", jwksUri);
            return Result<ICollection<SecurityKey>>.Failure("JWKS_FETCH_ERROR", $"Error al obtener JWKS: {ex.Message}");
        }
    }
}
