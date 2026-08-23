using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface ITipoMFARepository : IRepositoryAsync<TipoMFA>
{
    Task<Result<IReadOnlyList<TipoMFA>>> ObtenerActivosAsync(CancellationToken ct = default);
}

public class TipoMFARepository : RepositoryAsync<TipoMFA>, ITipoMFARepository
{
    public TipoMFARepository(PassPlatDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<Result<IReadOnlyList<TipoMFA>>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(t => t.Activo).ToListAsync(ct);
            return Result<IReadOnlyList<TipoMFA>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<TipoMFA>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
