using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface INotificacionRepository : IRepositoryAsync<Notificacion>
{
    Task<Result<IReadOnlyList<Notificacion>>> ObtenerNoLeidasAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<int>> ContarNoLeidasAsync(int idUsuario, CancellationToken ct = default);
    Task<Result> MarcarTodasComoLeidasAsync(int idUsuario, CancellationToken ct = default);
    Result MarcarComoLeida(int idNotificacion);
    Task<Result<IReadOnlyList<Notificacion>>> ObtenerPorTipoAsync(int idUsuario, string tipoNotif, int cantidad, CancellationToken ct = default);
    Task<Result<int>> EliminarNotificacionesAntiguasAsync(int dias, CancellationToken ct = default);
    Result<Notificacion> CrearNotificacion(int idUsuario, int idTenant, string tipoNotif, string asunto, string? mensaje = null);
}

public class NotificacionRepository : RepositoryAsync<Notificacion>, INotificacionRepository
{
    public NotificacionRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<Notificacion>>> ObtenerNoLeidasAsync(int idUsuario, CancellationToken ct = default)
    {
        try
        {
            var result = await DbSet.Where(n => n.IdUsuario == idUsuario && !n.Leida).OrderByDescending(n => n.FecCrea).ToListAsync(ct);
            return Result<IReadOnlyList<Notificacion>>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Notificacion>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<int>> ContarNoLeidasAsync(int idUsuario, CancellationToken ct = default)
    {
        try
        {
            return Result<int>.Success(await DbSet.CountAsync(n => n.IdUsuario == idUsuario && !n.Leida, ct));
        }
        catch (Exception ex)
        {
            return Result<int>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result> MarcarTodasComoLeidasAsync(int idUsuario, CancellationToken ct = default)
    {
        try
        {
            await DbSet.Where(n => n.IdUsuario == idUsuario && !n.Leida)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(n => n.Leida, true)
                    .SetProperty(n => n.FecLeida, DateTime.Now), ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("DB_ERROR", ex.Message);
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
        catch (Exception ex)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Notificacion>>> ObtenerPorTipoAsync(int idUsuario, string tipoNotif, int cantidad, CancellationToken ct = default)
    {
        try
        {
            var result = await Query().Where(n => n.IdUsuario == idUsuario && n.TipoNotif == tipoNotif)
                .OrderByDescending(n => n.FecCrea).Take(cantidad).ToListAsync(ct);
            return Result<IReadOnlyList<Notificacion>>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Notificacion>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<int>> EliminarNotificacionesAntiguasAsync(int dias, CancellationToken ct = default)
    {
        try
        {
            var fechaLimite = DateTime.Now.AddDays(-dias);
            var count = await DbSet.Where(n => n.FecCrea < fechaLimite).ExecuteDeleteAsync(ct);
            return Result<int>.Success(count);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure("DB_ERROR", ex.Message);
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
        catch (Exception ex)
        {
            return Result<Notificacion>.Failure("DB_ERROR", ex.Message);
        }
    }
}