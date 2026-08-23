using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Repositories;

public interface IAppEmailAccountRepository : IRepositoryAsync<AppEmailAccount>
{
    Task<Result<IReadOnlyList<AppEmailAccount>>> ObtenerPorAppAsync(int idApp, CancellationToken ct = default);
}

public class AppEmailAccountRepository : RepositoryAsync<AppEmailAccount>, IAppEmailAccountRepository
{
    public AppEmailAccountRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<AppEmailAccount>>> ObtenerPorAppAsync(int idApp, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(e => e.IdApp == idApp && e.Activo)
                .Include(e => e.EmailAccount)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<AppEmailAccount>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<AppEmailAccount>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
