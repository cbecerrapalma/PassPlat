using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PassPlat.Web.Shared;
using PassPlat.Web.Tests.Infra;
using PermisoDetail = PassPlat.Web.Pages.Permisos.PermisoDetail;
using PermisosIndex = PassPlat.Web.Pages.Permisos.Index;

namespace PassPlat.Web.Tests;

/// <summary>
/// S104 · D3-D4 — catálogo de permisos en árbol App → Módulo (por IdModulo) → Permiso, con filtro de app,
/// búsqueda amplia, contadores de roles y acciones de escritura solo con su permiso; detalle con app,
/// solo los roles que tienen el permiso y sin Editar/Desactivar sin PERMISOS_EDITAR.
/// </summary>
public class S104PermisosCatalogoTests : WebTestBase
{
    private static string Permiso(int id, string codigo, string nombre, int idModulo, string modulo, string app, string? descripcion = null, bool activo = true) =>
        $$"""{ "id": {{id}}, "codigo": "{{codigo}}", "nombre": "{{nombre}}", "descripcion": {{(descripcion is null ? "null" : $"\"{descripcion}\"")}}, "idModulo": {{idModulo}}, "orden": {{id}}, "activo": {{(activo ? "true" : "false")}}, "fecCrea": "2026-01-01T00:00:00Z", "moduloNombre": "{{modulo}}", "moduloCodigo": "M{{idModulo}}", "appNombre": "{{app}}" }""";

    private static string Fila(int idRol, int idPermiso, string estado) =>
        $$"""{ "idRol": {{idRol}}, "rolNombre": "Rol {{idRol}}", "rolCodigo": "ROL{{idRol}}", "idPermiso": {{idPermiso}}, "codigoPermiso": "P{{idPermiso}}", "nombrePermiso": "Permiso {{idPermiso}}", "ordenPermiso": 1, "idModulo": 1, "nombreModulo": "Mod", "codigoModulo": "MOD", "ordenModulo": 1, "estado": "{{estado}}", "esDirecto": {{(estado == "on" ? "true" : "false")}}, "esHeredado": {{(estado == "inherited" ? "true" : "false")}} }""";

    // AccessPlat: Ventas(1). Inventa POS: Ventas(2), Ventas(4) — homónimos con distinto IdModulo — y Clientes(3).
    private static readonly string Catalogo = "[" + string.Join(",",
        Permiso(1, "VENTAS_VER", "Ver ventas web", 1, "Ventas", "AccessPlat"),
        Permiso(2, "POS_VENTAS_VER", "Ver ventas", 2, "Ventas", "Inventa POS"),
        Permiso(3, "POS_CLIENTES_VER", "Ver clientes", 3, "Clientes", "Inventa POS", "Consulta la cartera de compradores"),
        Permiso(4, "POS_DEVOLUCIONES_VER", "Ver devoluciones", 4, "Ventas", "Inventa POS", activo: false)) + "]";

    public S104PermisosCatalogoTests()
    {
        Handler.Responder("/api/permisos", Catalogo);
        Handler.Responder("/api/MatrizPermisos", "[" + string.Join(",", Fila(1, 1, "on"), Fila(2, 1, "inherited"), Fila(3, 1, "off"), Fila(3, 2, "off")) + "]");
    }

