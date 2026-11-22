using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PassPlat.Web.Shared;
using PassPlat.Web.Tests.Infra;
using AccesoDetail = PassPlat.Web.Pages.Accesos.AccesoDetail;
using AccesosIndex = PassPlat.Web.Pages.Accesos.Index;
using PermisoDetail = PassPlat.Web.Pages.Permisos.PermisoDetail;
using PermisosIndex = PassPlat.Web.Pages.Permisos.Index;

namespace PassPlat.Web.Tests;

/// <summary>
/// S69.3 · K3 — Permisos y Accesos sin inspector: un clic en el árbol o en la fila navega a
/// <c>/permiso/{id}</c> o <c>/acceso/{idUsuario}/{id}</c>, y las páginas de detalle muestran
/// el contenido que antes tenía el panel lateral.
/// </summary>
public class S693PermisosAccesosDetalleTests : WebTestBase
{
    private const string Permiso = """
    { "id": 7, "codigo": "AUDIT_VER", "nombre": "Ver auditoría", "descripcion": "Consulta la auditoría", "idModulo": 2, "orden": 1, "activo": true, "fecCrea": "2026-01-01T00:00:00Z", "moduloNombre": "Auditoría", "moduloCodigo": "AUDIT", "appNombre": "AccessPlat" }
    """;

    private const string Matriz = """
    [
      { "idRol": 3, "rolNombre": "Auditor Interno", "rolCodigo": "AUDITOR", "idPermiso": 7, "codigoPermiso": "AUDIT_VER", "nombrePermiso": "Ver auditoría", "ordenPermiso": 1, "idModulo": 2, "nombreModulo": "Auditoría", "codigoModulo": "AUDIT", "ordenModulo": 1, "estado": "on", "esDirecto": true, "esHeredado": false }
    ]
    """;

    public S693PermisosAccesosDetalleTests()
    {
        CargarCatalogos();
        Handler.Responder("/api/permisos", $"[{Permiso}]");
        Handler.Responder("/api/permisos/7", Permiso);
        Handler.Responder("/api/MatrizPermisos", Matriz);
        Handler.Responder("/api/accesos/usuario/1", Datos.AccesosJson((1, 12, 1, "Administrador")));
        Handler.Responder("/api/accesos/usuario/2", "[]");
    }

    private NavigationManager Navegacion => Services.GetRequiredService<NavigationManager>();

    private IRenderedComponent<TestHost> RenderAccesos() =>
        Render<TestHost>(p => p.Add(x => x.ChildContent, (RenderFragment)(b =>
        {
            b.OpenComponent<AccesosIndex>(0);
            b.CloseComponent();
        })));

    private IRenderedComponent<TestHost> RenderAccesoDetail(int idUsuario, int id) =>
        Render<TestHost>(p => p.Add(x => x.ChildContent, (RenderFragment)(b =>
        {
            b.OpenComponent<AccesoDetail>(0);
            b.AddAttribute(1, nameof(AccesoDetail.IdUsuario), idUsuario);
            b.AddAttribute(2, nameof(AccesoDetail.Id), id);
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
    public async Task Permisos_clic_en_nodo_navega_al_detalle()
    {
        await AutenticarAsync(permisos: "PERMISOS_VER");
        var cut = Render<PermisosIndex>();
        cut.WaitForAssertion(() => Assert.Contains("Auditoría", cut.Markup));

        cut.FindAll(".tree-modulo").First(n => n.TextContent.Contains("Auditoría")).Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-permiso-id='7']")));

        cut.Find("[data-permiso-id='7']").Click();

        cut.WaitForAssertion(() => Assert.EndsWith("/permiso/7", Navegacion.Uri));
    }

    [Fact]
    public async Task Accesos_clic_en_fila_navega_al_detalle()
    {
        await AutenticarAsync(permisos: "ACCESOS_VER");
        var cut = RenderAccesos();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tbody tr.mud-table-row")));

        cut.FindAll("tbody tr.mud-table-row")[0].Click();

        cut.WaitForAssertion(() => Assert.EndsWith("/acceso/1/1", Navegacion.Uri));
    }

    [Fact]
    public async Task PermisoDetail_muestra_nombre_y_pestanas()
    {
        await AutenticarAsync(permisos: "PERMISOS_VER");
        var cut = Render<PermisoDetail>(p => p.Add(x => x.Id, 7));

        cut.WaitForAssertion(() => Assert.Contains("Ver auditoría", cut.Markup));
        foreach (var pestana in new[] { "Info", "Roles" })
            Assert.Contains(pestana, cut.Markup);
    }

    [Fact]
    public async Task PermisoDetail_sin_permiso_muestra_SinPermiso()
    {
        await AutenticarAsync(permisos: "USUARIOS_VER");
        var cut = Render<PermisoDetail>(p => p.Add(x => x.Id, 7));

        Assert.NotEmpty(cut.FindComponents<SinPermiso>());
        Assert.DoesNotContain(Handler.Peticiones, p => p.Contains("/api/permisos", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AccesoDetail_muestra_app_y_seis_pestanas()
    {
        await AutenticarAsync(permisos: "ACCESOS_VER");
        var cut = RenderAccesoDetail(1, 1);

        cut.WaitForAssertion(() => Assert.Contains("Inventa POS", cut.Markup));
        foreach (var pestana in new[] { "Información", "Usuario", "Aplicación", "Tenant", "Rol", "Auditoría" })
            Assert.Contains(pestana, cut.Markup);
    }

    [Fact]
    public async Task AccesoDetail_id_inexistente_muestra_no_encontrado()
    {
        await AutenticarAsync(permisos: "ACCESOS_VER");
        var cut = RenderAccesoDetail(1, 999);

        cut.WaitForAssertion(() => Assert.Contains("Acceso no encontrado", cut.Markup));
    }

    [Fact]
    public async Task Accesos_query_usuario_preselecciona_filtro()
    {
        await AutenticarAsync(permisos: "ACCESOS_VER");
        Navegacion.NavigateTo("/accesos?usuario=2");

        var cut = RenderAccesos();

        cut.WaitForAssertion(() => Assert.Contains(Handler.Peticiones, p => p.EndsWith("/api/accesos/usuario/2", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(Handler.Peticiones, p => p.EndsWith("/api/accesos/usuario/1", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Permisos_y_Accesos_sin_inspector()
    {
        var raiz = RaizDelRepo();
        var archivos = new[]
        {
            Path.Combine("PassPlat.Web", "Pages", "Permisos", "Index.razor"),
            Path.Combine("PassPlat.Web", "Pages", "Accesos", "Index.razor")
        };
        var pendientes = new List<string>();

        foreach (var archivo in archivos)
        {
            var texto = File.ReadAllText(Path.Combine(raiz, archivo));
            foreach (var patron in new[] { "iam-inspector", "insp-", "_inspectorTab", "_selectedItem", "_selectedPermiso", "SelectedItemChanged" })
            {
                if (texto.Contains(patron, StringComparison.Ordinal))
                    pendientes.Add($"{archivo.Replace('\\', '/')}: {patron}");
            }
        }

        Assert.Empty(pendientes);
    }
}
