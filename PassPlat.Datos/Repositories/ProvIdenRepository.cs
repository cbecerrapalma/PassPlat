using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Repositories;

public interface IProvIdenRepository : IRepositoryAsync<ProvIden>
{
    Task<Result<ProvIden?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ProvIden>>> ObtenerActivosAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<ProvIden>>> ObtenerTodosOrdenadosAsync(CancellationToken ct = default);
}

public class ProvIdenRepository : RepositoryAsync<ProvIden>, IProvIdenRepository
{
    public ProvIdenRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<ProvIden?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(p => p.Codigo == codigo, ct);
            return Result<ProvIden?>.Success(entity, allowNull: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<ProvIden?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<ProvIden>>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(p => p.Activo).OrderBy(p => p.Orden).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<ProvIden>>.Success(list);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<ProvIden>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<ProvIden>>> ObtenerTodosOrdenadosAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.OrderBy(p => p.Orden).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<ProvIden>>.Success(list);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<ProvIden>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
