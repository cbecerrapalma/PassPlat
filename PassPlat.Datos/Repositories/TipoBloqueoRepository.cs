using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface ITipoBloqueoRepository : IRepositoryAsync<TipoBloqueo>
{
    Task<Result<IReadOnlyList<TipoBloqueo>>> ObtenerActivosAsync(CancellationToken ct = default);
}

public class TipoBloqueoRepository : RepositoryAsync<TipoBloqueo>, ITipoBloqueoRepository
{
    public TipoBloqueoRepository(PassPlatDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<Result<IReadOnlyList<TipoBloqueo>>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(t => t.Activo).ToListAsync(ct);
            return Result<IReadOnlyList<TipoBloqueo>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<TipoBloqueo>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
