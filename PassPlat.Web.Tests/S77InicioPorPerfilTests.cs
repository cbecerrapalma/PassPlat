using Bunit;
using PassPlat.Web.Tests.Infra;
using Panel = PassPlat.Web.Pages.Dashboard;

namespace PassPlat.Web.Tests;

/// <summary>
/// S77 · D5 — inicio por perfil: una sola peticion a /api/inicio, tres vistas según
/// el perfil del actor y sin federación de identidades.
/// </summary>
public class S77InicioPorPerfilTests : WebTestBase
{
    private const string InicioTenantJson = """
        {
          "cuenta": { "fecExpiraPwd": "2027-01-15", "metodosMfaActivos": 2, "sesionesActivas": 3, "dispositivosConfiables": 1 },
          "tenant": { "usuarios": 5, "sesionesActivas": 3, "conIntentosExcedidos": 0, "conPasswordExpirada": 0, "dispConfiables": 1, "dispNoConfiables": 0, "actividad": [] },
          "plataforma": null
        }
        """;

    private const string InicioPlataformaJson = """
        {
          "cuenta": { "fecExpiraPwd": null, "metodosMfaActivos": 0, "sesionesActivas": 1, "dispositivosConfiables": 0 },
          "tenant": null,
          "plataforma": {
            "tenantsActivos": 2,
            "apps": 3,
            "usuarios": 3,
            "servicios": [ { "nombre": "EmailBackground", "estado": "Activo", "ultimaEjecucion": "2026-10-30T09:00:00" } ]
          }
        }
        """;

    private const string InicioSinPermisosJson = """
        {
          "cuenta": { "fecExpiraPwd": null, "metodosMfaActivos": 0, "sesionesActivas": 0, "dispositivosConfiables": 0 },
          "tenant": { "usuarios": null, "sesionesActivas": null, "conIntentosExcedidos": null, "conPasswordExpirada": null, "dispConfiables": null, "dispNoConfiables": null, "actividad": null },
          "plataforma": null
        }
        """;

    [Fact]
    public async Task Inicio_hace_una_sola_peticion()
    {
        Handler.Responder("/api/inicio", InicioTenantJson);
        await AutenticarAsync(1, 1, "sistema", "USUARIOS_VER", "SESIONES_VER", "APPS_VER", "TENANTS_VER", "AUDITORIA_VER");

        var cut = Render<Panel>();

        cut.WaitForAssertion(() => Assert.Single(Handler.Peticiones, p => p.Contains("GET /api/inicio")), TimeSpan.FromSeconds(10));
        var otras = Handler.Peticiones.Where(p => p.StartsWith("GET /api/") && !p.Contains("/api/inicio") && !p.Contains("/api/auth/current-tenant")).ToList();
        Assert.Empty(otras);
    }

    [Fact]
    public async Task Sin_permisos_muestra_solo_mi_cuenta()
    {
        Handler.Responder("/api/inicio", InicioSinPermisosJson);
        await AutenticarAsync(5, 1, "u", "PERFIL_VER");

        var cut = Render<Panel>();

        cut.WaitForAssertion(() => Assert.Contains("Mi cuenta", cut.Markup), TimeSpan.FromSeconds(10));
        Assert.DoesNotContain("Usuarios", cut.Markup);
    }

    [Fact]
    public async Task Modo_plataforma_muestra_totales_globales()
    {
        Handler.Responder("/api/inicio", InicioPlataformaJson);
        await AutenticarAsync(1, 0, "sistema", "TENANTS_VER", "APPS_VER", "USUARIOS_VER");

        var cut = Render<Panel>();

        cut.WaitForAssertion(() => Assert.Contains("Plataforma", cut.Markup), TimeSpan.FromSeconds(10));
        Assert.Contains("EmailBackground", cut.Markup);
        Assert.DoesNotContain("Mi tenant", cut.Markup);
    }

    [Fact]
    public async Task Ya_no_muestra_federacion()
    {
        Handler.Responder("/api/inicio", InicioTenantJson);
        await AutenticarAsync(1, 1, "sistema", "USUARIOS_VER", "SESIONES_VER", "APPS_VER", "TENANTS_VER", "AUDITORIA_VER");

        var cut = Render<Panel>();

        cut.WaitForAssertion(() => Assert.DoesNotContain("Federación de Identidades", cut.Markup), TimeSpan.FromSeconds(10));
    }
}
