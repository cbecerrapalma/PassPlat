using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IModuloRepository : IRepositoryAsync<Modulo>
{
    Task<Result<IReadOnlyList<Modulo>>> ObtenerRaicesAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<Modulo>>> ObtenerArbolCompletoAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<Modulo>>> ObtenerPorTipoAsync(int idTipoModulo, CancellationToken ct = default);
    Task<Result<IReadOnlyList<Modulo>>> ObtenerVisiblesMenuAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<Modulo>>> ObtenerPorAppAsync(int idApp, CancellationToken ct = default);
}

public class ModuloRepository : RepositoryAsync<Modulo>, IModuloRepository
{
    public ModuloRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<Modulo>>> ObtenerRaicesAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Include(m => m.TipoModulo).Where(m => m.IdModuloPadre == null && m.Activo)
                .OrderBy(m => m.Orden).ThenBy(m => m.Nombre).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<Modulo>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Modulo>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Modulo>>> ObtenerArbolCompletoAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Include(m => m.TipoModulo).Include(m => m.ModuloPadre)
                .OrderBy(m => m.Orden).ThenBy(m => m.Nombre).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<Modulo>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Modulo>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Modulo>>> ObtenerPorTipoAsync(int idTipoModulo, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Include(m => m.TipoModulo).Where(m => m.IdTipoModulo == idTipoModulo && m.Activo)
                .OrderBy(m => m.Orden).ThenBy(m => m.Nombre).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<Modulo>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Modulo>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Modulo>>> ObtenerVisiblesMenuAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Include(m => m.TipoModulo).Where(m => m.EsVisibleMenu && m.Activo)
                .OrderBy(m => m.Orden).ThenBy(m => m.Nombre).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<Modulo>>.Success(list);
        }
        catch (Exception ex)
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
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Modulo>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
