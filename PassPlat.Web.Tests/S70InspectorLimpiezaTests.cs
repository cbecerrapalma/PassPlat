using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PassPlat.Web.Tests.Infra;
using DispositivoDetail = PassPlat.Web.Pages.Dispositivos.DispositivoDetail;
using DispositivosIndex = PassPlat.Web.Pages.Dispositivos.Index;

namespace PassPlat.Web.Tests;

/// <summary>
/// S70 · K4 — Limpieza final del inspector: <c>/dispositivos</c> navega a <c>/dispositivos/{id}</c>,
/// los iconos de acción no navegan, el detalle muestra sus dos secciones y en <c>PassPlat.Web</c>
/// no queda ningún resto del inspector.
/// </summary>
public class S70InspectorLimpiezaTests : WebTestBase
{
    private const string Dispositivos = """
    [
      { "id": 1, "idUsuario": 3, "idTenant": 1, "idDisp": 10, "nombre": "Portátil", "fecAlta": "2026-10-01T10:00:00Z", "confiable": true, "dispModelo": "ThinkPad X1", "dispFabricante": "Lenovo", "dispTipo": "Laptop", "ip": "10.0.0.1", "pais": "CL", "navegador": "Edge", "so": "Windows", "cantidadLogins": 4, "usuarioNombre": "ana.local" },
      { "id": 2, "idUsuario": 3, "idTenant": 1, "idDisp": 11, "nombre": "Móvil", "fecAlta": "2026-10-02T10:00:00Z", "confiable": false, "dispModelo": "Pixel 8", "dispFabricante": "Google", "dispTipo": "Phone", "cantidadLogins": 1, "usuarioNombre": "ana.local" }
    ]
    """;

    private static readonly string[] DetallesConSecciones =
    [
        Path.Combine("PassPlat.Web", "Pages", "Accesos", "AccesoDetail.razor"),
        Path.Combine("PassPlat.Web", "Pages", "Roles", "RolDetail.razor"),
        Path.Combine("PassPlat.Web", "Pages", "Permisos", "PermisoDetail.razor"),
        Path.Combine("PassPlat.Web", "Pages", "Grupos", "GrupoDetail.razor"),
        Path.Combine("PassPlat.Web", "Pages", "Federacion", "ConfProvIden", "ConfProvIdenDetail.razor"),
        Path.Combine("PassPlat.Web", "Pages", "Federacion", "IdenExt", "IdenExtDetail.razor")
    ];

    public S70InspectorLimpiezaTests()
    {
        Handler.Responder("/api/DispConfiables", Dispositivos);
    }

    private NavigationManager Navegacion => Services.GetRequiredService<NavigationManager>();

    private IRenderedComponent<TestHost> RenderLista() =>
        Render<TestHost>(p => p.Add(x => x.ChildContent, (RenderFragment)(b =>
        {
            b.OpenComponent<DispositivosIndex>(0);
            b.CloseComponent();
        })));

    private IRenderedComponent<TestHost> RenderDetalle(int id) =>
        Render<TestHost>(p => p.Add(x => x.ChildContent, (RenderFragment)(b =>
        {
            b.OpenComponent<DispositivoDetail>(0);
            b.AddAttribute(1, nameof(DispositivoDetail.Id), id);
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
    public async Task Dispositivos_clic_en_fila_navega_al_detalle()
    {
        await AutenticarAsync();
        var cut = RenderLista();
        cut.WaitForAssertion(() => Assert.Contains("ThinkPad X1", cut.Markup));

        cut.FindAll("tbody tr.mud-table-row")[0].Click();

        cut.WaitForAssertion(() => Assert.EndsWith("/dispositivos/1", Navegacion.Uri));
    }

    [Fact]
    public async Task Dispositivos_clic_en_accion_no_navega()
    {
        await AutenticarAsync();
        var cut = RenderLista();
        cut.WaitForAssertion(() => Assert.Contains("ThinkPad X1", cut.Markup));
        var uriAntes = Navegacion.Uri;

        cut.FindAll("tbody tr.mud-table-row")[0]
            .QuerySelector("button[aria-label='Acción peligrosa']")!.Click();

        Assert.Equal(uriAntes, Navegacion.Uri);
    }

    [Fact]
    public async Task DispositivoDetail_muestra_dispositivo_y_secciones()
    {
        await AutenticarAsync();
        var cut = RenderDetalle(1);

        cut.WaitForAssertion(() => Assert.Contains("ThinkPad X1", cut.Markup));
        foreach (var texto in new[] { "Lenovo", "Confiable", "ana.local", ">Dispositivo<", ">Uso<" })
            Assert.Contains(texto, cut.Markup);
    }

    [Fact]
    public async Task DispositivoDetail_id_inexistente_muestra_no_encontrado()
    {
        await AutenticarAsync();
        var cut = RenderDetalle(99);

        cut.WaitForAssertion(() => Assert.Contains("Dispositivo no encontrado", cut.Markup));
    }

    [Fact]
    public void Web_sin_restos_del_inspector()
    {
        var web = Path.Combine(RaizDelRepo(), "PassPlat.Web");
        var patrones = new[] { "IamInspector", "InspectorField", "iam-inspector", "inspector-", "tab-btn", "insp-", "_inspector", "AbrirInspector" };
        var pendientes = new List<string>();

        foreach (var nombre in new[] { "IamInspector.razor", "InspectorField.razor" })
        {
            if (File.Exists(Path.Combine(web, "Shared", nombre)))
                pendientes.Add($"Shared/{nombre}: existe");
        }

        var archivos = new[] { "*.razor", "*.css", "*.md" }
            .SelectMany(filtro => Directory.EnumerateFiles(web, filtro, SearchOption.AllDirectories))
            .Where(ruta =>
            {
                var relativa = Path.GetRelativePath(web, ruta).Replace('\\', '/');
                return !relativa.StartsWith("bin/", StringComparison.Ordinal) && !relativa.StartsWith("obj/", StringComparison.Ordinal);
            });

        foreach (var archivo in archivos)
        {
            var texto = File.ReadAllText(archivo);
            foreach (var patron in patrones)
            {
                if (texto.Contains(patron, StringComparison.Ordinal))
                    pendientes.Add($"{Path.GetRelativePath(web, archivo).Replace('\\', '/')}: {patron}");
            }
        }

        Assert.Empty(pendientes);
    }

    [Fact]
    public void Detalles_usan_detail_section_con_estilo()
    {
        var raiz = RaizDelRepo();
        var pendientes = new List<string>();

        foreach (var archivo in DetallesConSecciones)
        {
            if (!File.ReadAllText(Path.Combine(raiz, archivo)).Contains("detail-section", StringComparison.Ordinal))
                pendientes.Add($"{archivo.Replace('\\', '/')}: sin detail-section");
        }

        var css = File.ReadAllText(Path.Combine(raiz, "PassPlat.Web", "wwwroot", "css", "iam-pages.css"));
        foreach (var regla in new[] { ".iam-page .detail-section", ".iam-page .field-label", ".iam-page .field-value" })
        {
            if (!css.Contains(regla, StringComparison.Ordinal))
                pendientes.Add($"iam-pages.css: falta {regla}");
        }

        var lista = File.ReadAllText(Path.Combine(raiz, "PassPlat.Web", "Pages", "Dispositivos", "Index.razor"));
        foreach (var patron in new[] { "_selectedItem", "SelectedItemChanged" })
        {
            if (lista.Contains(patron, StringComparison.Ordinal))
                pendientes.Add($"Dispositivos/Index.razor: {patron}");
        }

        Assert.Empty(pendientes);
    }
}
