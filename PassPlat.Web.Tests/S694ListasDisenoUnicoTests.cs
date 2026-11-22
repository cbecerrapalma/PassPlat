using System.Text.RegularExpressions;
using Bunit;
using PassPlat.Web.Tests.Infra;
using BloqueosIndex = PassPlat.Web.Pages.Bloqueos.Index;
using GruposIndex = PassPlat.Web.Pages.Grupos.Index;
using TenantsIndex = PassPlat.Web.Pages.Tenants.Index;

namespace PassPlat.Web.Tests;

/// <summary>
/// S69.4 · K4 (D1–D6) — candados del diseño único de listas: las 8 páginas del lote 1
/// usan el patrón de <c>/apps</c> (<c>iam-page</c> + <c>page-header</c> + <c>table-panel</c>
/// + <c>table-wrap</c>), sin <c>MudContainer</c> ni título duplicado, y los filtros viven
/// dentro del <c>table-panel</c>.
/// </summary>
public class S694ListasDisenoUnicoTests : WebTestBase
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

    private const string TenantsPagina = """
    {
      "items": [
        { "id": 2, "codigo": "ABARROTES", "nombre": "Abarrotes del Sur", "activo": true, "esSistema": false, "fecCrea": "2026-01-01T00:00:00Z" }
      ],
      "totalCount": 1,
      "pageNumber": 1,
      "pageSize": 10,
      "totalPages": 1,
      "hasPreviousPage": false,
      "hasNextPage": false
    }
    """;

    private const string Usuarios = """
    [
      { "id": 1, "idTenant": 1, "nomUsuario": "sistema", "email": "sistema@passplat.app", "nombre": "Usuario", "apellido": "Sistema", "eliminado": false, "estadoNombre": "Activo" }
    ]
    """;

    public S694ListasDisenoUnicoTests()
    {
        Handler.Responder("/api/tenants/page", TenantsPagina);
        Handler.Responder("/api/tenants/count", "1");
        Handler.Responder("/api/tenants/activos/count", "1");
        Handler.Responder("/api/dominiosTenant/count", "1");
        Handler.Responder("/api/grupos/stats", "{}");
        Handler.Responder("/api/grupos", "[]");
        Handler.Responder("/api/usuarios", Usuarios);
        Handler.Responder("/api/bloqueos/usuario/1", "[]");
    }

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static string LeerPagina(string relativa) =>
        File.ReadAllText(Path.Combine(RaizDelRepo(), relativa.Replace('/', Path.DirectorySeparatorChar)));

    private static int Contar(string texto, string buscado) => Regex.Matches(texto, Regex.Escape(buscado)).Count;

    [Fact]
    public void Lote1_usa_iam_page_page_header_y_table_panel()
    {
        var faltantes = new List<string>();
        foreach (var pagina in PaginasLote1)
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
    public void Lote1_sin_MudContainer_ni_titulo_h5_ni_CrudToolbar_Title()
    {
        var sobrantes = new List<string>();
        foreach (var pagina in PaginasLote1)
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
    public void Paginas_iam_page_no_anidan_MudContainer()
    {
        var conContenedor = new List<string>();
        foreach (var pagina in PaginasLote1)
        {
            var texto = LeerPagina(pagina);
            if (texto.Contains("class=\"iam-page\"", StringComparison.Ordinal)
                && texto.Contains("<MudContainer", StringComparison.Ordinal))
                conContenedor.Add(pagina);
        }

        Assert.Empty(conContenedor);
    }

    [Fact]
    public async Task Tenants_muestra_h1_y_un_solo_boton_Nuevo_Tenant()
    {
        await AutenticarAsync(permisos: "TENANTS_VER");
        var cut = Render<TenantsIndex>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tbody tr.mud-table-row")));
        Assert.Equal("Tenants", cut.Find("h1.page-title").TextContent.Trim());
        Assert.Equal(1, Contar(cut.Markup, "Nuevo Tenant"));
    }

    [Fact]
    public async Task Grupos_muestra_un_solo_boton_Nuevo_Grupo()
    {
        await AutenticarAsync(permisos: "GRUPOS_VER");
        var cut = Render<GruposIndex>();

        cut.WaitForAssertion(() => Assert.Contains("Sin grupos configurados", cut.Markup));
        Assert.Equal(1, Contar(cut.Markup, "Nuevo Grupo"));
        Assert.DoesNotContain(cut.FindAll(".mud-typography-h6"), e => e.TextContent.Trim() == "Grupos");
    }

    [Fact]
    public async Task Bloqueos_filtro_dentro_de_table_panel()
    {
        await AutenticarAsync(permisos: "BLOQUEOS_VER");
        var cut = Render<BloqueosIndex>();

        cut.WaitForAssertion(() => Assert.Contains("No hay registros", cut.Markup));
        var barra = cut.Find(".table-panel .table-toolbar");
        Assert.Contains("Usuario", barra.TextContent);
        Assert.DoesNotContain("Tenant", cut.Markup);
    }

    [Fact]
    public void AuditoriaPwd_sin_barra_suelta_en_boton()
    {
        var texto = LeerPagina("PassPlat.Web/Pages/AuditoriaPwd/Index.razor");

        Assert.DoesNotContain("\" / aria-label", texto);
    }
}
