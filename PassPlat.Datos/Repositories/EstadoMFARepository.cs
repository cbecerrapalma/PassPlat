using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IEstadoMFARepository : IRepositoryAsync<EstadoMFA>
{
    Task<Result<EstadoMFA?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default);
}

public class EstadoMFARepository : RepositoryAsync<EstadoMFA>, IEstadoMFARepository
{
    public EstadoMFARepository(PassPlatDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<Result<EstadoMFA?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(e => e.Codigo == codigo, ct);
            return Result<EstadoMFA?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<EstadoMFA?>.Failure("DB_ERROR", ex.Message);
        }
    }
}
