using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PassPlat.Web.Layout;
using PassPlat.Web.Tests.Infra;

namespace PassPlat.Web.Tests;

/// <summary>
/// S89 · D1-D4 — higiene del layout (P37): el reloj vive en su propio componente, navegar no
/// pinta una barra de carga fija, el ancho y el modo oscuro se leen sin eval y el CSV del
/// panel se descarga con el servicio de CBP.Blazor.
/// </summary>
public class S89LayoutTests : WebTestBase
{
    private static readonly string[] Saludos = ["Buenos días", "Buenas tardes", "Buenas noches"];

    public S89LayoutTests()
    {
        Services.AddSingleton<IWebAssemblyHostEnvironment>(new EntornoWebAssembly());
        Services.AddSingleton<IBrowserViewportService>(new ViewportFalso());
        Handler.Responder("/api/notificaciones", "[]");
        Handler.Responder("/api/notificaciones/no-leidas/7", "[]");
        Handler.Responder("/api/tenants", "[]");
        Handler.Responder("/api/apps", "[]");
        Handler.Responder("/api/modulos/menu", S83MenuDePrueba.Json());
    }

    [Fact]
    public async Task El_reloj_de_la_barra_es_un_componente_propio()
    {
        await AutenticarAsync(7, 0, "sistema", "NOTIFICACIONES_VER");

        var cut = Render<MainLayout>();

        Assert.Single(cut.FindComponents<RelojBarra>());
    }

    [Fact]
    public void El_reloj_muestra_la_hora_y_el_saludo()
    {
        var cut = Render<RelojBarra>();

        Assert.Matches(@"^\d{2}\D\d{2}\D\d{2}$", cut.Find(".appbar-time").TextContent.Trim());
        Assert.Contains(cut.Find(".appbar-greeting").TextContent.Trim(), Saludos);
    }

    [Fact]
    public async Task Navegar_no_pinta_una_barra_de_carga_fija()
    {
        await AutenticarAsync(7, 0, "sistema", "NOTIFICACIONES_VER");
        var cut = Render<MainLayout>();

        Services.GetRequiredService<NavigationManager>().NavigateTo("/usuarios");

        Assert.Empty(cut.FindAll(".page-loader"));
    }

    [Fact]
    public async Task Al_navegar_se_comprueba_el_ancho_con_el_modulo_del_layout()
    {
        var modulo = JSInterop.SetupModule("./js/layout.js");
        modulo.Setup<bool>("esMovil").SetResult(false);
        await AutenticarAsync(7, 0, "sistema", "NOTIFICACIONES_VER");
        var cut = Render<MainLayout>();
        var antes = modulo.Invocations.Count(i => i.Identifier == "esMovil");

        Services.GetRequiredService<NavigationManager>().NavigateTo("/usuarios");

        cut.WaitForAssertion(() => Assert.True(modulo.Invocations.Count(i => i.Identifier == "esMovil") > antes));
    }

    [Fact]
    public async Task El_modo_oscuro_se_lee_de_localStorage_sin_eval()
    {
        await AutenticarAsync(7, 0, "sistema", "NOTIFICACIONES_VER");

        Render<MainLayout>();

        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "localStorage.getItem"
            && i.Arguments.Count == 1
            && i.Arguments[0] is "dark_mode");
        Assert.DoesNotContain(JSInterop.Invocations, i => i.Identifier == "eval");
    }

    [Fact]
    public void Ningun_componente_de_la_Web_llama_eval()
    {
        var raiz = RaizDelRepo();
        var web = Path.Combine(raiz, "PassPlat.Web");
        var conEval = Directory.EnumerateFiles(web, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".razor", StringComparison.Ordinal) || f.EndsWith(".cs", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains("\"eval\"", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(raiz, f))
            .ToList();

        Assert.Empty(conEval);
    }

    [Fact]
    public void El_csv_del_panel_se_descarga_con_el_servicio_de_CBP()
    {
        var raiz = RaizDelRepo();
        var panel = File.ReadAllText(Path.Combine(raiz, "PassPlat.Web", "Pages", "Admin", "DashboardEnterprise.razor"));
        var programa = File.ReadAllText(Path.Combine(raiz, "PassPlat.Web", "Program.cs"));

        Assert.Contains("@inject IFileDownloadService Descargas", panel, StringComparison.Ordinal);
        Assert.Contains("Descargas.DownloadAsync(\"dashboard-enterprise.csv\", \"text/csv\", bytes)", panel, StringComparison.Ordinal);
        Assert.Contains("builder.Services.AddCbpBlazor();", programa, StringComparison.Ordinal);
    }

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private sealed class EntornoWebAssembly : IWebAssemblyHostEnvironment
    {
        public string Environment { get; set; } = "Development";

        public string BaseAddress { get; set; } = "https://localhost:5001/";
    }
}
