using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using PassPlat.Web.Tests.Infra;
using AppsIndex = PassPlat.Web.Pages.Apps.Index;
using TenantsIndex = PassPlat.Web.Pages.Tenants.Index;
using UsuariosIndex = PassPlat.Web.Pages.Usuarios.Index;

namespace PassPlat.Web.Tests;

/// <summary>
/// S68.1 · K5 (D6) — candados de las listas sin inspector: Tenants y Usuarios
/// navegan al detalle con un clic en la fila, Apps muestra «Creado» y ninguna de
/// las cinco páginas conserva el panel inspector ni el atributo muerto
/// <c>align-items="AlignItems.Center"</c>.
/// </summary>
public class S681ListasSinInspectorTests : WebTestBase
{
    private const string TenantsPagina = """
    {
      "items": [
        { "id": 2, "codigo": "ABARROTES", "nombre": "Abarrotes del Sur", "activo": true, "esSistema": false, "fecCrea": "2026-01-01T00:00:00Z" },
        { "id": 1, "codigo": "PLATFORM", "nombre": "Plataforma", "activo": true, "esSistema": true, "fecCrea": "2026-01-01T00:00:00Z" }
      ],
      "totalCount": 2,
      "pageNumber": 1,
      "pageSize": 10,
      "totalPages": 1,
      "hasPreviousPage": false,
      "hasNextPage": false
    }
    """;

    private const string AppsPagina = """
    {
      "items": [
        { "id": 12, "codigo": "INVENTA", "nombre": "Inventa POS", "urlBase": "https://localhost:7001", "activa": true, "fecCrea": "2026-01-05T00:00:00Z" }
      ],
      "totalCount": 1,
      "pageNumber": 1,
      "pageSize": 10,
      "totalPages": 1,
      "hasPreviousPage": false,
      "hasNextPage": false
    }
    """;

    private const string AppsLista = """
    [
      { "id": 12, "codigo": "INVENTA", "nombre": "Inventa POS", "urlBase": "https://localhost:7001", "activa": true, "fecCrea": "2026-01-05T00:00:00Z" }
    ]
    """;

    private const string UsuariosPagina = """
    {
      "items": [
        { "id": 1, "idTenant": 1, "nomUsuario": "sistema", "email": "sistema@passplat.app", "nombre": "Usuario", "apellido": "Sistema", "eliminado": false, "estadoNombre": "Activo" },
        { "id": 2, "idTenant": 1, "nomUsuario": "admin_plataforma", "email": "admin@passplat.app", "nombre": "Admin", "apellido": "Plataforma", "eliminado": false, "estadoNombre": "Activo" }
      ],
      "totalCount": 2,
      "pageNumber": 1,
      "pageSize": 10,
      "totalPages": 1,
      "hasPreviousPage": false,
      "hasNextPage": false
    }
    """;

    public S681ListasSinInspectorTests()
    {
        Handler.Responder("/api/tenants/page", TenantsPagina);
        Handler.Responder("/api/tenants/count", "2");
        Handler.Responder("/api/tenants/activos/count", "2");
        Handler.Responder("/api/dominiosTenant/count", "1");
        Handler.Responder("/api/apps/page", AppsPagina);
        Handler.Responder("/api/apps", AppsLista);
        Handler.Responder("/api/usuarios/page", UsuariosPagina);
    }

    private NavigationManager Navegacion => Services.GetRequiredService<NavigationManager>();

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private async Task<IRenderedComponent<TenantsIndex>> RenderTenantsAsync()
    {
        await AutenticarAsync(permisos: "TENANTS_VER");
        var cut = Render<TenantsIndex>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tbody tr.mud-table-row")));
        return cut;
    }

    private async Task<IRenderedComponent<UsuariosIndex>> RenderUsuariosAsync()
    {
        await AutenticarAsync(permisos: "USUARIOS_VER");
        var cut = Render<UsuariosIndex>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tbody tr.mud-table-row")));
        return cut;
    }

