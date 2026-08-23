using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IAppModuloRepository : IRepositoryAsync<AppModulo>
{
    Task<Result<IReadOnlyList<AppModulo>>> ObtenerPorAppAsync(int idApp, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AppModulo>>> ObtenerActivosPorAppAsync(int idApp, CancellationToken ct = default);
}

public class AppModuloRepository : RepositoryAsync<AppModulo>, IAppModuloRepository
{
    public AppModuloRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<AppModulo>>> ObtenerPorAppAsync(int idApp, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Include(am => am.Modulo).Where(am => am.IdApp == idApp)
                .OrderBy(am => am.Modulo != null ? am.Modulo.Orden : 0).ToListAsync(ct);
            return Result<IReadOnlyList<AppModulo>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<AppModulo>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<AppModulo>>> ObtenerActivosPorAppAsync(int idApp, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Include(am => am.Modulo).Where(am => am.IdApp == idApp && am.Activo)
                .OrderBy(am => am.Modulo != null ? am.Modulo.Orden : 0).ToListAsync(ct);
            return Result<IReadOnlyList<AppModulo>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<AppModulo>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
