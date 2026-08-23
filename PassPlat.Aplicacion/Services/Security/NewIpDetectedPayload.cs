namespace PassPlat.Aplicacion.Services.Security;

internal record NewIpDetectedPayload
{
    public int IdUsuario { get; init; }
    public int IdTenant { get; init; }
    public int IdIP { get; init; }
    public string DireccionIP { get; init; } = string.Empty;
    public string? Pais { get; init; }
    public string? Ciudad { get; init; }
    public string? UserAgent { get; init; }
    public string? DeviceName { get; init; }
    public string? UserEmail { get; init; }
}
