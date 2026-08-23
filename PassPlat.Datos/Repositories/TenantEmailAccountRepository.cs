using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Repositories;

public interface ITenantEmailAccountRepository : IRepositoryAsync<TenantEmailAccount>
{
    Task<Result<IReadOnlyList<TenantEmailAccount>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
}

public class TenantEmailAccountRepository : RepositoryAsync<TenantEmailAccount>, ITenantEmailAccountRepository
{
    public TenantEmailAccountRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<TenantEmailAccount>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(e => e.IdTenant == idTenant && e.Activo)
                .Include(e => e.EmailAccount)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<TenantEmailAccount>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<TenantEmailAccount>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
