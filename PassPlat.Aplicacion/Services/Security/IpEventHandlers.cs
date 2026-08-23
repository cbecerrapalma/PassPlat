using CBP.Events;
using CBP.Results;
using PassPlat.Aplicacion.Services.Email;

namespace PassPlat.Aplicacion.Services.Security;

public sealed class NewIpDetectedEventHandler : IEventHandler<NewIpDetectedEvent>
{
    private readonly IEmailQueue _emailQueue;

    public NewIpDetectedEventHandler(IEmailQueue emailQueue) => _emailQueue = emailQueue;

    public async Task<Result> HandleAsync(NewIpDetectedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await _emailQueue.EnqueueAsync(new EmailJob(
                EmailJobKind.NewIp,
                string.Empty,
                string.Empty,
                new Dictionary<string, object?>
                {
                    ["IdUsuario"] = @event.IdUsuario,
                    ["IdTenant"] = @event.IdTenant,
                    ["IdIP"] = @event.IdIP,
                    ["AppName"] = "PassPlat",
                    ["IP"] = @event.DireccionIP,
                    ["DireccionIP"] = @event.DireccionIP,
                    ["Pais"] = @event.Pais,
                    ["Ciudad"] = @event.Ciudad,
                    ["UserAgent"] = @event.UserAgent,
                    ["DeviceName"] = @event.DeviceName,
                    ["FechaDeteccion"] = @event.FechaDeteccion.ToString("yyyy-MM-dd HH:mm:ss")
                },
                IdTenant: @event.IdTenant,
                IdUsuario: @event.IdUsuario,
                IdApp: null,
                CorrelationId: @event.CorrelationId), cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("NOTIFY_ERROR", $"Error al encolar notificación de nueva IP: {ex.Message}");
        }
    }
}

public sealed class SecurityAlertEventHandler : IEventHandler<SecurityAlertEvent>
{
    private readonly IEmailQueue _emailQueue;

    public SecurityAlertEventHandler(IEmailQueue emailQueue) => _emailQueue = emailQueue;

    public async Task<Result> HandleAsync(SecurityAlertEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await _emailQueue.EnqueueAsync(new EmailJob(
                EmailJobKind.SecurityAlert,
                string.Empty,
                string.Empty,
                new Dictionary<string, object?>
                {
                    ["IdUsuario"] = @event.IdUsuario,
                    ["IdTenant"] = @event.IdTenant,
                    ["IdIP"] = @event.IdIP,
                    ["DireccionIP"] = @event.DireccionIP,
                    ["AlertType"] = @event.AlertType,
                    ["Detalles"] = @event.Detalles,
                    ["FechaAlerta"] = @event.FechaAlerta.ToString("yyyy-MM-dd HH:mm:ss")
                },
                IdTenant: @event.IdTenant,
                IdUsuario: @event.IdUsuario,
                IdApp: null,
                CorrelationId: @event.CorrelationId), cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("NOTIFY_ERROR", $"Error al encolar alerta de seguridad: {ex.Message}");
        }
    }
}