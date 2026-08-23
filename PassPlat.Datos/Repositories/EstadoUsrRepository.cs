using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IEstadoUsrRepository : IRepositoryAsync<EstadoUsr>
{
    Task<Result<EstadoUsr?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default);
    Task<Result<IReadOnlyList<EstadoUsr>>> ObtenerActivosAsync(CancellationToken ct = default);
}

public class EstadoUsrRepository : RepositoryAsync<EstadoUsr>, IEstadoUsrRepository
{
    public EstadoUsrRepository(PassPlatDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<Result<EstadoUsr?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(e => e.Codigo == codigo, ct);
            return Result<EstadoUsr?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<EstadoUsr?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<EstadoUsr>>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(e => e.Activo).ToListAsync(ct);
            return Result<IReadOnlyList<EstadoUsr>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<EstadoUsr>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
