using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PassPlat.Web.Tests.Infra;
using ConfProvIdenDetail = PassPlat.Web.Pages.Federacion.ConfProvIden.ConfProvIdenDetail;
using ConfProvIdenIndex = PassPlat.Web.Pages.Federacion.ConfProvIden.Index;
using IdenExtDetail = PassPlat.Web.Pages.Federacion.IdenExt.IdenExtDetail;
using IdenExtIndex = PassPlat.Web.Pages.Federacion.IdenExt.Index;

namespace PassPlat.Web.Tests;

/// <summary>
/// S69.7 · K3 — Federación sin inspector: un clic en la fila navega a
/// <c>/federacion/confproviden/{idTenant}/{id}</c> o <c>/federacion/iden-ext/{idUsuario}/{id}</c>,
/// los iconos de acción no navegan y las páginas de detalle muestran lo que tenía el panel lateral.
/// </summary>
public class S697FederacionDetalleTests : WebTestBase
{
    private const string Configs = """
    [
      { "id": 5, "idTenant": 1, "idProvIden": 1, "clientId": "google-client", "callback": "https://localhost:7275/callback/google", "estado": 1, "activo": true, "provIdenNombre": "Google", "tenantNombre": "Plataforma" },
      { "id": 6, "idTenant": 1, "idProvIden": 2, "clientId": "microsoft-client", "callback": "https://localhost:7275/callback/microsoft", "estado": 1, "activo": true, "provIdenNombre": "Microsoft", "tenantNombre": "Plataforma" }
    ]
    """;

    private const string Identidades = """
    [
      { "id": 9, "idUsuario": 2, "idProvIden": 1, "idTenant": 1, "subExterno": "sub-9", "providerUserName": "ana.ext", "activo": true, "eliminado": false, "provIdenNombre": "Google", "usuarioNombre": null }
    ]
    """;

    private static string Pagina(string items) => $$"""
    { "items": {{items}}, "totalCount": 2, "pageNumber": 1, "pageSize": 10, "totalPages": 1, "hasPreviousPage": false, "hasNextPage": false }
    """;

    public S697FederacionDetalleTests()
    {
        CargarCatalogos();
        Handler.Responder("/api/confproviden/page", Pagina(Configs));
        Handler.Responder("/api/confproviden/tenant/1", Configs);
        Handler.Responder("/api/iden-ext/page", Pagina(Identidades));
        Handler.Responder("/api/iden-ext/usuario/2", Identidades);
    }

    private NavigationManager Navegacion => Services.GetRequiredService<NavigationManager>();

    private IRenderedComponent<TestHost> RenderEnHost<TComponent>()
        where TComponent : IComponent =>
        Render<TestHost>(p => p.Add(x => x.ChildContent, (RenderFragment)(b =>
        {
            b.OpenComponent<TComponent>(0);
            b.CloseComponent();
        })));

    private IRenderedComponent<TestHost> RenderConfProvIdenDetail(int idTenant, int id) =>
        Render<TestHost>(p => p.Add(x => x.ChildContent, (RenderFragment)(b =>
        {
            b.OpenComponent<ConfProvIdenDetail>(0);
            b.AddAttribute(1, nameof(ConfProvIdenDetail.IdTenant), idTenant);
            b.AddAttribute(2, nameof(ConfProvIdenDetail.Id), id);
            b.CloseComponent();
        })));

    private IRenderedComponent<TestHost> RenderIdenExtDetail(int idUsuario, long id) =>
        Render<TestHost>(p => p.Add(x => x.ChildContent, (RenderFragment)(b =>
        {
            b.OpenComponent<IdenExtDetail>(0);
            b.AddAttribute(1, nameof(IdenExtDetail.IdUsuario), idUsuario);
            b.AddAttribute(2, nameof(IdenExtDetail.Id), id);
            b.CloseComponent();
        })));

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    [Fact]
    public async Task ConfProvIden_clic_en_fila_navega_al_detalle()
    {
        await AutenticarAsync();
        var cut = RenderEnHost<ConfProvIdenIndex>();
        cut.WaitForAssertion(() => Assert.Contains("Google", cut.Markup));

        cut.FindAll("tbody tr.mud-table-row").First(f => f.TextContent.Contains("Google")).Click();

        cut.WaitForAssertion(() => Assert.EndsWith("/federacion/confproviden/1/5", Navegacion.Uri));
    }

    [Fact]
    public async Task ConfProvIden_clic_en_accion_no_navega()
    {
        await AutenticarAsync();
        var cut = RenderEnHost<ConfProvIdenIndex>();
        cut.WaitForAssertion(() => Assert.Contains("Google", cut.Markup));

        cut.FindAll("tbody tr.mud-table-row").First(f => f.TextContent.Contains("Google"))
            .QuerySelector("button[aria-label='Editar']")!.Click();

        Assert.DoesNotContain("/federacion/confproviden/1/", Navegacion.Uri);
    }

    [Fact]
    public async Task IdenExt_clic_en_fila_navega_al_detalle()
    {
        await AutenticarAsync();
        var cut = RenderEnHost<IdenExtIndex>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tbody tr.mud-table-row")));

        cut.FindAll("tbody tr.mud-table-row")[0].Click();

        cut.WaitForAssertion(() => Assert.EndsWith("/federacion/iden-ext/2/9", Navegacion.Uri));
    }

    [Fact]
    public async Task ConfProvIdenDetail_muestra_proveedor_y_pestanas()
    {
        await AutenticarAsync();
        var cut = RenderConfProvIdenDetail(1, 5);

        cut.WaitForAssertion(() => Assert.Contains("Google", cut.Markup));
        foreach (var pestana in new[] { "Información", "Opciones" })
            Assert.Contains(pestana, cut.Markup);
    }

    [Fact]
    public async Task ConfProvIdenDetail_id_inexistente_muestra_no_encontrado()
    {
        await AutenticarAsync();
        var cut = RenderConfProvIdenDetail(1, 999);

        cut.WaitForAssertion(() => Assert.Contains("Configuración no encontrada", cut.Markup));
    }

    [Fact]
    public async Task IdenExtDetail_muestra_usuario_y_pestanas()
    {
        await AutenticarAsync();
        var cut = RenderIdenExtDetail(2, 9);

        cut.WaitForAssertion(() => Assert.Contains("ana.ext", cut.Markup));
        foreach (var texto in new[] { "admin_plataforma", "Información", "Tokens" })
            Assert.Contains(texto, cut.Markup);
    }

    [Fact]
    public async Task IdenExtDetail_id_inexistente_muestra_no_encontrado()
    {
        await AutenticarAsync();
        var cut = RenderIdenExtDetail(2, 999);

        cut.WaitForAssertion(() => Assert.Contains("Identidad no encontrada", cut.Markup));
    }

    [Fact]
    public void Federacion_sin_inspector()
    {
        var raiz = RaizDelRepo();
        var archivos = new[]
        {
            Path.Combine("PassPlat.Web", "Pages", "Federacion", "ConfProvIden", "Index.razor"),
            Path.Combine("PassPlat.Web", "Pages", "Federacion", "IdenExt", "Index.razor")
        };
        var pendientes = new List<string>();

        foreach (var archivo in archivos)
        {
            var texto = File.ReadAllText(Path.Combine(raiz, archivo));
            foreach (var patron in new[] { "iam-inspector", "insp-", "_inspectorTab", "_selectedItem", "SelectedItemChanged" })
            {
                if (texto.Contains(patron, StringComparison.Ordinal))
                    pendientes.Add($"{archivo.Replace('\\', '/')}: {patron}");
            }
        }

        Assert.Empty(pendientes);
    }
}
