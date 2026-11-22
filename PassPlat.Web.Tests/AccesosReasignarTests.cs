using Bunit;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using PassPlat.Web.Pages.Accesos;
using PassPlat.Web.Tests.Infra;

namespace PassPlat.Web.Tests;

/// <summary>
/// S54.1 · K3 (D2) — «Reasignar» abre el dialogo de asignacion con el usuario y la app
/// del acceso seleccionado ya precargados (desde S69.3, en la página de detalle).
/// </summary>
public class AccesosReasignarTests : WebTestBase
{
    public AccesosReasignarTests()
    {
        CargarCatalogos();
        Handler.Responder("/api/accesos/usuario/1", Datos.AccesosJson((1, 12, 1, "Administrador")));
    }

    [Fact]
    public async Task Reasignar_abre_el_dialogo_con_usuario_y_app_precargados()
    {
        // S113 · D4 — el diálogo pide api/tenants y api/apps solo con TENANTS_VER/APPS_VER (sesión de plataforma)
        await AutenticarAsync(permisos: ["ACCESOS_VER", "ACCESOS_ASIGNAR", "ACCESOS_REVOCAR", "USUARIOS_VER", "TENANTS_VER", "APPS_VER"]);

        // S69.3 · D6 — el acceso se abre en su página de detalle (/acceso/1/1), sin inspector.
        var cut = Render<TestHost>(p => p.Add(x => x.ChildContent, (RenderFragment)(b =>
        {
            b.OpenComponent<AccesoDetail>(0);
            b.AddAttribute(1, nameof(AccesoDetail.IdUsuario), 1);
            b.AddAttribute(2, nameof(AccesoDetail.Id), 1);
            b.CloseComponent();
        })));

        cut.WaitForAssertion(() => Assert.Contains("Inventa POS", cut.Markup));

        await cut.InvokeAsync(() => cut.FindAll(".page-header button")
            .First(b => b.TextContent.Contains("Reasignar"))
            .Click());

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=acceso-asignar]")));

        var usuario = ValorDe(cut, "Usuario");
        var app = ValorDe(cut, "App");
        Assert.Contains("sistema", usuario);
        Assert.Contains("Inventa POS (INVENTA)", app);
    }

    private static string ValorDe(IRenderedComponent<TestHost> cut, string etiqueta)
    {
        var label = cut.FindAll("label").First(l => l.TextContent.Trim().StartsWith(etiqueta, StringComparison.OrdinalIgnoreCase));
        var id = label.GetAttribute("for");
        return cut.Find($"input#{id}").GetAttribute("value") ?? "";
    }
}
