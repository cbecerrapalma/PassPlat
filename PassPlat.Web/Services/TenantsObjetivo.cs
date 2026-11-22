using PassPlat.Aplicacion.Dtos.Catalogos;

namespace PassPlat.Web.Services;

/// <summary>
/// S113 · D4 — tenants que la Web ofrece en los selectores de administración.
/// Con TENANTS_VER (el sistema), el catálogo (api/tenants). Sin él (admin de tenant desde S113),
/// solo el tenant de la sesión, tomado de sus membresías (api/auth/tenants, sin permiso).
/// </summary>
public static class TenantsObjetivo
{
    public static async Task<List<TenantDto>> CargarAsync(ApiClient api, AuthService auth)
    {
        if (auth.TienePermiso("TENANTS_VER"))
            return await api.GetAsync<List<TenantDto>>("api/tenants") ?? [];
        var propios = await auth.GetTenantsAsync();
        return [.. propios.Where(t => t.Id == auth.IdTenant).Select(t => new TenantDto { Id = t.Id, Codigo = t.Codigo, Nombre = t.Nombre })];
    }
}
