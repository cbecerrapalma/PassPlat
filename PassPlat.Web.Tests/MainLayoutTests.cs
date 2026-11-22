using Bunit;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PassPlat.Web.Layout;
using PassPlat.Web.Tests.Infra;

namespace PassPlat.Web.Tests;

/// <summary>
/// S55.1 · K4 (D4) — el menu lateral arranca cerrado en celular (ancho menor de 960 px)
/// y abierto en escritorio. El boton de la barra expone el estado con aria-expanded.
/// </summary>
public class MainLayoutTests : WebTestBase
{
    public MainLayoutTests()
    {
        Services.AddSingleton<IWebAssemblyHostEnvironment>(new EntornoWebAssembly());
        Services.AddSingleton<IBrowserViewportService>(new ViewportFalso());
        Handler.Responder("/api/notificaciones", "[]");
        Handler.Responder("/api/tenants", "[]");
        Handler.Responder("/api/apps", "[]");
        Handler.Responder("/api/modulos/menu", S83MenuDePrueba.Json());
    }

    [Fact]
    public void El_menu_lateral_arranca_cerrado_en_celular()
    {
        JSInterop.SetupModule("./js/layout.js").Setup<bool>("esMovil").SetResult(true);

        var cut = Render<MainLayout>();

        cut.WaitForAssertion(() => Assert.Equal("false", BotonMenu(cut).GetAttribute("aria-expanded")));
    }

    [Fact]
    public void El_menu_lateral_arranca_abierto_en_escritorio()
    {
        JSInterop.SetupModule("./js/layout.js").Setup<bool>("esMovil").SetResult(false);

        var cut = Render<MainLayout>();

        cut.WaitForAssertion(() => Assert.Equal("true", BotonMenu(cut).GetAttribute("aria-expanded")));
    }

    [Fact]
    public void El_boton_del_menu_tiene_nombre_accesible_y_estado()
    {
        JSInterop.SetupModule("./js/layout.js").Setup<bool>("esMovil").SetResult(false);

        var cut = Render<MainLayout>();

        var boton = BotonMenu(cut);
        Assert.Equal("Abrir o cerrar el menu lateral", boton.GetAttribute("aria-label"));
        Assert.NotNull(boton.GetAttribute("aria-expanded"));
    }

    [Fact]
    public void En_el_primer_render_se_comprueba_el_ancho_de_la_ventana()
    {
        var modulo = JSInterop.SetupModule("./js/layout.js");
        modulo.Setup<bool>("esMovil").SetResult(true);

        Render<MainLayout>();

        // S89 · D3 — el ancho lo da el modulo del layout, no un eval.
        Assert.Contains(modulo.Invocations, i => i.Identifier == "esMovil");
    }

    // S82 — app.css desplaza la lista con este selector; si MudBlazor cambia el envoltorio del cajón, falla aquí.
    [Fact]
    public void El_menu_es_hijo_directo_del_contenido_del_cajon()
    {
        JSInterop.SetupModule("./js/layout.js").Setup<bool>("esMovil").SetResult(false);

        var cut = Render<MainLayout>();

        Assert.Single(cut.FindAll(".mud-drawer .mud-drawer-content > .mud-navmenu"));
    }

    private static AngleSharp.Dom.IElement BotonMenu(IRenderedComponent<MainLayout> cut) =>
        cut.Find("button[aria-label='Abrir o cerrar el menu lateral']");

    private sealed class EntornoWebAssembly : IWebAssemblyHostEnvironment
    {
        public string Environment { get; set; } = "Development";

        public string BaseAddress { get; set; } = "https://localhost:5001/";
    }
}
