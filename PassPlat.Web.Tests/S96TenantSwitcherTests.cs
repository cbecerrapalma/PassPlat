using System.Text.RegularExpressions;
using Bunit;
using MudBlazor;
using PassPlat.Web.Shared;

namespace PassPlat.Web.Tests.Tests.S96;

/// <summary>
/// S96 · D4 — el selector de tenant carga los tenants al abrirse y su clic no abre el menú de usuario.
/// </summary>
public class S96TenantSwitcherTests : WebTestBase
{
    private const string Ruta = "/api/auth/mis-tenants";

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private int Pedidas() => Handler.Peticiones.Count(p => p == "GET " + Ruta);

    [Fact]
    public async Task AbrirMenu_CargaLosTenants()
    {
        await AutenticarAsync(1, 1, "sistema");
        Handler.Responder(Ruta, """[{"id":1,"codigo":"PLAT","nombre":"Plataforma Central"},{"id":2,"codigo":"ABARROTES","nombre":"Abarrotes del Sur"}]""");

        var cut = Render(b =>
        {
            b.OpenComponent<MudPopoverProvider>(0);
            b.CloseComponent();
            b.OpenComponent<TenantSwitcher>(1);
            b.CloseComponent();
        });
        Assert.Equal(0, Pedidas());

        cut.Find(".tenant-switcher-chip").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Plataforma Central", cut.Markup);
            Assert.Contains("Abarrotes del Sur", cut.Markup);
        });
        Assert.Equal(1, Pedidas());
    }

    [Fact]
    public void MainLayout_SelectorSinPropagacionAlMenuDeUsuario()
    {
        var layout = File.ReadAllText(Path.Combine(RaizDelRepo(), "PassPlat.Web", "Layout", "MainLayout.razor"));

        Assert.Matches(@"<\w+[^>]*@onclick:stopPropagation=""true""[^>]*>\s*<TenantSwitcher\s*/>", layout);
    }
}
