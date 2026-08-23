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
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IConfigTenantRepository : IRepositoryAsync<ConfigTenant>
{
    Task<Result<ConfigTenant?>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
    Result<ConfigTenant> ObtenerOCrear(int idTenant);
    Result ActualizarPepperVersion(int idTenant, byte version);
}

public class ConfigTenantRepository : RepositoryAsync<ConfigTenant>, IConfigTenantRepository
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private readonly ICacheService _cache;
    private readonly ILoggerService _logger;

    public ConfigTenantRepository(PassPlatDbContext dbContext, ICacheService cache, ILoggerService logger) : base(dbContext)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<ConfigTenant?>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        try
        {
            var key = BuildKey(idTenant);
            var swCache = Stopwatch.StartNew();
            var cached = await _cache.GetAsync<ConfigTenant>(key, ct);
            swCache.Stop();
            if (cached != null)
            {
                EmitCacheEvent(LoggingEvents.CacheHit, LoggingCacheResults.Hit, LoggingSources.Memory, key, idTenant, nameof(ObtenerPorTenantAsync), swCache);
                return Result<ConfigTenant?>.Success(cached, allowNull: true);
            }

            var swDb = Stopwatch.StartNew();
            var entity = await DbSet.FirstOrDefaultAsync(c => c.IdTenant == idTenant, ct);
            swDb.Stop();
            EmitCacheEvent(LoggingEvents.CacheMiss, LoggingCacheResults.Miss, LoggingSources.SqlServer, key, idTenant, nameof(ObtenerPorTenantAsync), swDb);
            if (entity != null)
            {
                await _cache.SetAsync(key, entity, new CacheEntryOptions { SlidingExpiration = CacheTtl }, ct);
                EmitCacheEvent(LoggingEvents.CacheSet, LoggingCacheResults.Refreshed, LoggingSources.Memory, key, idTenant, nameof(ObtenerPorTenantAsync), swDb);
            }
            return Result<ConfigTenant?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<ConfigTenant?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public Result<ConfigTenant> ObtenerOCrear(int idTenant)
    {
        try
        {
            var config = DbSet.FirstOrDefault(c => c.IdTenant == idTenant);
            if (config != null) return Result<ConfigTenant>.Success(config);
            config = new ConfigTenant { IdTenant = idTenant };
            DbSet.Add(config);
            return Result<ConfigTenant>.Success(config);
        }
        catch (Exception ex)
        {
            return Result<ConfigTenant>.Failure("DB_ERROR", ex.Message);
        }
    }

    public Result ActualizarPepperVersion(int idTenant, byte version)
    {
        try
        {
            var config = DbSet.FirstOrDefault(c => c.IdTenant == idTenant);
            if (config == null)
                return Result.Failure("CONFIG_NOT_FOUND", "Configuración de tenant no encontrada");

            config.PepperVersionActual = version;
            var sw = Stopwatch.StartNew();
            _ = _cache.RemoveAsync(BuildKey(idTenant));
            sw.Stop();
            EmitCacheEvent(LoggingEvents.CacheInvalidation, LoggingCacheResults.Invalidated, LoggingSources.Memory, BuildKey(idTenant), idTenant, nameof(ActualizarPepperVersion), sw);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    private void EmitCacheEvent(string eventName, string cacheResult, string source, string key, int idTenant, string operation, Stopwatch sw)
    {
        _logger.LogInformation(new LogEvent
        {
            EventName = eventName,
            Message = $"{CacheTtl.TotalSeconds:0}s TTL | {operation} | key={key} | {source} | {cacheResult}",
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.DataCache,
                [LoggingPropertyNames.Repository] = nameof(ConfigTenantRepository),
                [LoggingPropertyNames.Operation] = operation,
                [LoggingPropertyNames.Source] = source,
                [LoggingPropertyNames.CacheResult] = cacheResult,
                [LoggingPropertyNames.Key] = key,
                [LoggingPropertyNames.TenantId] = idTenant,
                [LoggingPropertyNames.ElapsedMs] = sw.Elapsed.TotalMilliseconds,
            }
        });
    }

    private static string BuildKey(int idTenant) => $"configtenant:tenant:{idTenant}";
}
