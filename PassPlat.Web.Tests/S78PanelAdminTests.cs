using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using NavMenu = PassPlat.Web.Layout.NavMenu;
using PanelAdmin = PassPlat.Web.Pages.Admin.DashboardEnterprise;

namespace PassPlat.Web.Tests;

/// <summary>
/// S78 — panel de administración único: responde en /admin/panel y en las rutas viejas,
/// al entrar pide solo estado general + Ejecutivo y cada pestaña pide su sección la primera vez.
/// </summary>
public class S78PanelAdminTests : WebTestBase
{
    private const string Seccion = "api/dashboard-enterprise/";

    private static bool Cargando(IRenderedComponent<PanelAdmin> cut) =>
        (bool)typeof(PanelAdmin).GetField("_loading", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cut.Instance)!;

    private List<string> PeticionesDeSeccion() =>
        Handler.Peticiones.Where(p => p.Contains(Seccion, StringComparison.OrdinalIgnoreCase)).ToList();

    private int Pedidas(string seccion) =>
        PeticionesDeSeccion().Count(p => p.Contains(Seccion + seccion, StringComparison.OrdinalIgnoreCase)
                                         && !p.Contains(Seccion + seccion + "-", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public async Task Panel_al_cargar_pide_solo_estado_general_y_ejecutivo()
    {
        await AutenticarAsync(permisos: "USUARIOS_VER");

        var cut = Render<PanelAdmin>();

        cut.WaitForAssertion(() => Assert.False(Cargando(cut)));
        Assert.Equal(1, Pedidas("estado-general"));
        Assert.Equal(1, Pedidas("ejecutivo"));
        Assert.Equal(2, PeticionesDeSeccion().Count);
        Assert.Contains("Panel de administración", cut.Markup);
    }

    [Fact]
    public async Task Panel_al_abrir_Seguridad_la_pide_una_sola_vez()
    {
        await AutenticarAsync(permisos: "USUARIOS_VER");
        var cut = Render<PanelAdmin>();
        cut.WaitForAssertion(() => Assert.False(Cargando(cut)));

        cut.FindAll(".mud-tab")[1].Click();
        cut.WaitForAssertion(() => Assert.Equal(1, Pedidas("seguridad")));
        cut.FindAll(".mud-tab")[0].Click();
        cut.FindAll(".mud-tab")[1].Click();

        cut.WaitForAssertion(() => Assert.Equal(1, Pedidas("seguridad")));
        Assert.Equal(1, Pedidas("ejecutivo"));
        Assert.Equal(0, Pedidas("oauth"));
    }

    [Fact]
    public void Panel_responde_en_las_cuatro_rutas()
    {
        var rutas = typeof(PanelAdmin).GetCustomAttributes<RouteAttribute>().Select(r => r.Template).OrderBy(r => r).ToArray();

        Assert.Equal(
            ["/admin/dashboard-enterprise", "/admin/dashboard-operacional", "/admin/iam-dashboard", "/admin/panel"],
            rutas);
    }

    [Fact]
    public async Task NavMenu_muestra_una_sola_entrada_de_panel()
    {
        await AutenticarAsync(permisos: "USUARIOS_VER");
        Handler.Responder("/api/modulos/menu", S83MenuDePrueba.Json());
        await Menu.AsegurarCargaAsync();

        var cut = Render<NavMenu>();

        var enlaces = cut.FindAll("a[href]").Select(a => a.GetAttribute("href")!).ToList();
        Assert.Single(enlaces, h => h == "/admin/panel");
        Assert.DoesNotContain(enlaces, h => h.Contains("dashboard-enterprise") || h.Contains("iam-dashboard") || h.Contains("dashboard-operacional"));
        Assert.Single(cut.FindAll("a"), a => a.TextContent.Contains("Panel de administración"));
    }
}
