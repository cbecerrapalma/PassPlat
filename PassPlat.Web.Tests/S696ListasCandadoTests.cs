using Bunit;
using ConfigAppIndex = PassPlat.Web.Pages.ConfigApp.Index;
using EmailTemplatesIndex = PassPlat.Web.Pages.EmailTemplates.Index;

namespace PassPlat.Web.Tests;

/// <summary>
/// S69.6 · K3 (D1–D7) — candado del diseño único de listas: recorre todas las páginas de
/// <c>PassPlat.Web/Pages</c> con <c>@page</c> y <c>MudTable</c>/<c>MudDataGrid</c> (sin diálogos,
/// detalles ni componentes) y exige el patrón de <c>/apps</c> (<c>iam-page</c> + <c>page-header</c>
/// + <c>table-panel</c> + <c>table-wrap</c>), sin <c>MudContainer</c>, título h5, <c>CrudToolbar Title=</c>
/// ni barra suelta antes de <c>aria-label</c>. Una lista nueva fuera del patrón rompe la prueba.
/// </summary>
public class S696ListasCandadoTests : WebTestBase
{
    private static readonly string[] Excluidas =
    [
        // Dashboards: no son listas (P9 los excluye).
        "PassPlat.Web/Pages/Dashboard.razor",
        "PassPlat.Web/Pages/Admin/DashboardEnterprise.razor",
    ];

    private const string ConfigAppVacia = """
    {
      "items": [],
      "totalCount": 0,
      "pageNumber": 1,
      "pageSize": 10,
      "totalPages": 0,
      "hasPreviousPage": false,
      "hasNextPage": false
    }
    """;

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static string LeerPagina(string relativa) =>
        File.ReadAllText(Path.Combine(RaizDelRepo(), relativa.Replace('/', Path.DirectorySeparatorChar)));

    private static List<string> Listas()
    {
        var raiz = RaizDelRepo();
        var paginas = Path.Combine(raiz, "PassPlat.Web", "Pages");
        return Directory.EnumerateFiles(paginas, "*.razor", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(raiz, f).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(r => !r.Contains("/Components/", StringComparison.Ordinal))
            .Where(r =>
            {
                var nombre = Path.GetFileNameWithoutExtension(r);
                return !nombre.Contains("Dialog", StringComparison.Ordinal) && !nombre.Contains("Detail", StringComparison.Ordinal);
            })
            .Where(r => !Excluidas.Contains(r, StringComparer.OrdinalIgnoreCase))
            .Where(r =>
            {
                var texto = LeerPagina(r);
                return texto.Contains("@page", StringComparison.Ordinal)
                    && (texto.Contains("<MudTable", StringComparison.Ordinal) || texto.Contains("<MudDataGrid", StringComparison.Ordinal));
            })
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();
    }

    [Fact]
    public void Todas_las_listas_usan_iam_page_page_header_y_table_panel()
    {
        var listas = Listas();
        Assert.True(listas.Count >= 27, $"Se esperaban al menos 27 listas y se encontraron {listas.Count}");

        var faltantes = new List<string>();
        foreach (var pagina in listas)
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
    public void Todas_las_listas_sin_MudContainer_ni_titulo_h5_ni_CrudToolbar_Title()
    {
        var sobrantes = new List<string>();
        foreach (var pagina in Listas())
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
    public void Todas_las_listas_sin_barra_suelta_antes_de_aria_label()
    {
        var conBarra = Listas()
            .Where(pagina => LeerPagina(pagina).Contains("\" / aria-label", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(conBarra);
    }

    [Fact]
    public async Task EmailTemplates_un_solo_titulo_y_Nueva_Plantilla_en_page_actions()
    {
        Handler.Responder("/api/emailtemplates", "[]");
        Handler.Responder("/api/emailtemplates/tenant/1", "[]");
        await AutenticarAsync(permisos: "EMAIL_TEMPLATES_VER");
        var cut = Render<EmailTemplatesIndex>();

        cut.WaitForAssertion(() => Assert.Contains("No hay plantillas registradas", cut.Markup));
        Assert.Equal("Plantillas de Email", cut.Find("h1.page-title").TextContent.Trim());
        Assert.Contains("Nueva Plantilla", cut.Find(".page-actions").TextContent);
        Assert.Contains("Categoría", cut.Find(".table-panel .table-toolbar").TextContent);
    }

    [Fact]
    public async Task ConfigApp_busqueda_dentro_de_table_panel()
    {
        Handler.Responder("/api/configapp/page", ConfigAppVacia);
        await AutenticarAsync(permisos: "CONFIGAPP_VER");
        var cut = Render<ConfigAppIndex>();

        cut.WaitForAssertion(() => Assert.Contains("No hay registros", cut.Markup));
        Assert.NotNull(cut.Find(".table-panel .table-toolbar"));
        Assert.Equal("Config App", cut.Find("h1.page-title").TextContent.Trim());
    }
}
