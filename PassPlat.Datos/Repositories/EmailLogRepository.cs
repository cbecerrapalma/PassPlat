using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Repositories;

public interface IEmailLogRepository : IRepositoryAsync<EmailLog>
{
    Task<Result<IReadOnlyList<EmailLog>>> ObtenerPendientesAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<EmailLog>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<bool>> ExisteNotificacionNuevaIpAsync(int idUsuario, string direccionIP, CancellationToken ct = default);
}

public class EmailLogRepository : RepositoryAsync<EmailLog>, IEmailLogRepository
{
    public EmailLogRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<EmailLog>>> ObtenerPendientesAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(el => el.Estado == "pendiente" && el.Intentos < 3)
                .OrderBy(el => el.FecCrea)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<EmailLog>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<EmailLog>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<EmailLog>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(el => el.IdUsuario == idUsuario)
                .OrderByDescending(el => el.FecCrea)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<EmailLog>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<EmailLog>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<bool>> ExisteNotificacionNuevaIpAsync(int idUsuario, string direccionIP, CancellationToken ct = default)
    {
        try
        {
            var exists = await DbSet.AnyAsync(el =>
                    el.IdUsuario == idUsuario
                    && el.ExtraJson != null
                    && el.ExtraJson.Contains(direccionIP)
                    && el.ExtraJson.Contains("NewIp"), ct);
            return Result<bool>.Success(exists);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure("DB_ERROR", ex.Message);
        }
    }
}
