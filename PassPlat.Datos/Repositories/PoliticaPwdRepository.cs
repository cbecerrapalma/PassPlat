using System.Diagnostics;
using CBP.Caching.Interfaces;
using CBP.Caching.Models;
using CBP.Data.Abstractions;
using CBP.Logging;
using CBP.Logging.Interfaces;
using CBP.Logging.Models;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IPoliticaPwdRepository : IRepositoryAsync<PoliticaPwd>
{
    Task<Result<PoliticaPwd?>> ObtenerActivaPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<PoliticaPwd?>> ObtenerActivaPorTenantYAppAsync(int idTenant, int idApp, CancellationToken ct = default);
    Task<Result<PoliticaPwd?>> ObtenerPoliticaGlobalAsync(CancellationToken ct = default);
    Task<Result<PoliticaPwd?>> ObtenerPoliticaAplicableAsync(int idTenant, int? idApp = null, CancellationToken ct = default);
    Task<Result<PoliticaPwd?>> ObtenerPoliticaParaRolAsync(int idTenant, int idRol, CancellationToken ct = default);
    Task<Result<IReadOnlyList<PoliticaPwd>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
    Result DesactivarPolitica(int idPolitica);
    Task InvalidarCacheAsync(CancellationToken ct = default);
}

public class PoliticaPwdRepository : RepositoryAsync<PoliticaPwd>, IPoliticaPwdRepository
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private readonly ICacheService _cache;
    private readonly ILoggerService _logger;

    public PoliticaPwdRepository(PassPlatDbContext dbContext, ICacheService cache, ILoggerService logger) : base(dbContext)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<PoliticaPwd?>> ObtenerActivaPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        try
        {
            var key = BuildApplicableKey(idTenant, null);
            var swCache = Stopwatch.StartNew();
            var cached = await _cache.GetAsync<PoliticaPwd>(key, ct);
            swCache.Stop();
            if (cached != null)
            {
                EmitCacheEvent(LoggingEvents.CacheHit, LoggingCacheResults.Hit, LoggingSources.Memory, key, idTenant, nameof(ObtenerActivaPorTenantAsync), swCache);
                return Result<PoliticaPwd?>.Success(cached, allowNull: true);
            }

            var swDb = Stopwatch.StartNew();
            var entity = await DbSet.AsNoTracking().FirstOrDefaultAsync(p => p.IdTenant == idTenant && p.Activa, ct);
            swDb.Stop();
            EmitCacheEvent(LoggingEvents.CacheMiss, LoggingCacheResults.Miss, LoggingSources.SqlServer, key, idTenant, nameof(ObtenerActivaPorTenantAsync), swDb);
            if (entity != null)
            {
                await _cache.SetAsync(key, entity, new CacheEntryOptions { SlidingExpiration = CacheTtl }, ct);
                EmitCacheEvent(LoggingEvents.CacheSet, LoggingCacheResults.Refreshed, LoggingSources.Memory, key, idTenant, nameof(ObtenerActivaPorTenantAsync), swDb);
            }
            return Result<PoliticaPwd?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<PoliticaPwd?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<PoliticaPwd?>> ObtenerActivaPorTenantYAppAsync(int idTenant, int idApp, CancellationToken ct = default)
    {
        try
        {
            var key = BuildApplicableKey(idTenant, idApp);
            var swCache = Stopwatch.StartNew();
            var cached = await _cache.GetAsync<PoliticaPwd>(key, ct);
            swCache.Stop();
            if (cached != null)
            {
                EmitCacheEvent(LoggingEvents.CacheHit, LoggingCacheResults.Hit, LoggingSources.Memory, key, idTenant, nameof(ObtenerActivaPorTenantYAppAsync), swCache);
                return Result<PoliticaPwd?>.Success(cached, allowNull: true);
            }

            var swDb = Stopwatch.StartNew();
            var entity = await DbSet.AsNoTracking().FirstOrDefaultAsync(p => p.IdTenant == idTenant && p.IdApp == idApp && p.Activa, ct);
            swDb.Stop();
            EmitCacheEvent(LoggingEvents.CacheMiss, LoggingCacheResults.Miss, LoggingSources.SqlServer, key, idTenant, nameof(ObtenerActivaPorTenantYAppAsync), swDb);
            if (entity != null)
            {
                await _cache.SetAsync(key, entity, new CacheEntryOptions { SlidingExpiration = CacheTtl }, ct);
                EmitCacheEvent(LoggingEvents.CacheSet, LoggingCacheResults.Refreshed, LoggingSources.Memory, key, idTenant, nameof(ObtenerActivaPorTenantYAppAsync), swDb);
            }
            return Result<PoliticaPwd?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<PoliticaPwd?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<PoliticaPwd?>> ObtenerPoliticaGlobalAsync(CancellationToken ct = default)
    {
        try
        {
            const string key = "politicapwd:global";
            var swCache = Stopwatch.StartNew();
            var cached = await _cache.GetAsync<PoliticaPwd>(key, ct);
            swCache.Stop();
            if (cached != null)
            {
                EmitCacheEvent(LoggingEvents.CacheHit, LoggingCacheResults.Hit, LoggingSources.Memory, key, null, nameof(ObtenerPoliticaGlobalAsync), swCache);
                return Result<PoliticaPwd?>.Success(cached, allowNull: true);
            }

            var swDb = Stopwatch.StartNew();
            var entity = await DbSet.AsNoTracking().FirstOrDefaultAsync(p => p.IdTenant == null && p.IdApp == null && p.Activa, ct);
            swDb.Stop();
            EmitCacheEvent(LoggingEvents.CacheMiss, LoggingCacheResults.Miss, LoggingSources.SqlServer, key, null, nameof(ObtenerPoliticaGlobalAsync), swDb);
            if (entity != null)
            {
                await _cache.SetAsync(key, entity, new CacheEntryOptions { SlidingExpiration = CacheTtl }, ct);
                EmitCacheEvent(LoggingEvents.CacheSet, LoggingCacheResults.Refreshed, LoggingSources.Memory, key, null, nameof(ObtenerPoliticaGlobalAsync), swDb);
            }
            return Result<PoliticaPwd?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<PoliticaPwd?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<PoliticaPwd?>> ObtenerPoliticaAplicableAsync(int idTenant, int? idApp = null, CancellationToken ct = default)
    {
        try
        {
            if (idApp.HasValue)
            {
                var politicaApp = await ObtenerActivaPorTenantYAppAsync(idTenant, idApp.Value, ct);
                if (politicaApp.IsFailure) return politicaApp;
                if (politicaApp.Value != null) return politicaApp;
            }
            var tenantResult = await ObtenerActivaPorTenantAsync(idTenant, ct);
            if (tenantResult.IsFailure) return tenantResult;
            if (tenantResult.Value != null) return tenantResult;

            return await ObtenerPoliticaGlobalAsync(ct);
        }
        catch (Exception ex)
        {
            return Result<PoliticaPwd?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<PoliticaPwd?>> ObtenerPoliticaParaRolAsync(int idTenant, int idRol, CancellationToken ct = default)
    {
        try
        {
            var entity = await Query().Where(p => p.Activa && p.RolesPoliticasPwd.Any(rp => rp.IdTenant == idTenant && rp.IdRol == idRol && rp.Activo))
                .AsNoTracking().FirstOrDefaultAsync(ct);
            return Result<PoliticaPwd?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<PoliticaPwd?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<PoliticaPwd>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(p => p.IdTenant == idTenant || p.IdTenant == null)
                .OrderByDescending(p => p.Activa).ThenBy(p => p.Codigo).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<PoliticaPwd>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<PoliticaPwd>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public Result DesactivarPolitica(int idPolitica)
    {
        try
        {
            var politica = DbSet.FirstOrDefault(p => p.Id == idPolitica && p.Activa);
            if (politica == null)
                return Result.Failure("POLITICA_NOT_FOUND", "Política no encontrada o ya inactiva");

            politica.Desactivar();
            _ = InvalidarCacheAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task InvalidarCacheAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await _cache.RemoveByPatternAsync("politicapwd:", ct);
        sw.Stop();
        EmitCacheEvent(LoggingEvents.CacheInvalidation, LoggingCacheResults.Invalidated, LoggingSources.Memory, "politicapwd:", null, nameof(InvalidarCacheAsync), sw);
    }

    private void EmitCacheEvent(string eventName, string cacheResult, string source, string key, int? idTenant, string operation, Stopwatch sw)
    {
        _logger.LogInformation(new LogEvent
        {
            EventName = eventName,
            Message = $"{CacheTtl.TotalSeconds:0}s TTL | {operation} | key={key} | {source} | {cacheResult}",
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.DataCache,
                [LoggingPropertyNames.Repository] = nameof(PoliticaPwdRepository),
                [LoggingPropertyNames.Operation] = operation,
                [LoggingPropertyNames.Source] = source,
                [LoggingPropertyNames.CacheResult] = cacheResult,
                [LoggingPropertyNames.Key] = key,
                [LoggingPropertyNames.TenantId] = idTenant,
                [LoggingPropertyNames.ElapsedMs] = sw.Elapsed.TotalMilliseconds,
            }
        });
    }

    private static string BuildApplicableKey(int idTenant, int? idApp)
    {
        return idApp.HasValue
            ? $"politicapwd:applicable:{idTenant}:{idApp.Value}"
            : $"politicapwd:applicable:{idTenant}";
    }
}
