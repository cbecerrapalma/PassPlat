using Bunit;
using NavMenu = PassPlat.Web.Layout.NavMenu;

namespace PassPlat.Web.Tests;

/// <summary>
/// S83 · D5/D6 — el menú lateral se dibuja desde <c>api/modulos/menu</c>: grupos de la BD, reintento si
/// la carga falla y recarga al cambiar la sesión.
/// </summary>
public class S83NavMenuTests : WebTestBase
{
    private const string RutaMenu = "/api/modulos/menu";

    private static List<string> Enlaces(IRenderedComponent<NavMenu> cut) =>
        cut.FindAll("a[href]").Select(a => a.GetAttribute("href")!).ToList();

    [Fact]
    public async Task El_menu_se_dibuja_desde_el_catalogo()
    {
        await AutenticarAsync(permisos: "USUARIOS_VER");
        Handler.Responder(RutaMenu, S83MenuDePrueba.Json());

        var cut = Render<NavMenu>();

        cut.WaitForAssertion(() => Assert.Contains("/admin/panel", Enlaces(cut)));
        Assert.Equal(["/", "/usuarios", "/admin/panel", "/admin/roles", "/auditoria", "/email/providers"], Enlaces(cut));
        Assert.Contains("Correos", cut.Markup);
        Assert.DoesNotContain("Infraestructura", cut.Markup);
        Assert.Empty(cut.FindAll(".nav-section-label"));
        Assert.Contains("mud-navmenu", cut.Nodes.OfType<AngleSharp.Dom.IElement>().First().ClassList);
    }

    [Fact]
    public async Task Si_falla_la_carga_muestra_reintentar()
    {
        await AutenticarAsync(permisos: "USUARIOS_VER");

        var cut = Render<NavMenu>();

        cut.WaitForAssertion(() => Assert.Contains("No se pudo cargar el menú", cut.Markup));
        Assert.Empty(Enlaces(cut));

        Handler.Responder(RutaMenu, S83MenuDePrueba.Json());
        cut.FindAll("button").Single(b => b.TextContent.Contains("Reintentar")).Click();

        cut.WaitForAssertion(() => Assert.Contains("/usuarios", Enlaces(cut)));
        Assert.DoesNotContain("No se pudo cargar el menú", cut.Markup);
    }

    [Fact]
    public async Task Al_cambiar_la_sesion_el_menu_se_recarga()
    {
        await AutenticarAsync(permisos: "USUARIOS_VER");
        Handler.Responder(RutaMenu, S83MenuDePrueba.Json([S83MenuDePrueba.Nodo("PASSPLAT.INICIO", "Panel principal", "/", "Dashboard")]));
        var cut = Render<NavMenu>();
        cut.WaitForAssertion(() => Assert.Equal(["/"], Enlaces(cut)));

        Handler.Responder(RutaMenu, S83MenuDePrueba.Json());
        await cut.InvokeAsync(() => Auth.SetSessionFromFragmentAsync(Token(1, ["USUARIOS_VER", "AUDITORIA_VER"]), null, 2, 1, "otro", false));

        cut.WaitForAssertion(() => Assert.Contains("/auditoria", Enlaces(cut)));
        Assert.Equal(2, Handler.Peticiones.Count(p => p.EndsWith(RutaMenu, StringComparison.OrdinalIgnoreCase)));
    }
}
