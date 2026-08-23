using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Repositories;

public interface IEmailAccountRepository : IRepositoryAsync<EmailAccount>
{
    Task<Result<IReadOnlyList<EmailAccount>>> ObtenerActivosAsync(CancellationToken ct = default);
    Task<Result<EmailAccount?>> ObtenerPredeterminadaAsync(CancellationToken ct = default);
}

public class EmailAccountRepository : RepositoryAsync<EmailAccount>, IEmailAccountRepository
{
    public EmailAccountRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<EmailAccount>>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(e => e.Activo).Include(e => e.Provider).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<EmailAccount>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<EmailAccount>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<EmailAccount?>> ObtenerPredeterminadaAsync(CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.Where(e => e.EsPredeterminada && e.Activo).Include(e => e.Provider).FirstOrDefaultAsync(ct);
            return Result<EmailAccount?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<EmailAccount?>.Failure("DB_ERROR", ex.Message);
        }
    }
}
