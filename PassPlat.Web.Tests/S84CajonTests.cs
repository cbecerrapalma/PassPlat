using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PassPlat.Web.Layout;
using PassPlat.Web.Tests.Infra;

namespace PassPlat.Web.Tests;

/// <summary>
/// S84 · D2-D5 — la barra y el cajon sin datos de maqueta: sin avisos fijos, el ambito de la
/// sesion en lugar de un rol fijo, la version del ensamblado y el pie sin botones sin destino.
/// </summary>
public class S84CajonTests : WebTestBase
{
    public S84CajonTests()
    {
        Services.AddSingleton<IWebAssemblyHostEnvironment>(new EntornoWebAssembly());
        Services.AddSingleton<IBrowserViewportService>(new ViewportFalso());
        Handler.Responder("/api/notificaciones", "[]");
        Handler.Responder("/api/notificaciones/no-leidas/7", "[]");
        Handler.Responder("/api/tenants", "[]");
        Handler.Responder("/api/apps", "[]");
        Handler.Responder("/api/modulos/menu", S83MenuDePrueba.Json());
        JSInterop.SetupModule("./js/layout.js").Setup<bool>("esMovil").SetResult(false);
    }

    [Fact]
    public async Task La_barra_no_muestra_avisos_de_maqueta()
    {
        await AutenticarAsync(7, 0, "sistema", "NOTIFICACIONES_VER");

        var cut = Render<MainLayout>();

        Assert.DoesNotContain("Nuevo acceso detectado", cut.Markup);
        Assert.DoesNotContain("sin leer de 12", cut.Markup);
    }

    [Fact]
    public async Task El_cajon_muestra_el_ambito_y_no_un_rol_fijo()
    {
        await AutenticarAsync(7, 0, "sistema", "NOTIFICACIONES_VER");

        var cut = Render<MainLayout>();

        Assert.Equal("Plataforma", cut.Find(".drawer-user-role").TextContent.Trim());
    }

    [Fact]
    public async Task La_version_del_cajon_y_del_pie_sale_del_ensamblado()
    {
        await AutenticarAsync(7, 0, "sistema", "NOTIFICACIONES_VER");
        var ensamblado = typeof(MainLayout).Assembly;
        var informativa = ensamblado.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var esperado = informativa is null
            ? ensamblado.GetName().Version?.ToString(3) ?? "0.0.0"
            : informativa.Split('+')[0];

        var cut = Render<MainLayout>();

        Assert.Contains("v" + esperado, cut.Find(".drawer-footer-text").TextContent);
        Assert.Contains("v" + esperado, cut.Find(".footer-version").TextContent);
    }

    [Fact]
    public async Task El_pie_no_tiene_Documentacion_ni_Soporte()
    {
        await AutenticarAsync(7, 0, "sistema", "NOTIFICACIONES_VER");

        var cut = Render<MainLayout>();

        Assert.Empty(cut.FindAll("[aria-label=\"Documentacion\"]"));
        Assert.Empty(cut.FindAll("[aria-label=\"Soporte\"]"));
    }

    private sealed class EntornoWebAssembly : IWebAssemblyHostEnvironment
    {
        public string Environment { get; set; } = "Development";

        public string BaseAddress { get; set; } = "https://localhost:5001/";
    }
}
