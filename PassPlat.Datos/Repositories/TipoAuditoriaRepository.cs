using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface ITipoAuditoriaRepository : IRepositoryAsync<TipoAuditoria>
{
    Task<Result<IReadOnlyList<TipoAuditoria>>> ObtenerActivosAsync(CancellationToken ct = default);
}

public class TipoAuditoriaRepository : RepositoryAsync<TipoAuditoria>, ITipoAuditoriaRepository
{
    public TipoAuditoriaRepository(PassPlatDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<Result<IReadOnlyList<TipoAuditoria>>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(t => t.Activo).ToListAsync(ct);
            return Result<IReadOnlyList<TipoAuditoria>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<TipoAuditoria>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
