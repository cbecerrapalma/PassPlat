using System.Security.Claims;
using CBP.MultiTenant.Abstractions;

namespace PassPlat.WebAPI.Auth;

/// <summary>
/// S105 · D1 — alcance de tenant de la sesión (P63): el sistema ve todos los tenants; cualquier otro, solo el suyo.
/// <c>null</c> significa «sin acotar».
/// </summary>
public static class AlcanceTenantExtensions
{
    public static bool EsSistema(this ClaimsPrincipal user) => user.HasClaim("is_system", "true");

    /// <summary>S120 · D1 — id del usuario del token (<c>NameIdentifier</c>); <c>null</c> si falta, no es un número o no es positivo.</summary>
    public static int? IdUsuario(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id > 0 ? id : null;

    public static bool EsPropio(this ClaimsPrincipal user, int idUsuario) => user.IdUsuario() == idUsuario;

    public static int? AlcanceTenant(this ClaimsPrincipal user, ITenantContext tenantContext) =>
        user.EsSistema() ? null : tenantContext.CurrentId;

    public static bool FueraDeAlcance(this ClaimsPrincipal user, ITenantContext tenantContext, int idTenant) =>
        user.AlcanceTenant(tenantContext) is int propio && propio != idTenant;

    /// <summary>Lectura de los datos de un usuario: el propio usuario y el sistema ven todo lo suyo; otro, solo su tenant.</summary>
    public static int? AlcanceTenantDe(this ClaimsPrincipal user, ITenantContext tenantContext, int idUsuario) =>
        user.EsPropio(idUsuario) ? null : user.AlcanceTenant(tenantContext);
}
