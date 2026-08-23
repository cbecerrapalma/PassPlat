using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Repositories;

public interface IEstIdenExtRepository : IRepositoryAsync<EstIdenExt>
{
    Task<Result<EstIdenExt?>> ObtenerPorNombreAsync(string nombre, CancellationToken ct = default);
    Task<Result<IReadOnlyList<EstIdenExt>>> ObtenerActivosAsync(CancellationToken ct = default);
}

public class EstIdenExtRepository : RepositoryAsync<EstIdenExt>, IEstIdenExtRepository
{
    public EstIdenExtRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<EstIdenExt?>> ObtenerPorNombreAsync(string nombre, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(e => e.Nombre == nombre, ct);
            return Result<EstIdenExt?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<EstIdenExt?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<EstIdenExt>>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(e => e.Activo).OrderBy(e => e.Orden).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<EstIdenExt>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<EstIdenExt>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
