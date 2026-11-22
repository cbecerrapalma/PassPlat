using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface INotificacionRepository : IRepositoryAsync<Notificacion>
{
    // S118 · D5 — idTenant: null = sin acotar; con valor = solo las notificaciones de ese tenant.
    Task<Result<IReadOnlyList<Notificacion>>> ObtenerNoLeidasAsync(int idUsuario, int? idTenant, CancellationToken ct = default);
    Task<Result> MarcarTodasComoLeidasAsync(int idUsuario, int? idTenant, CancellationToken ct = default);
    Task<Result<(IReadOnlyList<Notificacion> Items, int TotalCount)>> ObtenerPaginadoPorTenantAsync(int idTenant, int pageNumber, int pageSize, CancellationToken ct = default);
    Result MarcarComoLeida(int idNotificacion);
    Result<Notificacion> CrearNotificacion(int idUsuario, int idTenant, string tipoNotif, string asunto, string? mensaje = null);
}

public class NotificacionRepository : RepositoryAsync<Notificacion>, INotificacionRepository
{
    public NotificacionRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<Notificacion>>> ObtenerNoLeidasAsync(int idUsuario, int? idTenant, CancellationToken ct = default)
    {
        try
        {
            var result = await DbSet.Where(n => n.IdUsuario == idUsuario && !n.Leida && (idTenant == null || n.IdTenant == idTenant))
                .OrderByDescending(n => n.FecCrea).ToListAsync(ct);
            return Result<IReadOnlyList<Notificacion>>.Success(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<Notificacion>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result> MarcarTodasComoLeidasAsync(int idUsuario, int? idTenant, CancellationToken ct = default)
    {
        try
        {
            await DbSet.Where(n => n.IdUsuario == idUsuario && !n.Leida && (idTenant == null || n.IdTenant == idTenant))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(n => n.Leida, true)
                    .SetProperty(n => n.FecLeida, DateTime.Now), ct);
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<(IReadOnlyList<Notificacion> Items, int TotalCount)>> ObtenerPaginadoPorTenantAsync(int idTenant, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        try
        {
            var query = DbSet.AsNoTracking().Where(n => n.IdTenant == idTenant);
            var totalCount = await query.CountAsync(ct);
            var items = await query.OrderByDescending(n => n.FecCrea)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            return Result<(IReadOnlyList<Notificacion>, int)>.Success((items, totalCount));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<(IReadOnlyList<Notificacion>, int)>.Failure("DB_ERROR", ex.Message);
        }
    }

    public Result MarcarComoLeida(int idNotificacion)
    {
        try
        {
            var notif = DbSet.FirstOrDefault(n => n.Id == idNotificacion && !n.Leida);
            if (notif == null)
                return Result.Failure("NOTIF_NOT_FOUND", "Notificación no encontrada o ya leída");

            notif.MarcarLeida();
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    public Result<Notificacion> CrearNotificacion(int idUsuario, int idTenant, string tipoNotif, string asunto, string? mensaje = null)
    {
        try
        {
            var notificacion = Notificacion.Crear(idUsuario, idTenant, tipoNotif, asunto, mensaje);
            DbSet.Add(notificacion);
            return Result<Notificacion>.Success(notificacion);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<Notificacion>.Failure("DB_ERROR", ex.Message);
        }
    }
}