using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IResultadoAccesoRepository : IRepositoryAsync<ResultadoAcceso>
{
    Task<Result<ResultadoAcceso?>> ObtenerExitosoAsync(CancellationToken ct = default);
}

public class ResultadoAccesoRepository : RepositoryAsync<ResultadoAcceso>, IResultadoAccesoRepository
{
    public ResultadoAccesoRepository(PassPlatDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<Result<ResultadoAcceso?>> ObtenerExitosoAsync(CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(r => r.EsExitoso, ct);
            return Result<ResultadoAcceso?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<ResultadoAcceso?>.Failure("DB_ERROR", ex.Message);
        }
    }
}
