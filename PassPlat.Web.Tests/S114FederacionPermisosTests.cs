using Bunit;
using Microsoft.AspNetCore.Components;
using PassPlat.Web.Tests.Infra;
using ConfProvIdenIndex = PassPlat.Web.Pages.Federacion.ConfProvIden.Index;
using IdenExtIndex = PassPlat.Web.Pages.Federacion.IdenExt.Index;
using ProvIdenIndex = PassPlat.Web.Pages.Federacion.ProvIden.Index;

namespace PassPlat.Web.Tests;

/// <summary>
/// S114 · D3/D4 (P13) — Las páginas de Federación exigen su permiso FED_*_VER (sin él: «Acceso Denegado» y
/// ninguna llamada a su API) y muestran las acciones de escritura solo con FED_CONFPROVIDEN_EDITAR /
/// FED_IDENEXT_DESVINCULAR.
/// </summary>
public sealed class S114FederacionPermisosTests : WebTestBase
{
    private const string Configs = """
    [ { "id": 5, "idTenant": 1, "idProvIden": 1, "clientId": "google-client", "callback": "https://localhost:7275/callback/google", "estado": 1, "activo": true, "provIdenNombre": "Google", "tenantNombre": "Plataforma" } ]
    """;

    private const string Identidades = """
    [ { "id": 9, "idUsuario": 2, "idProvIden": 1, "idTenant": 1, "subExterno": "sub-9", "providerUserName": "ana.ext", "activo": true, "eliminado": false, "provIdenNombre": "Google", "usuarioNombre": null } ]
    """;

    private static string Pagina(string items) => $$"""
    { "items": {{items}}, "totalCount": 1, "pageNumber": 1, "pageSize": 10, "totalPages": 1, "hasPreviousPage": false, "hasNextPage": false }
    """;

    public S114FederacionPermisosTests()
    {
        CargarCatalogos();
        Handler.Responder("/api/confproviden/page", Pagina(Configs));
        Handler.Responder("/api/iden-ext/page", Pagina(Identidades));
        Handler.Responder("/api/providen/catalog", "[]");
    }

    private IRenderedComponent<TestHost> RenderPagina(Type pagina) =>
        Render<TestHost>(p => p.Add(x => x.ChildContent, (RenderFragment)(b =>
        {
            b.OpenComponent(0, pagina);
            b.CloseComponent();
        })));

    [Theory]
    [InlineData(typeof(ProvIdenIndex), "FED_PROVIDEN_VER", "/api/providen")]
    [InlineData(typeof(ConfProvIdenIndex), "FED_CONFPROVIDEN_VER", "/api/confproviden")]
    [InlineData(typeof(IdenExtIndex), "FED_IDENEXT_VER", "/api/iden-ext")]
    public async Task Sin_permiso_VER_muestra_Acceso_Denegado_y_no_llama_a_la_API(Type pagina, string permiso, string api)
    {
        await AutenticarAsync(5, 2, "admin_abarrotes", "USUARIOS_VER");

        var cut = RenderPagina(pagina);

        cut.WaitForAssertion(() => Assert.Contains("Acceso Denegado", cut.Markup));
        Assert.Contains(permiso, cut.Markup);
        Assert.DoesNotContain(Handler.Peticiones, p => p.Contains(api));
    }

    [Theory]
    [InlineData(typeof(ConfProvIdenIndex), "FED_CONFPROVIDEN_VER", "Nueva Configuración", "Editar")]
    [InlineData(typeof(IdenExtIndex), "FED_IDENEXT_VER", "Vincular Identidad", "Desvincular")]
    public async Task Con_VER_sin_permiso_de_escritura_oculta_las_acciones(Type pagina, string permiso, string botonCrear, string accionFila)
    {
        await AutenticarAsync(1, 1, "supervisor", permiso);

        var cut = RenderPagina(pagina);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tbody tr.mud-table-row")));
        Assert.DoesNotContain(botonCrear, cut.Markup);
        Assert.Empty(cut.FindAll($"button[aria-label='{accionFila}']"));
    }
}
