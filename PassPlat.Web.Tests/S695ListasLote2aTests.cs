using Bunit;
using PassPlat.Web.Tests.Infra;
using AccesosIndex = PassPlat.Web.Pages.Accesos.Index;
using DispConfiablesIndex = PassPlat.Web.Pages.DispConfiables.Index;

namespace PassPlat.Web.Tests;

/// <summary>
/// S69.5 · K3 (D1–D4) — candados del diseño único de listas para el lote 2a: las 7 páginas
/// usan el patrón de <c>/apps</c> (<c>iam-page</c> + <c>page-header</c> + <c>table-panel</c>
/// + <c>table-wrap</c>), sin <c>MudContainer</c> ni título duplicado, y ninguna de las 15
/// listas de los lotes 1 y 2a conserva la barra suelta antes de <c>aria-label</c> (P10).
/// </summary>
public class S695ListasLote2aTests : WebTestBase
{
    private static readonly string[] PaginasLote1 =
    [
        "PassPlat.Web/Pages/Tenants/Index.razor",
        "PassPlat.Web/Pages/Grupos/Index.razor",
        "PassPlat.Web/Pages/Sesiones/Index.razor",
        "PassPlat.Web/Pages/Bloqueos/Index.razor",
        "PassPlat.Web/Pages/MFA/Index.razor",
        "PassPlat.Web/Pages/Notificaciones/Index.razor",
        "PassPlat.Web/Pages/DominiosTenant/Index.razor",
        "PassPlat.Web/Pages/AuditoriaPwd/Index.razor",
    ];

    private static readonly string[] PaginasLote2a =
    [
        "PassPlat.Web/Pages/HistorialPwd/Index.razor",
        "PassPlat.Web/Pages/IntentosAcceso/Index.razor",
        "PassPlat.Web/Pages/PoliticasPwd/Index.razor",
        "PassPlat.Web/Pages/DispConfiables/Index.razor",
        "PassPlat.Web/Pages/Dispositivos/Index.razor",
        "PassPlat.Web/Pages/ConfigTenants/Index.razor",
        "PassPlat.Web/Pages/Accesos/Index.razor",
    ];

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static string LeerPagina(string relativa) =>
        File.ReadAllText(Path.Combine(RaizDelRepo(), relativa.Replace('/', Path.DirectorySeparatorChar)));

    [Fact]
    public void Lote2a_usa_iam_page_page_header_y_table_panel()
    {
        var faltantes = new List<string>();
        foreach (var pagina in PaginasLote2a)
        {
            var texto = LeerPagina(pagina);
            foreach (var marca in new[] { "class=\"iam-page\"", "class=\"page-header\"", "class=\"page-title\"", "class=\"table-panel\"", "class=\"table-wrap\"" })
            {
                if (!texto.Contains(marca, StringComparison.Ordinal))
                    faltantes.Add($"{pagina} -> {marca}");
            }
        }

        Assert.Empty(faltantes);
    }

    [Fact]
    public void Lote2a_sin_MudContainer_ni_titulo_h5_ni_CrudToolbar_Title()
    {
        var sobrantes = new List<string>();
        foreach (var pagina in PaginasLote2a)
        {
            var texto = LeerPagina(pagina);
            if (texto.Contains("<MudContainer", StringComparison.Ordinal))
                sobrantes.Add($"{pagina} -> <MudContainer");
            if (texto.Contains("Typo=\"Typo.h5\"", StringComparison.Ordinal))
                sobrantes.Add($"{pagina} -> Typo.h5");
            if (texto.Contains("CrudToolbar Title=", StringComparison.Ordinal))
                sobrantes.Add($"{pagina} -> CrudToolbar Title");
        }

        Assert.Empty(sobrantes);
    }

    [Fact]
    public void Listas_lote1_y_lote2a_sin_barra_suelta_antes_de_aria_label()
    {
        var conBarra = PaginasLote1.Concat(PaginasLote2a)
            .Where(pagina => LeerPagina(pagina).Contains("\" / aria-label", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(conBarra);
    }

    [Fact]
    public async Task Accesos_un_solo_titulo_y_Asignar_Acceso_en_page_actions()
    {
        CargarCatalogos();
        Handler.Responder("/api/accesos/usuario/1", Datos.AccesosJson((1, 1, 1, "Administrador")));
        await AutenticarAsync();
        var cut = Render<AccesosIndex>();

        cut.WaitForAssertion(() => Assert.Contains("Inventa POS", cut.Markup));
        Assert.Equal("Accesos", cut.Find("h1.page-title").TextContent.Trim());
        Assert.DoesNotContain(cut.FindAll(".table-panel .mud-typography-h6"), e => e.TextContent.Trim() == "Accesos");
        Assert.Contains("Asignar Acceso", cut.Find(".page-actions").TextContent);
    }

    [Fact]
    public async Task DispConfiables_filtro_dentro_de_table_panel()
    {
        Handler.Responder("/api/usuarios", "[]");
        Handler.Responder("/api/DispConfiables/usuario/1", "[]");
        await AutenticarAsync(permisos: "USUARIOS_VERDISP");
        var cut = Render<DispConfiablesIndex>();

        cut.WaitForAssertion(() => Assert.Contains("No hay registros", cut.Markup));
        var barra = cut.Find(".table-panel .table-toolbar");
        Assert.Contains("Usuario", barra.TextContent);
        Assert.Equal("Dispositivos Confiables", cut.Find("h1.page-title").TextContent.Trim());
    }
}
