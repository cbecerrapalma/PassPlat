using Bunit;
using PassPlat.Web.Tests.Infra;
using BloqueosIndex = PassPlat.Web.Pages.Bloqueos.Index;
using TenantDetail = PassPlat.Web.Pages.Tenants.TenantDetail;
using UsuarioDetail = PassPlat.Web.Pages.Usuarios.UsuarioDetail;

namespace PassPlat.Web.Tests;

/// <summary>
/// S68.2 · K5 (D7) — <c>/bloqueos</c> pide la ruta sin segmento de tenant y las paginas
/// de detalle <c>/tenant/{id}</c> y <c>/usuario/{id}</c> exigen su permiso en la UI.
/// </summary>
public class S682BloqueosYDetallesTests : WebTestBase
{
    private const string Usuarios = """
    [
      { "id": 1, "idTenant": 1, "nomUsuario": "sistema", "email": "sistema@passplat.app", "nombre": "Usuario", "apellido": "Sistema", "eliminado": false, "estadoNombre": "Activo" },
      { "id": 2, "idTenant": 1, "nomUsuario": "otro", "email": "otro@passplat.app", "nombre": "Otro", "apellido": "Usuario", "eliminado": false, "estadoNombre": "Activo" }
    ]
    """;

    private const string TenantUno = """
    { "id": 1, "codigo": "PLATFORM", "nombre": "Plataforma", "activo": true, "esSistema": true, "fecCrea": "2026-01-01T00:00:00Z" }
    """;

    public S682BloqueosYDetallesTests()
    {
        Handler.Responder("/api/usuarios", Usuarios);
        Handler.Responder("/api/bloqueos/usuario/1", "[]");
        Handler.Responder("/api/tenants/1", TenantUno);
        Handler.Responder("/api/DominiosTenant/tenant/1", "[]");
        Handler.Responder("/api/configtenants/tenant/1", "{}");
    }

    private async Task<IRenderedComponent<BloqueosIndex>> RenderBloqueosAsync()
    {
        await AutenticarAsync(1, 1, "sistema", "BLOQUEOS_VER");
        return Render<BloqueosIndex>();
    }

    [Fact]
    public async Task Bloqueos_pide_la_ruta_sin_segmento_de_tenant()
    {
        var cut = await RenderBloqueosAsync();

        cut.WaitForAssertion(() => Assert.Contains("No hay registros", cut.Markup));
        Assert.Contains(Handler.Peticiones, p => p.Contains("GET /api/bloqueos/usuario/1"));
        Assert.DoesNotContain(Handler.Peticiones, p => p.Contains("/api/bloqueos/usuario/1/"));
    }

    [Fact]
    public async Task Bloqueos_no_pide_tenants_ni_muestra_selector_de_tenant()
    {
        var cut = await RenderBloqueosAsync();

        cut.WaitForAssertion(() => Assert.Contains("No hay registros", cut.Markup));
        Assert.DoesNotContain(Handler.Peticiones, p => p.Contains("GET /api/tenants"));
        Assert.DoesNotContain("Tenant", cut.Markup);
    }

    [Fact]
    public async Task TenantDetail_sin_TENANTS_VER_muestra_acceso_denegado()
    {
        await AutenticarAsync(1, 1, "sistema"); // sin TENANTS_VER
        var cut = Render<TenantDetail>(p => p.Add(x => x.Id, 1));

        cut.WaitForAssertion(() => Assert.Contains("Acceso Denegado", cut.Markup));
        Assert.DoesNotContain(Handler.Peticiones, p => p.Contains("GET /api/tenants/1"));
    }

    [Fact]
    public async Task TenantDetail_con_TENANTS_VER_carga_el_tenant()
    {
        await AutenticarAsync(1, 1, "sistema", "TENANTS_VER");
        var cut = Render<TenantDetail>(p => p.Add(x => x.Id, 1));

        cut.WaitForAssertion(() => Assert.Contains(Handler.Peticiones, p => p.Contains("GET /api/tenants/1")));
        Assert.DoesNotContain("Acceso Denegado", cut.Markup);
    }

    [Fact]
    public async Task UsuarioDetail_sin_USUARIOS_VER_muestra_acceso_denegado()
    {
        await AutenticarAsync(1, 1, "sistema", "BLOQUEOS_VER"); // sin USUARIOS_VER
        var cut = Render<UsuarioDetail>(p => p.Add(x => x.Id, 1));

        cut.WaitForAssertion(() => Assert.Contains("Acceso Denegado", cut.Markup));
        Assert.DoesNotContain(Handler.Peticiones, p => p.Contains("GET /api/usuarios/1"));
    }
}
