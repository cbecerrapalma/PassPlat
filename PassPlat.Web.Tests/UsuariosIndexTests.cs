using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PassPlat.Web.Tests.Infra;
using UsuariosIndex = PassPlat.Web.Pages.Usuarios.Index;

namespace PassPlat.Web.Tests;

/// <summary>
/// S55 · K4 (D5) — KPI y tabla de Usuarios usan el mismo conjunto de datos (U5)
/// y el KPI de MFA muestra "—" porque la API no expone ese dato por usuario (U4).
/// </summary>
public class UsuariosIndexTests : WebTestBase
{
    private const string UsuariosPagina = """
    {
      "items": [
        { "id": 1, "idTenant": 1, "nomUsuario": "sistema", "email": "sistema@passplat.app", "nombre": "Usuario", "apellido": "Sistema", "eliminado": false, "estadoNombre": "Activo" },
        { "id": 2, "idTenant": 1, "nomUsuario": "admin_plataforma", "email": "admin@passplat.app", "nombre": "Admin", "apellido": "Plataforma", "eliminado": false, "estadoNombre": "Activo" }
      ],
      "totalCount": 9,
      "pageNumber": 1,
      "pageSize": 10,
      "totalPages": 1,
      "hasPreviousPage": false,
      "hasNextPage": false
    }
    """;

    public UsuariosIndexTests()
    {
        CargarCatalogos();
        Handler.Responder("/api/usuarios/page", UsuariosPagina);
    }

    private async Task<IRenderedComponent<UsuariosIndex>> RenderPaginaAsync()
    {
        await AutenticarAsync();
        return Render<UsuariosIndex>();
    }

    [Fact]
    public async Task El_kpi_de_total_usa_el_mismo_conjunto_que_la_tabla()
    {
        var cut = await RenderPaginaAsync();

        cut.WaitForAssertion(() => Assert.Contains("Usuarios Totales", cut.Markup));
        cut.WaitForAssertion(() => Assert.Contains("sistema", cut.Markup));

        // El total del KPI (9) es el de la consulta paginada, no el de la lista del tenant.
        Assert.Contains(Handler.Peticiones, p => p.Contains("GET /api/usuarios/page"));
        var tarjetas = cut.FindAll(".mud-paper");
        Assert.Contains(tarjetas, t => t.TextContent.Contains("Usuarios Totales") && t.TextContent.Contains("9"));
    }

    [Fact]
    public async Task El_kpi_de_mfa_muestra_guion_porque_la_api_no_lo_expone()
    {
        var cut = await RenderPaginaAsync();

        cut.WaitForAssertion(() => Assert.Contains("Con MFA", cut.Markup));
        var tarjeta = cut.FindAll(".mud-paper").First(t => t.TextContent.Contains("Con MFA"));
        Assert.Contains("—", tarjeta.TextContent);
    }
}
