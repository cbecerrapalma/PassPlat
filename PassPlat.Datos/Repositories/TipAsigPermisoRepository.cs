using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Repositories;

public interface ITipAsigPermisoRepository : IRepositoryAsync<TipAsigPermiso>
{
    Task<Result<IReadOnlyList<TipAsigPermiso>>> ObtenerTodosAsync(CancellationToken ct = default);
}

public class TipAsigPermisoRepository : RepositoryAsync<TipAsigPermiso>, ITipAsigPermisoRepository
{
    public TipAsigPermisoRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<TipAsigPermiso>>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.OrderBy(t => t.Id).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<TipAsigPermiso>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<TipAsigPermiso>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