    private async Task<IRenderedComponent<AppsIndex>> RenderAppsAsync()
    {
        await AutenticarAsync(permisos: "APPS_VER");
        var cut = Render<AppsIndex>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tbody tr.mud-table-row")));
        return cut;
    }

    [Fact]
    public async Task Tenants_clic_en_fila_navega_al_detalle()
    {
        var cut = await RenderTenantsAsync();

        cut.FindAll("tbody tr.mud-table-row")[0].Click();

        cut.WaitForAssertion(() => Assert.EndsWith("/tenant/2", Navegacion.Uri));
    }

    [Fact]
    public async Task Tenants_no_muestra_inspector()
    {
        var cut = await RenderTenantsAsync();

        cut.FindAll("tbody tr.mud-table-row")[0].Click();
        cut.WaitForAssertion(() => Assert.EndsWith("/tenant/2", Navegacion.Uri));

        Assert.DoesNotContain("Detalle de Tenant", cut.Markup);
        Assert.DoesNotContain("iam-inspector", cut.Markup);
    }

    [Fact]
    public async Task Tenants_abrir_menu_de_acciones_no_navega()
    {
        await AutenticarAsync(permisos: "TENANTS_VER");
        var cut = Render<TestHost>(p => p.Add(x => x.ChildContent, (RenderFragment)(builder =>
        {
            builder.OpenComponent<TenantsIndex>(0);
            builder.CloseComponent();
        })));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tbody tr.mud-table-row")));

        var acciones = cut.FindAll("tbody tr.mud-table-row")[0].QuerySelector(".table-actions");
        Assert.NotNull(acciones);
        var boton = acciones!.QuerySelector("button");
        Assert.NotNull(boton);

        boton!.Click();

        Assert.DoesNotContain("/tenant/", Navegacion.Uri);
    }

    [Fact]
    public async Task Usuarios_clic_en_fila_navega_al_detalle()
    {
        var cut = await RenderUsuariosAsync();

        cut.FindAll("tbody tr.mud-table-row")[0].Click();

        cut.WaitForAssertion(() => Assert.EndsWith("/usuario/1", Navegacion.Uri));
    }

    [Fact]
    public async Task Apps_muestra_columna_creado_y_no_inspector()
    {
        var cut = await RenderAppsAsync();

        Assert.Contains("<th", cut.Markup);
        cut.WaitForAssertion(() => Assert.Contains("Creado", cut.Markup));

        var fila = cut.FindAll("tbody tr.mud-table-row")[0];
        var uriAntes = Navegacion.Uri;
        fila.Click();
        Assert.Equal(uriAntes, Navegacion.Uri);

        Assert.DoesNotContain("iam-inspector", cut.Markup);
        Assert.DoesNotContain("Detalles de la Aplicación", cut.Markup);
    }

    [Fact]
    public void Paginas_sin_inspector_ni_align_items_muerto()
    {
        var raiz = RaizDelRepo();
        var archivos = new[]
        {
            Path.Combine("PassPlat.Web", "Pages", "Tenants", "Index.razor"),
            Path.Combine("PassPlat.Web", "Pages", "Apps", "Index.razor"),
            Path.Combine("PassPlat.Web", "Pages", "Usuarios", "Index.razor"),
            Path.Combine("PassPlat.Web", "Pages", "Sesiones", "Index.razor"),
            Path.Combine("PassPlat.Web", "Pages", "Bloqueos", "Index.razor")
        };
        var pendientes = new List<string>();

        foreach (var archivo in archivos)
        {
            var texto = File.ReadAllText(Path.Combine(raiz, archivo));
            foreach (var patron in new[] { "IamInspector", "_inspectorTab", "iam-inspector", "align-items=\"AlignItems.Center\"" })
            {
                if (texto.Contains(patron, StringComparison.Ordinal))
                    pendientes.Add($"{archivo.Replace('\\', '/')}: {patron}");
            }
        }

        Assert.Empty(pendientes);
    }
}
