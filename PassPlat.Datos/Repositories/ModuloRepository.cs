using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IModuloRepository : IRepositoryAsync<Modulo>
{
    Task<Result<IReadOnlyList<Modulo>>> ObtenerArbolCompletoAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<Modulo>>> ObtenerPorAppAsync(int idApp, CancellationToken ct = default);
    Task<Result<IReadOnlyList<Modulo>>> ObtenerMenuPorAppAsync(int idApp, CancellationToken ct = default);
}

public class ModuloRepository : RepositoryAsync<Modulo>, IModuloRepository
{
    public ModuloRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<Modulo>>> ObtenerArbolCompletoAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Include(m => m.TipoModulo).Include(m => m.ModuloPadre)
                .OrderBy(m => m.Orden).ThenBy(m => m.Nombre).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<Modulo>>.Success(list);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<Modulo>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Modulo>>> ObtenerPorAppAsync(int idApp, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Include(m => m.TipoModulo)
                .Where(m => m.Activo && m.AppsModulos.Any(am => am.IdApp == idApp && am.Activo))
                .OrderBy(m => m.Orden).ThenBy(m => m.Nombre).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<Modulo>>.Success(list);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<Modulo>>.Failure("DB_ERROR", ex.Message);
        }
    }

    /// <summary>S83 · D3 — módulos activos asociados (activos) a la app, con sus permisos; sin filtro por tipo.</summary>
    public async Task<Result<IReadOnlyList<Modulo>>> ObtenerMenuPorAppAsync(int idApp, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Include(m => m.Permisos)
                .Where(m => m.Activo && m.AppsModulos.Any(am => am.IdApp == idApp && am.Activo))
                .AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<Modulo>>.Success(list);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<Modulo>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
