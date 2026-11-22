using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PassPlat.Web.Layout;
using PassPlat.Web.Tests.Infra;

namespace PassPlat.Web.Tests;

/// <summary>
/// S83 · D7/D8 — la barra toma el título del catálogo de la app y el atajo Ctrl+K abre el buscador.
/// </summary>
public class S83BarraTests : WebTestBase
{
    public S83BarraTests()
    {
        Services.AddSingleton<IWebAssemblyHostEnvironment>(new EntornoWebAssembly());
        Services.AddSingleton<IBrowserViewportService>(new ViewportFalso());
        Handler.Responder("/api/notificaciones", "[]");
        Handler.Responder("/api/tenants", "[]");
        Handler.Responder("/api/apps", "[]");
        Handler.Responder("/api/modulos/menu", S83MenuDePrueba.Json());
    }

    [Fact]
    public async Task El_titulo_de_la_barra_sale_del_catalogo()
    {
        await AutenticarAsync(permisos: "AUDITORIA_VER");
        Services.GetRequiredService<NavigationManager>().NavigateTo("/auditoria");

        var cut = Render<MainLayout>();

        cut.WaitForAssertion(() => Assert.Equal("Eventos de auditoría", cut.Find(".appbar-title").TextContent.Trim()));
        Assert.Equal("Eventos de seguridad del sistema", cut.Find(".appbar-subtitle").TextContent.Trim());
        Assert.DoesNotContain("auditoria", cut.Find(".appbar-title").TextContent);
    }

    [Fact]
    public async Task AbrirBuscador_abre_el_buscador()
    {
        await AutenticarAsync(permisos: "AUDITORIA_VER");
        var cut = Render<MainLayout>();
        Assert.Empty(cut.FindAll(".search-overlay"));

        await cut.InvokeAsync(cut.Instance.AbrirBuscador);
        await cut.InvokeAsync(cut.Instance.AbrirBuscador);

        Assert.Single(cut.FindAll(".search-overlay"));
        cut.Find(".search-overlay-input").Input("auditoria");
        cut.WaitForAssertion(() => Assert.Contains("Eventos de auditoría", cut.Find(".search-results").TextContent));
    }

    private sealed class EntornoWebAssembly : IWebAssemblyHostEnvironment
    {
        public string Environment { get; set; } = "Development";

        public string BaseAddress { get; set; } = "https://localhost:5001/";
    }
}
