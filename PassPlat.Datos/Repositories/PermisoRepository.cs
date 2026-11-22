using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IPermisoRepository : IRepositoryAsync<Permiso>
{
    Task<Result<IReadOnlyList<Permiso>>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<Permiso>>> ObtenerActivosAsync(CancellationToken ct = default);
    // S104 · D2 — permiso por id con su módulo y su app (null si no existe)
    Task<Result<Permiso?>> ObtenerPorIdConAppAsync(int id, CancellationToken ct = default);
    // S103 · D3 — permisos activos cuyo módulo pertenece (AppsModulos activa) a la app indicada
    Task<Result<IReadOnlyList<Permiso>>> ObtenerActivosPorAppAsync(int idApp, CancellationToken ct = default);
    // S110 · D5 — permisos filtrados por lista de apps (mismo include que ObtenerActivosPorAppAsync)
    Task<Result<IReadOnlyList<Permiso>>> ObtenerPorAppsAsync(IReadOnlyCollection<int> idsApp, bool soloActivos, CancellationToken ct = default);
}

public class PermisoRepository : RepositoryAsync<Permiso>, IPermisoRepository
{
    public PermisoRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<Permiso>>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        try
        {
            // S104 · D2 — la app de cada permiso sale de su módulo (AppsModulos activas), igual que en ObtenerActivosAsync
            var list = await DbSet.Include(p => p.Modulo).ThenInclude(m => m!.AppsModulos.Where(am => am.Activo)).ThenInclude(am => am.App)
                .OrderBy(p => p.Modulo != null ? p.Modulo.Codigo : "").ThenBy(p => p.Orden).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<Permiso>>.Success(list);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<Permiso>>.Failure("DB_ERROR", ex.Message);
        }
    }

    // S104 · D2 — un permiso con su módulo y su app, para el detalle
    public async Task<Result<Permiso?>> ObtenerPorIdConAppAsync(int id, CancellationToken ct = default)
    {
        try
        {
            var permiso = await DbSet.Include(p => p.Modulo).ThenInclude(m => m!.AppsModulos.Where(am => am.Activo)).ThenInclude(am => am.App)
                .AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
            return Result<Permiso?>.Success(permiso, allowNull: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<Permiso?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Permiso>>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        try
        {
            // S103 · D7 — la app de cada permiso sale de su módulo (AppsModulos activas)
            var list = await DbSet.Include(p => p.Modulo).ThenInclude(m => m!.AppsModulos.Where(am => am.Activo)).ThenInclude(am => am.App)
                .Where(p => p.Activo).OrderBy(p => p.Modulo != null ? p.Modulo.Codigo : "").ThenBy(p => p.Orden).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<Permiso>>.Success(list);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<Permiso>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Permiso>>> ObtenerActivosPorAppAsync(int idApp, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Include(p => p.Modulo)
                .Where(p => p.Activo && p.Modulo != null && p.Modulo.AppsModulos.Any(am => am.IdApp == idApp && am.Activo))
                .OrderBy(p => p.Modulo!.Codigo).ThenBy(p => p.Orden).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<Permiso>>.Success(list);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<Permiso>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Permiso>>> ObtenerPorAppsAsync(IReadOnlyCollection<int> idsApp, bool soloActivos, CancellationToken ct = default)
    {
        try
        {
            var query = DbSet.Include(p => p.Modulo).ThenInclude(m => m!.AppsModulos.Where(am => am.Activo)).ThenInclude(am => am.App).AsNoTracking();
            if (soloActivos) query = query.Where(p => p.Activo);
            query = query.Where(p => p.Modulo != null && p.Modulo.AppsModulos.Any(am => idsApp.Contains(am.IdApp) && am.Activo));
            var list = await query.OrderBy(p => p.Modulo!.Codigo).ThenBy(p => p.Orden).ToListAsync(ct);
            return Result<IReadOnlyList<Permiso>>.Success(list);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<Permiso>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
