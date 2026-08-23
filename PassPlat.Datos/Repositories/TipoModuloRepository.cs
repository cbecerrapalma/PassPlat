using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Repositories;

public interface ITipoModuloRepository : IRepositoryAsync<TipoModulo>
{
    Task<Result<TipoModulo?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default);
}

public class TipoModuloRepository : RepositoryAsync<TipoModulo>, ITipoModuloRepository
{
    public TipoModuloRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<TipoModulo?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(t => t.Codigo == codigo, ct);
            return Result<TipoModulo?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<TipoModulo?>.Failure("DB_ERROR", ex.Message);
        }
    }
}
