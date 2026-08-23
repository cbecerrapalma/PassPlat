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
    Task<Result<IReadOnlyList<Permiso>>> ObtenerPorModuloAsync(string modulo, CancellationToken ct = default);
}

public class PermisoRepository : RepositoryAsync<Permiso>, IPermisoRepository
{
    public PermisoRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<Permiso>>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Include(p => p.Modulo).OrderBy(p => p.Modulo != null ? p.Modulo.Codigo : "").ThenBy(p => p.Orden).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<Permiso>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Permiso>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Permiso>>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Include(p => p.Modulo).Where(p => p.Activo).OrderBy(p => p.Modulo != null ? p.Modulo.Codigo : "").ThenBy(p => p.Orden).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<Permiso>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Permiso>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Permiso>>> ObtenerPorModuloAsync(string modulo, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Include(p => p.Modulo).Where(p => p.Modulo != null && p.Modulo.Codigo == modulo && p.Activo).OrderBy(p => p.Orden).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<Permiso>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Permiso>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
