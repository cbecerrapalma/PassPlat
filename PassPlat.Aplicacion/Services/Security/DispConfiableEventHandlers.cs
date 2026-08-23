using CBP.Events;
using CBP.Results;
using PassPlat.Aplicacion.Services.Email;

namespace PassPlat.Aplicacion.Services.Security;

public sealed class NewDeviceDetectedEventHandler : IEventHandler<NewDeviceDetectedEvent>
{
    private readonly IEmailQueue _emailQueue;

    public NewDeviceDetectedEventHandler(IEmailQueue emailQueue) => _emailQueue = emailQueue;

    public async Task<Result> HandleAsync(NewDeviceDetectedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await _emailQueue.EnqueueAsync(new EmailJob(
                EmailJobKind.NewDevice,
                string.Empty,
                string.Empty,
                new Dictionary<string, object?>
                {
                    ["IdUsuario"] = @event.IdUsuario,
                    ["IdTenant"] = @event.IdTenant,
                    ["IdDisp"] = @event.IdDisp,
                    ["AppName"] = "PassPlat",
                    ["Dispositivo"] = @event.NombreDispositivo,
                    ["NombreDispositivo"] = @event.NombreDispositivo,
                    ["TipoDispositivo"] = @event.TipoDispositivo,
                    ["Fabricante"] = @event.Fabricante,
                    ["Modelo"] = @event.Modelo,
                    ["IP"] = @event.IpAddress,
                    ["IpAddress"] = @event.IpAddress,
                    ["UserAgent"] = @event.UserAgent,
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
            return Result.Failure("NOTIFY_ERROR", $"Error al encolar notificación de nuevo dispositivo: {ex.Message}");
        }
    }
}

public sealed class DeviceRevokedEventHandler : IEventHandler<DeviceRevokedEvent>
{
    private readonly IEmailQueue _emailQueue;

    public DeviceRevokedEventHandler(IEmailQueue emailQueue) => _emailQueue = emailQueue;

    public async Task<Result> HandleAsync(DeviceRevokedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await _emailQueue.EnqueueAsync(new EmailJob(
                EmailJobKind.DeviceRevoked,
                string.Empty,
                string.Empty,
                new Dictionary<string, object?>
                {
                    ["IdUsuario"] = @event.IdUsuario,
                    ["IdTenant"] = @event.IdTenant,
                    ["IdDisp"] = @event.IdDisp,
                    ["NombreDispositivo"] = @event.NombreDispositivo,
                    ["FechaRevoca"] = @event.FechaRevoca.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["RevocadoPor"] = @event.RevocadoPor
                },
                IdTenant: @event.IdTenant,
                IdUsuario: @event.IdUsuario,
                IdApp: null,
                CorrelationId: @event.CorrelationId), cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("NOTIFY_ERROR", $"Error al encolar notificación de dispositivo revocado: {ex.Message}");
        }
    }
}