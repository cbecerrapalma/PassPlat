using CBP.Caching.Interfaces;
using CBP.Caching.Models;
using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IConfigAppRepository : IRepositoryAsync<ConfigApp>
{
    Task<Result<IReadOnlyList<ConfigApp>>> ObtenerPorGrupoAsync(string grupo, CancellationToken ct = default);
    Task<Result<ConfigApp?>> ObtenerPorClaveAsync(string clave, int? idTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ConfigApp>>> ObtenerPorTenantAsync(int? idTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ConfigApp>>> ObtenerActivosAsync(CancellationToken ct = default);
    Task<Result<ConfigApp>> SetValorAsync(string grupo, string clave, string valor, string tipo = "string", string? descripcion = null, int? idTenant = null, CancellationToken ct = default);
    Task<Result<(IReadOnlyList<ConfigApp> Items, int TotalCount)>> BuscarPaginadoAsync(string search, int pageNumber, int pageSize, CancellationToken ct = default);
    Task InvalidarCacheGrupoAsync(string grupo, CancellationToken ct = default);
}

public class ConfigAppRepository : RepositoryAsync<ConfigApp>, IConfigAppRepository
{
    private static readonly TimeSpan HotGroupTtl = TimeSpan.FromSeconds(60);
    private static readonly string[] HotGroups = ["Email", "Branding"];

    private readonly ICacheService _cache;

    public ConfigAppRepository(PassPlatDbContext dbContext, ICacheService cache) : base(dbContext)
    {
        _cache = cache;
    }

    public async Task<Result<IReadOnlyList<ConfigApp>>> ObtenerPorGrupoAsync(string grupo, CancellationToken ct = default)
    {
        try
        {
            var cacheKey = BuildGroupCacheKey(grupo);
            if (IsHotGroup(grupo))
            {
                var cached = await _cache.GetAsync<IReadOnlyList<ConfigApp>>(cacheKey, ct);
                if (cached != null)
                    return Result<IReadOnlyList<ConfigApp>>.Success(cached);
            }

            var list = await DbSet.AsNoTracking().Where(c => c.Grupo == grupo).Include(c => c.Tenant).ToListAsync(ct);

            if (IsHotGroup(grupo))
                await _cache.SetAsync(cacheKey, list, new CacheEntryOptions { SlidingExpiration = HotGroupTtl }, ct);

            return Result<IReadOnlyList<ConfigApp>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<ConfigApp>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<ConfigApp?>> ObtenerPorClaveAsync(string clave, int? idTenant, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.Include(c => c.Tenant).FirstOrDefaultAsync(c => c.Clave == clave && c.IdTenant == idTenant, ct);
            return Result<ConfigApp?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<ConfigApp?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<ConfigApp>>> ObtenerPorTenantAsync(int? idTenant, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(c => c.IdTenant == idTenant).Include(c => c.Tenant).ToListAsync(ct);
            return Result<IReadOnlyList<ConfigApp>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<ConfigApp>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<ConfigApp>>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(c => c.Activo).Include(c => c.Tenant).ToListAsync(ct);
            return Result<IReadOnlyList<ConfigApp>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<ConfigApp>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<ConfigApp>> SetValorAsync(string grupo, string clave, string valor, string tipo = "string", string? descripcion = null, int? idTenant = null, CancellationToken ct = default)
    {
        var existing = await DbSet.FirstOrDefaultAsync(c => c.Grupo == grupo && c.Clave == clave && c.IdTenant == idTenant, ct);
        if (existing != null)
        {
            existing.Valor = valor;
            existing.Activo = true;
            Update(existing);
            await InvalidarCacheGrupoAsync(grupo, ct);
            return Result<ConfigApp>.Success(existing);
        }

        var entity = new ConfigApp
        {
            Grupo = grupo,
            Clave = clave,
            Valor = valor,
            Tipo = tipo,
            Descripcion = descripcion ?? $"Configuracion de {grupo}:{clave}",
            Activo = true,
            IdTenant = idTenant
        };
        await DbSet.AddAsync(entity, ct);
        await InvalidarCacheGrupoAsync(grupo, ct);
        return Result<ConfigApp>.Success(entity);
    }

    public async Task<Result<(IReadOnlyList<ConfigApp> Items, int TotalCount)>> BuscarPaginadoAsync(string search, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        try
        {
            IQueryable<ConfigApp> query = DbSet.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c => c.Clave.Contains(search) || (c.Valor ?? "").Contains(search) || c.Grupo.Contains(search));

            var total = await query.CountAsync(ct);
            var items = await query
                .Include(c => c.Tenant)
                .OrderBy(c => c.Grupo).ThenBy(c => c.Clave)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
            return Result<(IReadOnlyList<ConfigApp>, int)>.Success((items, total));
        }
        catch (Exception ex)
        {
            return Result<(IReadOnlyList<ConfigApp>, int)>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task InvalidarCacheGrupoAsync(string grupo, CancellationToken ct = default)
    {
        if (IsHotGroup(grupo))
            await _cache.RemoveAsync(BuildGroupCacheKey(grupo), ct);
    }

    private static bool IsHotGroup(string grupo) =>
        Array.IndexOf(HotGroups, grupo) >= 0;

    private static string BuildGroupCacheKey(string grupo) =>
        $"configapp:grupo:{grupo}";
}
