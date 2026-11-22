using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Repositories;

public interface IAudIdenExtRepository : IRepositoryAsync<AudIdenExt>
{
    Task<Result<IReadOnlyList<AudIdenExt>>> ObtenerPorTenantAsync(int idTenant, int limite = 50, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AudIdenExt>>> ObtenerPorUsuarioAsync(int idUsuario, int limite = 50, CancellationToken ct = default);
}

public class AudIdenExtRepository : RepositoryAsync<AudIdenExt>, IAudIdenExtRepository
{
    public AudIdenExtRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<AudIdenExt>>> ObtenerPorTenantAsync(int idTenant, int limite = 50, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet
                .Include(a => a.ProvIden)
                .Where(a => a.IdTenant == idTenant)
                .OrderByDescending(a => a.FecEvento)
                .Take(limite)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<AudIdenExt>>.Success(list);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<AudIdenExt>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<AudIdenExt>>> ObtenerPorUsuarioAsync(int idUsuario, int limite = 50, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet
                .Include(a => a.ProvIden)
                .Where(a => a.IdUsuario == idUsuario)
                .OrderByDescending(a => a.FecEvento)
                .Take(limite)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<AudIdenExt>>.Success(list);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<AudIdenExt>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
