namespace PassPlat.Datos.Models;

/// <summary>
/// S95 · D1 — filtros de la auditoría de contraseñas. <c>IdTenant</c> null = todos los tenants
/// (solo lo pide un usuario is_system); <c>Hasta</c> incluye el día completo.
/// </summary>
public sealed record FiltroAuditoriaPwd(
    int? IdTenant = null,
    int? IdUsuario = null,
    int? IdTipoAccion = null,
    int? NivelMinimo = null,
    DateTime? Desde = null,
    DateTime? Hasta = null,
    string? Texto = null);
