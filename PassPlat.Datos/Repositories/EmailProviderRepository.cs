using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Repositories;

public interface IEmailProviderRepository : IRepositoryAsync<EmailProvider>
{
    Task<Result<EmailProvider?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default);
}

public class EmailProviderRepository : RepositoryAsync<EmailProvider>, IEmailProviderRepository
{
    public EmailProviderRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<EmailProvider?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(e => e.Codigo == codigo, ct);
            return Result<EmailProvider?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<EmailProvider?>.Failure("DB_ERROR", ex.Message);
        }
    }
}
