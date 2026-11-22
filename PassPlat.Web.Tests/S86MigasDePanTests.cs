using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Web.Services;
using PassPlat.Web.Shared;

namespace PassPlat.Web.Tests;

/// <summary>
/// S86 · D1–D5 — migas de pan desde el catálogo de módulos (<c>api/modulos/menu</c>): rastro de la raíz al módulo,
/// fichas con su lista y su registro, respaldo sin catálogo y candado de las páginas que ya no las arman a mano.
/// </summary>
public class S86MigasDePanTests : WebTestBase
{
    private const string RutaMenu = "/api/modulos/menu";
    private static readonly CatalogoNavegacion Catalogo = new(S83MenuDePrueba.Arbol());

    private static List<string> Nombres(IReadOnlyList<ModuloMenuDto> rastro) => rastro.Select(m => m.Nombre).ToList();

    private static List<string> Tramos(IRenderedComponent<MigasDePan> cut) =>
        cut.FindAll("li.mud-breadcrumb-item").Select(li => li.TextContent.Trim()).ToList();

    // MudBlazor dibuja un tramo sin enlace con href="#".
    private static List<string> Enlaces(IRenderedComponent<MigasDePan> cut) =>
        cut.FindAll("li.mud-breadcrumb-item a")
            .Select(a => a.GetAttribute("href") ?? "#")
            .Where(h => h != "#")
            .ToList();

    private async Task<IRenderedComponent<MigasDePan>> RenderEnAsync(string ruta, string? padre = null, string? actual = null)
    {
        await AutenticarAsync(permisos: "USUARIOS_VER");
        Handler.Responder(RutaMenu, S83MenuDePrueba.Json());
        Services.GetRequiredService<NavigationManager>().NavigateTo(ruta);
        return Render<MigasDePan>(p => p.Add(x => x.Padre, padre).Add(x => x.Actual, actual));
    }

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    [Fact]
    public void RastroDe_modulo_de_segundo_nivel_va_de_la_raiz_al_modulo()
    {
        Assert.Equal(["IAM", "Usuarios", "Panel de administración"], Nombres(Catalogo.RastroDe("/admin/panel")));
        Assert.Equal(["IAM", "Roles"], Nombres(Catalogo.RastroDe("admin/roles")));
        Assert.Equal(["Panel principal"], Nombres(Catalogo.RastroDe("/")));
    }

    [Fact]
    public void RastroDe_ignora_la_consulta_y_resuelve_los_alias()
    {
        Assert.Equal(["IAM", "Usuarios"], Nombres(Catalogo.RastroDe("/usuarios?tenant=2#lista")));
        Assert.Equal(["Infraestructura", "Dispositivos confiables"], Nombres(Catalogo.RastroDe("/dispositivos-confiables")));
    }

    [Theory]
    [InlineData("/usuario/5")]
    [InlineData("/sin-modulo")]
    [InlineData("")]
    [InlineData(null)]
    public void RastroDe_sin_modulo_devuelve_vacio(string? ruta)
    {
        Assert.Empty(Catalogo.RastroDe(ruta));
        Assert.Empty(CatalogoNavegacion.Vacio.RastroDe("/usuarios"));
    }

    [Fact]
    public async Task Lista_muestra_raiz_grupo_y_pagina_sin_enlace_final()
    {
        var cut = await RenderEnAsync("/admin/roles");

        cut.WaitForAssertion(() => Assert.Equal(["Panel principal", "IAM", "Roles"], Tramos(cut)));
        Assert.Equal(["/"], Enlaces(cut));
    }

    [Fact]
    public async Task Modulo_intermedio_es_enlace()
    {
        var cut = await RenderEnAsync("/admin/panel");

        cut.WaitForAssertion(() => Assert.Equal(["Panel principal", "IAM", "Usuarios", "Panel de administración"], Tramos(cut)));
        Assert.Equal(["/", "/usuarios"], Enlaces(cut));
    }

    [Fact]
    public async Task Ficha_enlaza_su_lista_con_la_consulta_y_termina_en_el_registro()
    {
        var cut = await RenderEnAsync("/usuario/7", padre: "/usuarios?tenant=2", actual: "ana");

        cut.WaitForAssertion(() => Assert.Equal(["Panel principal", "IAM", "Usuarios", "ana"], Tramos(cut)));
        Assert.Equal(["/", "/usuarios?tenant=2"], Enlaces(cut));
    }

    [Fact]
    public async Task Sin_catalogo_quedan_el_panel_principal_y_el_registro()
    {
        await AutenticarAsync(permisos: "USUARIOS_VER");

        var cut = Render<MigasDePan>(p => p.Add(x => x.Padre, "/usuarios").Add(x => x.Actual, "ana"));

        cut.WaitForAssertion(() => Assert.True(Menu.Fallo));
        Assert.Equal(["Panel principal", "ana"], Tramos(cut));
        Assert.Equal(["/"], Enlaces(cut));
    }

    [Fact]
    public void Las_paginas_usan_MigasDePan_y_no_arman_migas_a_mano()
    {
        var paginas = Path.Combine(RaizDelRepo(), "PassPlat.Web", "Pages");
        var archivos = Directory.GetFiles(paginas, "*.razor", SearchOption.AllDirectories)
            .Select(p => (Archivo: Path.GetRelativePath(paginas, p).Replace('\\', '/'), Texto: File.ReadAllText(p)))
            .ToList();

        // S102 · D3 — RolesPermisos se reescribió con <MigasDePan>: ya no queda ninguna página con migas a mano.
        var aMano = archivos.Where(a => a.Texto.Contains("new BreadcrumbItem(", StringComparison.Ordinal)).Select(a => a.Archivo).ToList();
        Assert.Empty(aMano);
        var conMigas = archivos.Count(a => a.Texto.Contains("<MigasDePan", StringComparison.Ordinal));
        Assert.True(conMigas >= 40, $"solo {conMigas} páginas usan <MigasDePan>");
    }
}
