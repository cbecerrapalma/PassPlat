using CBP.Events;

namespace PassPlat.Aplicacion.Services.Security;

public sealed record NewDeviceDetectedEvent(
    int IdUsuario,
    int IdTenant,
    int IdDisp,
    string? NombreDispositivo,
    string? TipoDispositivo,
    string? Fabricante,
    string? Modelo,
    string? IpAddress,
    string? UserAgent) : EventBase
{
    public override string EventType => "NewDeviceDetected";
    public DateTime FechaDeteccion { get; init; } = DateTime.Now;
}

public sealed record DeviceRevokedEvent(
    int IdUsuario,
    int IdTenant,
    int IdDisp,
    string? NombreDispositivo,
    string? RevocadoPor) : EventBase
{
    public override string EventType => "DeviceRevoked";
    public DateTime FechaRevoca { get; init; } = DateTime.Now;
}
