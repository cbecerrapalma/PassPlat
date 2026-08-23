using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface ITipoCambioPwdRepository : IRepositoryAsync<TipoCambioPwd>
{
    Task<Result<TipoCambioPwd?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default);
}

public class TipoCambioPwdRepository : RepositoryAsync<TipoCambioPwd>, ITipoCambioPwdRepository
{
    public TipoCambioPwdRepository(PassPlatDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<Result<TipoCambioPwd?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(t => t.Codigo == codigo, ct);
            return Result<TipoCambioPwd?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<TipoCambioPwd?>.Failure("DB_ERROR", ex.Message);
        }
    }
}
