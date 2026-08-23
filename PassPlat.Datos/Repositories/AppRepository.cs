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

public interface IAppRepository : IRepositoryAsync<App>
{
    Task<Result<App?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default);
    Task<Result<IReadOnlyList<App>>> ObtenerActivasAsync(CancellationToken ct = default);
    Task<Result<(IReadOnlyList<App> Items, int TotalCount)>> BuscarPaginadoAsync(string search, int pageNumber, int pageSize, CancellationToken ct = default);
    Task InvalidarCacheAsync(CancellationToken ct = default);
}

public class AppRepository : RepositoryAsync<App>, IAppRepository
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private readonly ICacheService _cache;
    private readonly ILoggerService _logger;

    public AppRepository(PassPlatDbContext dbContext, ICacheService cache, ILoggerService logger) : base(dbContext)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<App?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(a => a.Codigo == codigo, ct);
            return Result<App?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<App?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<App>>> ObtenerActivasAsync(CancellationToken ct = default)
    {
        try
        {
            const string key = "app:catalog:activas";
            var swCache = Stopwatch.StartNew();
            var cached = await _cache.GetAsync<IReadOnlyList<App>>(key, ct);
            swCache.Stop();
            if (cached != null)
            {
                EmitCacheEvent(LoggingEvents.CacheHit, LoggingCacheResults.Hit, LoggingSources.Memory, key, null, nameof(ObtenerActivasAsync), swCache);
                return Result<IReadOnlyList<App>>.Success(cached);
            }

            var swDb = Stopwatch.StartNew();
            var list = await DbSet.Where(a => a.Activa).ToListAsync(ct);
            swDb.Stop();
            EmitCacheEvent(LoggingEvents.CacheMiss, LoggingCacheResults.Miss, LoggingSources.SqlServer, key, null, nameof(ObtenerActivasAsync), swDb);
            await _cache.SetAsync(key, list, new CacheEntryOptions { SlidingExpiration = CacheTtl }, ct);
            EmitCacheEvent(LoggingEvents.CacheSet, LoggingCacheResults.Refreshed, LoggingSources.Memory, key, null, nameof(ObtenerActivasAsync), swDb);
            return Result<IReadOnlyList<App>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<App>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<(IReadOnlyList<App> Items, int TotalCount)>> BuscarPaginadoAsync(string search, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        try
        {
            var query = DbSet.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(a => a.Codigo.Contains(search) || a.Nombre.Contains(search));

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderBy(a => a.Nombre)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
            return Result<(IReadOnlyList<App>, int)>.Success((items, total));
        }
        catch (Exception ex)
        {
            return Result<(IReadOnlyList<App>, int)>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task InvalidarCacheAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await _cache.RemoveAsync("app:catalog:activas", ct);
        sw.Stop();
        EmitCacheEvent(LoggingEvents.CacheInvalidation, LoggingCacheResults.Invalidated, LoggingSources.Memory, "app:catalog:activas", null, nameof(InvalidarCacheAsync), sw);
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
                [LoggingPropertyNames.Repository] = nameof(AppRepository),
                [LoggingPropertyNames.Operation] = operation,
                [LoggingPropertyNames.Source] = source,
                [LoggingPropertyNames.CacheResult] = cacheResult,
                [LoggingPropertyNames.Key] = key,
                [LoggingPropertyNames.TenantId] = idTenant,
                [LoggingPropertyNames.ElapsedMs] = sw.Elapsed.TotalMilliseconds,
            }
        });
    }
}
