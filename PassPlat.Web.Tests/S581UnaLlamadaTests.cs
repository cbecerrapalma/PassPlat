using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PassPlat.Web.Tests.Infra;
using UsuarioDetail = PassPlat.Web.Pages.Usuarios.UsuarioDetail;
using UsuarioIntentos = PassPlat.Web.Pages.Usuarios.Components.UsuarioIntentosAcceso;
using RolesPermisos = PassPlat.Web.Pages.RolesPermisos.Index;

namespace PassPlat.Web.Tests;

/// <summary>
/// S58.1 · K2 (D2) — una llamada por recurso: la ficha pide el usuario, la politica
/// y los intentos una sola vez, las pestañas no repiten lo que reciben y la página
/// de permisos directos pide los usuarios una sola vez.
/// </summary>
public class S581UnaLlamadaTests : WebTestBase
{
    private const string Usuario = """
    {
      "id": 1, "idTenant": 1, "nomUsuario": "sistema", "email": "sistema@passplat.app",
      "nombre": "Usuario", "apellido": "Sistema", "eliminado": false, "estadoNombre": "Activo",
      "tenantNombre": "Plataforma", "emailVerificado": true, "reqCambioPwd": false
    }
    """;

    public S581UnaLlamadaTests()
    {
        Handler.Responder("/api/usuarios/1", Usuario);
        Handler.Responder("/api/usuarios/password-policy", """{"id":1,"longMin":10,"longMax":64}""");
        Handler.Responder("/api/intentosacceso/recientes/1/60", "[]");
        Handler.Responder("/api/usuarios", Datos.UsuariosJson());
    }

    private async Task<IRenderedComponent<UsuarioDetail>> RenderFichaAsync()
    {
        await AutenticarAsync(1, 1, "sistema", "USUARIOS_VER", "INTENTOSACCESO_VER"); // S113 · D4 — intentos solo con su permiso
        return Render<UsuarioDetail>(p => p.Add(x => x.Id, 1));
    }

    [Fact]
    public async Task La_ficha_pide_cada_recurso_una_sola_vez()
    {
        var cut = await RenderFichaAsync();

        cut.WaitForAssertion(() => Assert.Contains("sistema", cut.Markup), TimeSpan.FromSeconds(10));
        Assert.Single(Handler.Peticiones, p => p.Contains("GET /api/usuarios/1"));
        Assert.Single(Handler.Peticiones, p => p.Contains("GET /api/usuarios/password-policy"));
        Assert.Single(Handler.Peticiones, p => p.Contains("GET /api/intentosacceso/recientes/1/60"));
    }

    [Fact]
    public async Task La_pestana_de_intentos_no_vuelve_a_pedir_los_datos()
    {
        await AutenticarAsync(1, 1, "sistema", "USUARIOS_VER", "INTENTOSACCESO_VER"); // S113 · D4 — intentos solo con su permiso
        var cut = Render<UsuarioIntentos>(p => p
            .Add(x => x.UserId, 1)
            .Add(x => x.IsActive, true)
            .Add(x => x.Intentos, new List<PassPlat.Aplicacion.Dtos.Core.IntentoAccesoDto>()));

        cut.WaitForAssertion(() => Assert.Contains("Sin intentos registrados", cut.Markup), TimeSpan.FromSeconds(10));
        Assert.DoesNotContain(Handler.Peticiones, p => p.Contains("intentosacceso"));
    }

    // S102: la página maestro-detalle se retiró; ahora prueba que la página corta pide los usuarios una sola vez.
    [Fact]
    public async Task Permisos_directos_pide_los_usuarios_una_sola_vez()
    {
        await AutenticarAsync(1, 1, "sistema", "PERMISOS_VER");
        var cut = Render<TestHost>(p => p.AddChildContent<RolesPermisos>());

        cut.WaitForAssertion(() => Assert.Contains(Handler.Peticiones, p => p.StartsWith("GET /api/usuarios")), TimeSpan.FromSeconds(10));
        Assert.Single(Handler.Peticiones, p => p.StartsWith("GET /api/usuarios"));
        Assert.DoesNotContain(Handler.Peticiones, p => p.Contains("/api/roles") || p.Contains("/api/permisos") || p.Contains("/api/accesos"));
    }
}
