using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Dtos.Core;

namespace PassPlat.Web.Services;

/// <summary>
/// S112 · D1 — apps que la Web ofrece en el filtro «App» de las listas de administración.
/// El sistema (claim is_system) ve el catálogo completo (api/apps, con APPS_VER). Cualquier otro
/// usuario ve solo las apps donde tiene un Acceso activo en su tenant
/// (api/UsuariosPermisos/contextos/{yo}, con PERMISOS_VER), las mismas que la API acepta como idApp (S110-S111).
/// </summary>
public static class AppsObjetivo
{
    /// <summary>
    /// S113 · D4 — diálogos de asignación: con APPS_VER (plataforma), el catálogo; sin él (admin de tenant
    /// desde S113), las apps de sus Accesos (<see cref="CargarAsync"/>). Decide por el permiso, no por is_system.
    /// </summary>
    public static async Task<List<AppDto>> CatalogoOAccesosAsync(ApiClient api, AuthService auth) =>
        auth.TienePermiso("APPS_VER") ? await api.GetAsync<List<AppDto>>("api/apps") ?? [] : await CargarAsync(api, auth);

    public static async Task<List<AppDto>> CargarAsync(ApiClient api, AuthService auth)
    {
        if (auth.EsSistema)
            return auth.TienePermiso("APPS_VER") ? await api.GetAsync<List<AppDto>>("api/apps") ?? [] : [];
        if (auth.IdUsuario is not int idUsuario || !auth.TienePermiso("PERMISOS_VER"))
            return [];
        var contextos = await api.GetAsync<List<ContextoPermisoDto>>($"api/UsuariosPermisos/contextos/{idUsuario}") ?? [];
        return
        [
            .. contextos
                .Where(c => c.IdTenant == auth.IdTenant)
                .DistinctBy(c => c.IdApp)
                .Select(c => new AppDto { Id = c.IdApp, Codigo = c.AppCodigo, Nombre = c.AppNombre, Activa = true })
                .OrderBy(a => a.Nombre, StringComparer.CurrentCultureIgnoreCase)
        ];
    }
}