    private async Task<IRenderedComponent<PermisosIndex>> RenderIndexAsync(params string[] permisos)
    {
        await AutenticarAsync(permisos: permisos.Length > 0 ? permisos : ["PERMISOS_VER"]);
        var cut = Render<PermisosIndex>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".tree-app")), TimeSpan.FromSeconds(10));
        return cut;
    }

    private static string Texto(AngleSharp.Dom.IElement e) => System.Text.RegularExpressions.Regex.Replace(e.TextContent, @"\s+", " ").Trim();

    [Fact]
    public async Task Agrupa_por_app_y_por_IdModulo_sin_fusionar_homonimos()
    {
        var cut = await RenderIndexAsync();

        Assert.Equal(["AccessPlat · 1 permiso · 1 módulo", "Inventa POS · 3 permisos · 3 módulos"], cut.FindAll(".tree-app").Select(Texto).ToList());
        // «Ventas» aparece tres veces: uno por IdModulo (1, 2 y 4), nunca fusionados por nombre
        Assert.Equal(4, cut.FindAll(".tree-modulo").Count);
        Assert.Equal(3, cut.FindAll(".tree-modulo").Count(m => Texto(m).StartsWith("Ventas", StringComparison.Ordinal)));
        Assert.Equal(["1", "2", "3", "4"], cut.FindAll(".tree-modulo").Select(m => m.GetAttribute("data-modulo-id")!).Order().ToList());
        // los módulos inician plegados; «Expandir todo» muestra los 4 permisos, con su código y el chip de inactivo
        Assert.Empty(cut.FindAll(".tree-permiso"));
        await cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Contains("Expandir todo")).Click());
        Assert.Equal(4, cut.FindAll(".tree-permiso").Count);
        Assert.Contains("POS_CLIENTES_VER", cut.Find("[data-permiso-id='3'] .permiso-codigo").TextContent);
        Assert.Single(cut.FindAll(".permiso-inactivo"));
        // clic en un permiso navega al detalle (S69.3)
        cut.Find("[data-permiso-id='3']").Click();
        Assert.EndsWith("/permiso/3", Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri);
    }

    [Fact]
    public async Task Filtro_de_app_y_busqueda_por_modulo_y_descripcion()
    {
        var cut = await RenderIndexAsync();
        var selector = cut.FindComponents<MudSelect<string>>().Single(s => s.Instance.Label == "Aplicación");
        var buscar = cut.FindComponents<MudTextField<string>>().Single(t => t.Instance.Label == "Buscar…");

        await cut.InvokeAsync(() => selector.Instance.ValueChanged.InvokeAsync("Inventa POS"));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".tree-app")));
        Assert.Contains("Inventa POS", Texto(cut.Find(".tree-app")));

        await cut.InvokeAsync(() => selector.Instance.ValueChanged.InvokeAsync("Todas"));
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".tree-app").Count));

        // por nombre de módulo: «Clientes» deja solo ese módulo y su permiso (la búsqueda abre todo)
        await cut.InvokeAsync(() => buscar.Find("input").Input("Clientes"));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".tree-modulo")));
        Assert.Equal("3", cut.Find(".tree-modulo").GetAttribute("data-modulo-id"));
        Assert.Single(cut.FindAll(".tree-permiso"));

        // por texto de la descripción
        await cut.InvokeAsync(() => buscar.Find("input").Input("cartera"));
        cut.WaitForAssertion(() => Assert.Equal("3", Assert.Single(cut.FindAll(".tree-permiso")).GetAttribute("data-permiso-id")));

        // sin coincidencias
        await cut.InvokeAsync(() => buscar.Find("input").Input("zzz-no-existe"));
        cut.WaitForAssertion(() => Assert.Contains("No hay permisos que coincidan", cut.Markup));
        Assert.Empty(cut.FindAll(".tree-app"));
    }

    [Fact]
    public async Task Cuenta_roles_on_e_inherited_del_tenant_y_marca_sin_rol()
    {
        var cut = await RenderIndexAsync();
        await cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Contains("Expandir todo")).Click());

        // permiso 1: roles on + inherited (el «off» no cuenta); permiso 2: solo «off»; 3 y 4 sin filas
        Assert.Equal("2 roles", Texto(cut.Find("[data-permiso-id='1'] .permiso-roles")));
        Assert.Equal("Sin rol", Texto(cut.Find("[data-permiso-id='2'] .permiso-roles")));
        Assert.Equal("Sin rol", Texto(cut.Find("[data-permiso-id='3'] .permiso-roles")));
        // KPI «Sin rol»: activos sin rol = permisos 2 y 3 (el 4 está inactivo)
        Assert.Equal("2", cut.FindComponents<IamKpiCard>().Single(k => k.Instance.Label == "Sin rol").Instance.Valor);
        Assert.Equal("4", cut.FindComponents<IamKpiCard>().Single(k => k.Instance.Label == "Módulos").Instance.Valor);
        Assert.Contains(Handler.Peticiones, p => p.EndsWith("/api/MatrizPermisos", StringComparison.OrdinalIgnoreCase) && p.StartsWith("GET", StringComparison.Ordinal));
        // el sistema pide la matriz completa; un usuario de tenant, solo la de su tenant
        Assert.Equal("api/MatrizPermisos", PermisosIndex.RutaMatriz(true, 3));
        Assert.Equal("api/MatrizPermisos?idTenant=3", PermisosIndex.RutaMatriz(false, 3));
    }

    [Fact]
    public async Task Nuevo_permiso_solo_con_PERMISOS_CREAR()
    {
        var sin = await RenderIndexAsync("PERMISOS_VER", "PERMISOS_EDITAR");
        Assert.DoesNotContain(sin.FindAll("button"), b => b.TextContent.Contains("Nuevo Permiso"));

        var con = await RenderIndexAsync("PERMISOS_VER", "PERMISOS_CREAR");
        Assert.Contains(con.FindAll("button"), b => b.TextContent.Contains("Nuevo Permiso"));
    }

    [Fact]
    public async Task Detalle_lista_solo_roles_con_el_permiso_y_oculta_acciones_sin_EDITAR()
    {
        Handler.Responder("/api/permisos/1", Permiso(1, "VENTAS_VER", "Ver ventas web", 1, "Ventas", "AccessPlat"));
        Handler.Responder("/api/MatrizPermisos", "[" + string.Join(",", Fila(3, 1, "on"), Fila(4, 1, "off"), Fila(5, 1, "inherited"), Fila(6, 2, "on")) + "]");
        await AutenticarAsync(permisos: ["PERMISOS_VER"]);

        var cut = Render<PermisoDetail>(p => p.Add(x => x.Id, 1));
        cut.WaitForAssertion(() => Assert.Contains("Ver ventas web", cut.Markup), TimeSpan.FromSeconds(10));

        Assert.Contains("AccessPlat", cut.Markup);
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Contains("Editar") || b.TextContent.Contains("Desactivar"));
        Assert.DoesNotContain("Auditoría", cut.Markup);

        await cut.InvokeAsync(() => cut.FindAll(".mud-tab").First(t => t.TextContent.Contains("Roles")).Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mini-table tbody tr")));
        var filas = cut.FindAll(".mini-table tbody tr").Select(Texto).ToList();
        Assert.Equal(2, filas.Count);
        Assert.Contains(filas, f => f.Contains("Rol 3") && f.Contains("ROL3") && f.Contains("Directo"));
        Assert.Contains(filas, f => f.Contains("Rol 5") && f.Contains("ROL5") && f.Contains("Heredado"));
        Assert.DoesNotContain(filas, f => f.Contains("Rol 4") || f.Contains("Rol 6"));

        // con PERMISOS_EDITAR reaparecen Editar y Desactivar
        await AutenticarAsync(permisos: ["PERMISOS_VER", "PERMISOS_EDITAR"]);
        var editor = Render<PermisoDetail>(p => p.Add(x => x.Id, 1));
        editor.WaitForAssertion(() => Assert.Contains(editor.FindAll("button"), b => b.TextContent.Contains("Editar")), TimeSpan.FromSeconds(10));
        Assert.Contains(editor.FindAll("button"), b => b.TextContent.Contains("Desactivar"));
    }
}
