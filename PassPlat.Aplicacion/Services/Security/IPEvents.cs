using CBP.Events;

namespace PassPlat.Aplicacion.Services.Security;

public sealed record NewIpDetectedEvent(
    int IdUsuario,
    int IdTenant,
    int IdIP,
    string DireccionIP,
    string? Pais,
    string? Ciudad,
    string? UserAgent,
    string? DeviceName) : EventBase
{
    public override string EventType => "NewIpDetected";
    public DateTime FechaDeteccion { get; init; } = DateTime.Now;
}

public sealed record SecurityAlertEvent(
    int IdUsuario,
    int IdTenant,
    int IdIP,
    string DireccionIP,
    string AlertType,
    string Detalles) : EventBase
{
    public override string EventType => "SecurityAlert";
    public DateTime FechaAlerta { get; init; } = DateTime.Now;
}
