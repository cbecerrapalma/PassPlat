using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PassPlat.Web.Tests.Infra;
using Panel = PassPlat.Web.Pages.Dashboard;

namespace PassPlat.Web.Tests;

/// <summary>
/// S58 · K5 (D6) — panel principal: cabecera compacta, indicadores con enlace a su
/// lista, accesos rapidos y estado de seguridad con semaforo.
/// S77 · D5 — stubea solo /api/inicio con un JSON de tenant; Cada_recurso_se_pide_una_sola_vez
/// exige un unico GET /api/inicio y ninguno a /api/federacion.
/// </summary>
public class S58DashboardTests : WebTestBase
{
    private const string InicioTenantJson = """
        {
          "cuenta": {
            "fecExpiraPwd": "2027-01-15",
            "metodosMfaActivos": 2,
            "sesionesActivas": 3,
            "dispositivosConfiables": 1
          },
          "tenant": {
            "usuarios": 5,
            "sesionesActivas": 3,
            "conIntentosExcedidos": 0,
            "conPasswordExpirada": 0,
            "dispConfiables": 1,
            "dispNoConfiables": 0,
            "actividad": [
              { "detalles": "Login de usuario", "nivelRiesgo": 1, "fechaAccion": "2026-10-30T10:00:00", "tipoAccionNombre": "Login", "usuarioNombre": "sistema" }
            ]
          },
          "plataforma": null
        }
        """;

    public S58DashboardTests()
    {
        Handler.Responder("/api/inicio", InicioTenantJson);
    }

    private async Task<IRenderedComponent<Panel>> RenderPanelAsync()
    {
        await AutenticarAsync(1, 1, "sistema", "USUARIOS_VER", "SESIONES_VER", "APPS_VER", "TENANTS_VER", "AUDITORIA_VER", "MATRIZ_PERMISOS_VER");
        return Render<Panel>();
    }

    [Fact]
    public async Task Los_indicadores_arden_a_su_lista()
    {
        var cut = await RenderPanelAsync();

        cut.WaitForAssertion(() => Assert.Contains("iam-kpi-link", cut.Markup), TimeSpan.FromSeconds(10));
        var enlaces = cut.FindAll("a.iam-kpi-link").Select(a => a.GetAttribute("href")).ToList();
        Assert.Contains("/usuarios", enlaces);
        Assert.Contains("/sesiones", enlaces);
        Assert.DoesNotContain("/apps", enlaces);
        Assert.DoesNotContain("/tenants", enlaces);
    }

    [Fact]
    public async Task La_cabecera_es_compacta_y_tiene_actualizar()
    {
        var cut = await RenderPanelAsync();

        cut.WaitForAssertion(() => Assert.Contains("Actualizar", cut.Markup), TimeSpan.FromSeconds(10));
        cut.WaitForAssertion(() => Assert.Contains("dash-head", cut.Markup), TimeSpan.FromSeconds(10));
        Assert.DoesNotContain("dash-welcome", cut.Markup);
    }

    [Fact]
    public async Task Hay_accesos_rapidos_y_semaforo_de_seguridad()
    {
        var cut = await RenderPanelAsync();

        cut.WaitForAssertion(() => Assert.Contains("Accesos rápidos", cut.Markup), TimeSpan.FromSeconds(10));
        cut.WaitForAssertion(() => Assert.Contains("Estado de seguridad", cut.Markup), TimeSpan.FromSeconds(10));
        cut.WaitForAssertion(() => Assert.Contains("dash-status-success", cut.Markup), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Cada_recurso_se_pide_una_sola_vez()
    {
        var cut = await RenderPanelAsync();

        cut.WaitForAssertion(() => Assert.Single(Handler.Peticiones, p => p.Contains("GET /api/inicio")), TimeSpan.FromSeconds(10));
        var otras = Handler.Peticiones.Where(p => p.StartsWith("GET /api/") && !p.Contains("/api/inicio") && !p.Contains("/api/auth/current-tenant")).ToList();
        Assert.Empty(otras);
        Assert.DoesNotContain(Handler.Peticiones, p => p.Contains("GET /api/federacion"));
    }
}
