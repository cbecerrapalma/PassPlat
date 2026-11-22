using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Dtos.Core;

namespace PassPlat.Web.Pages.Accesos;

/// <summary>
/// S54 · D3/K1 — reglas puras de la asignacion de accesos (sin UI, probables).
/// S54.1 · D5 — el aviso de reemplazo describe el acceso que se reemplaza.
/// El dialogo y la pagina solo las usan.
/// </summary>
public static class ReglasAsignacionAcceso
{
    /// <summary>
    /// Roles que se pueden asignar: solo activos, globales (IdTenant nulo) o del tenant de la sesion.
    /// El servidor reemplaza el tenant del cuerpo por el de la sesion (H4), asi que
    /// un rol de otro tenant no serviria de nada.
    /// </summary>
    public static IReadOnlyList<RolDto> RolesPermitidos(IEnumerable<RolDto> roles, int idTenantSesion)
        => roles
            .Where(r => r.Activo && (r.IdTenant is null || r.IdTenant == idTenantSesion))
            .ToList();

    /// <summary>Solo se puede asignar con usuario, app y rol elegidos de la lista.</summary>
    public static bool PuedeAsignar(UsuarioDto? usuario, AppDto? app, RolDto? rol)
        => usuario is not null && app is not null && rol is not null;

    /// <summary>Acceso ya existente del usuario para esa app (H5: un acceso por usuario y app).</summary>
    public static AccesoDto? BuscarAccesoExistente(IEnumerable<AccesoDto> accesos, int idApp)
        => accesos.FirstOrDefault(a => a.IdApp == idApp);

    /// <summary>
    /// D5 — ¿El acceso resultante seria identico al que ya existe? Mismo rol y mismo tenant.
    /// S54.2 · D1 — solo el identificador del rol: hay roles activos con el mismo nombre en
    /// distintos tenants (Administrador Id 1 global, 5 de ABARROTES, 9 de VESTUARIO) y
    /// comparar por nombre tomaba por "sin cambios" un cambio real de rol.
    /// </summary>
    public static bool EsMismoAcceso(AccesoDto? existente, RolDto? rolNuevo, int idTenantSesion)
        => existente is not null
           && rolNuevo is not null
           && existente.IdTenant == idTenantSesion
           && existente.IdRol == rolNuevo.Id;

    /// <summary>
    /// D5 — Aviso de reemplazo exacto: describe el rol y el tenant del acceso que ya existe.
    /// Mismo rol y mismo tenant: no hay cambios que guardar. Otro rol u otro tenant: reemplazo.
    /// S54.2 · D2 — sin rol elegido no se afirma "no hay cambios": el boton esta deshabilitado
    /// por falta de rol, asi que el aviso solo describe el acceso que ya existe.
    /// </summary>
    public static string? TextoReemplazo(AccesoDto? existente, RolDto? rolNuevo, int idTenantSesion, string? nombreTenantSesion)
    {
        if (existente is null) return null;

        var rolExistente = string.IsNullOrWhiteSpace(existente.RolNombre) ? "(sin rol)" : existente.RolNombre;
        var tenantExistente = string.IsNullOrWhiteSpace(existente.TenantNombre) ? "(sin tenant)" : existente.TenantNombre;

        if (rolNuevo is null)
            return $"Ya tiene el rol {rolExistente} en el tenant {tenantExistente} para esta app.";

        if (EsMismoAcceso(existente, rolNuevo, idTenantSesion))
            return $"Ya tiene el rol {rolExistente} en el tenant {tenantExistente} para esta app: no hay cambios que guardar.";

        var destino = string.IsNullOrWhiteSpace(nombreTenantSesion)
            ? "el tenant de tu sesión"
            : $"el tenant de tu sesión ({nombreTenantSesion})";
        return $"Reemplaza el rol {rolExistente} (tenant {tenantExistente}) por {rolNuevo.Nombre} en {destino}.";
    }
}
