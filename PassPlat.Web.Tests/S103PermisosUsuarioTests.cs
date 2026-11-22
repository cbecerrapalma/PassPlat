using System.Text.Json;
using Bunit;
using MudBlazor;
using PassPlat.Web.Tests.Infra;
using UsuarioPermisos = PassPlat.Web.Pages.Usuarios.Components.UsuarioPermisos;

namespace PassPlat.Web.Tests;

/// <summary>
/// S103 · D8-D9 y D11 — editor unificado de permisos de un usuario por (tenant, app): casilla = lo tiene,
/// origen por fila y cambios acumulados en un solo POST (el diálogo de permisos de rol se retiró en S106).
/// </summary>
public class S103PermisosUsuarioTests : WebTestBase
{
    private const string UnContexto = """
    [{ "idTenant": 1, "tenantNombre": "PLATFORM", "idApp": 1, "appNombre": "AccessPlat", "appCodigo": "PASSPLAT", "roles": [{ "idRol": 3, "nombre": "Admin" }] }]
    """;

    private const string DosApps = """
    [{ "idTenant": 1, "tenantNombre": "PLATFORM", "idApp": 1, "appNombre": "AccessPlat", "appCodigo": "PASSPLAT", "roles": [{ "idRol": 3, "nombre": "Admin" }] },
     { "idTenant": 1, "tenantNombre": "PLATFORM", "idApp": 2, "appNombre": "Inventa", "appCodigo": "INVENTA", "roles": [{ "idRol": 3, "nombre": "Admin" }] }]
    """;

    // 10 Ver usuarios (por rol), 11 Editar usuarios (agregado), 12 Borrar usuarios (por rol y quitado),
    // 13 Exportar (agregado vencido), 14 Importar (nada)
    private const string Editor = """
    { "idTenant": 1, "tenantNombre": "PLATFORM", "idApp": 1, "appNombre": "AccessPlat", "esSistema": false,
      "roles": [{ "idRol": 3, "nombre": "Admin" }],
      "permisos": [
        { "idPermiso": 10, "codigo": "USUARIOS_VER", "nombre": "Ver usuarios", "idModulo": 7, "moduloNombre": "Usuarios", "orden": 1, "porRol": true, "rolOrigen": "Admin", "directoVigente": false, "tiene": true },
        { "idPermiso": 11, "codigo": "USUARIOS_EDITAR", "nombre": "Editar usuarios", "idModulo": 7, "moduloNombre": "Usuarios", "orden": 2, "porRol": false, "idTipoAsigDirecto": 1, "directoVigente": true, "tiene": true },
        { "idPermiso": 12, "codigo": "USUARIOS_BORRAR", "nombre": "Borrar usuarios", "idModulo": 7, "moduloNombre": "Usuarios", "orden": 3, "porRol": true, "rolOrigen": "Admin", "idTipoAsigDirecto": 2, "directoVigente": true, "tiene": false },
        { "idPermiso": 13, "codigo": "USUARIOS_EXPORTAR", "nombre": "Exportar usuarios", "idModulo": 7, "moduloNombre": "Usuarios", "orden": 4, "porRol": false, "idTipoAsigDirecto": 1, "directoVigente": false, "tiene": false },
        { "idPermiso": 14, "codigo": "TENANTS_IMPORTAR", "nombre": "Importar tenants", "idModulo": 8, "moduloNombre": "Tenants", "orden": 1, "porRol": false, "directoVigente": false, "tiene": false }
      ] }
    """;

    private const string Resultado = """{ "concedidos": 1, "denegados": 1, "revocados": 0, "sinCambio": 0 }""";

    private async Task<IRenderedComponent<TestHost>> RenderAsync(string contextos = UnContexto, params string[] permisos)
    {
        Handler.Responder("/api/UsuariosPermisos/contextos/2", contextos);
        Handler.Responder("/api/UsuariosPermisos/editor/2", Editor);
        Handler.Responder("/api/UsuariosPermisos/aplicar", Resultado);
        await AutenticarAsync(1, 1, "sistema", permisos.Length > 0 ? permisos : ["PERMISOS_VER", "PERMISOS_EDITAR", "ROLES_VER"]);
        var cut = Render<TestHost>(p => p.AddChildContent<UsuarioPermisos>(c => c.Add(x => x.UserId, 2).Add(x => x.IsActive, true)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".permiso-fila")), TimeSpan.FromSeconds(10));
        return cut;
    }

    private static AngleSharp.Dom.IElement Casilla(IRenderedComponent<TestHost> cut, string nombre) =>
        cut.FindAll(".permiso-fila").First(f => f.TextContent.Contains(nombre)).QuerySelector("input[type=checkbox]")!;

    private static bool Marcada(IRenderedComponent<TestHost> cut, string nombre) =>
        Casilla(cut, nombre).HasAttribute("checked");

    private static IEnumerable<string> Posts(FakeHttpHandler handler) =>
        handler.Peticiones.Where(p => p.StartsWith("POST /api/UsuariosPermisos/aplicar", StringComparison.Ordinal));

    [Fact]
    public async Task Pestana_lista_los_permisos_de_la_app_con_su_origen()
    {
        var cut = await RenderAsync();

        Assert.Equal(5, cut.FindAll(".permiso-fila").Count);
        Assert.Equal(2, cut.FindAll(".permiso-fila .permiso-por-rol").Count);
        Assert.Single(cut.FindAll(".permiso-fila .permiso-agregado"));
        Assert.Single(cut.FindAll(".permiso-fila .permiso-quitado"));
        Assert.Single(cut.FindAll(".permiso-fila .permiso-vencido"));
        Assert.Equal("Tiene 2 de 5 · rol Admin", cut.Find(".resumen-permisos").TextContent.Trim());
        Assert.True(Marcada(cut, "Ver usuarios"));
        Assert.True(Marcada(cut, "Editar usuarios"));
        Assert.False(Marcada(cut, "Borrar usuarios"));
        Assert.False(Marcada(cut, "Importar tenants"));
        Assert.Equal("Editar rol", cut.Find("a.editar-rol").TextContent.Trim());
        Assert.Equal("/rol/3", cut.Find("a.editar-rol").GetAttribute("href"));
        // una sola petición por recurso y sin el catálogo completo de permisos
        Assert.Single(Handler.Peticiones, p => p.StartsWith("GET /api/UsuariosPermisos/contextos/2", StringComparison.Ordinal));
        Assert.Single(Handler.Peticiones, p => p.StartsWith("GET /api/UsuariosPermisos/editor/2", StringComparison.Ordinal));
        Assert.DoesNotContain(Handler.Peticiones, p => p.Contains("api/permisos/activos", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Marcar_y_desmarcar_guarda_un_solo_POST_aplicar_con_los_cambios()
    {
        var cut = await RenderAsync();

        await cut.InvokeAsync(() => Casilla(cut, "Ver usuarios").Change(false));       // viene del rol: se quita solo a él
        await cut.InvokeAsync(() => Casilla(cut, "Importar tenants").Change(true));    // no lo tiene: se agrega

        Assert.Contains("Cambios: +1, −1", cut.Find(".barra-cambios").TextContent);
        Assert.Equal("Se quitará solo a este usuario", cut.Find(".permiso-fila:has(.permiso-por-rol) .permiso-cambio").TextContent.Trim());
        Assert.Empty(Posts(Handler));

        await cut.InvokeAsync(() => cut.FindAll(".barra-cambios button").First(b => b.TextContent.Contains("Guardar cambios")).Click());
        cut.WaitForAssertion(() => Assert.Contains("Confirmar cambios de permisos", cut.Markup), TimeSpan.FromSeconds(10));
        Assert.Empty(Posts(Handler));                                                    // el diálogo no escribe
        await cut.InvokeAsync(() => cut.Find("[data-testid=boton-guardar-dialogo]").Click());

        cut.WaitForAssertion(() => Assert.Single(Posts(Handler)), TimeSpan.FromSeconds(10));
        var cuerpo = Posts(Handler).Single()["POST /api/UsuariosPermisos/aplicar ".Length..];
        using var json = JsonDocument.Parse(cuerpo);
        Assert.Equal(2, json.RootElement.GetProperty("idUsuario").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("idTenant").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("idApp").GetInt32());
        var cambios = json.RootElement.GetProperty("cambios").EnumerateArray()
            .Select(c => (c.GetProperty("idPermiso").GetInt32(), c.GetProperty("tiene").GetBoolean())).Order().ToList();
        Assert.Equal([(10, false), (14, true)], cambios);
        // recarga el editor y limpia los cambios pendientes
        cut.WaitForAssertion(() => Assert.Equal(2, Handler.Peticiones.Count(p => p.StartsWith("GET /api/UsuariosPermisos/editor/2", StringComparison.Ordinal))), TimeSpan.FromSeconds(10));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".barra-cambios")), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Seleccionar_visibles_marca_solo_los_filtrados()
    {
        var cut = await RenderAsync();

        await cut.InvokeAsync(() => cut.Find("input[type=text]").Input("usuarios"));      // 4 de 5 coinciden con «usuarios»
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll(".permiso-fila").Count), TimeSpan.FromSeconds(10));
        await cut.InvokeAsync(() => cut.Find(".seleccionar-visibles input[type=checkbox]").Change(true));

        // marca los dos visibles que no tenía; «Importar tenants» queda fuera del filtro y sin tocar
        Assert.Equal(2, cut.FindAll(".permiso-cambio").Count);
        Assert.Contains("Cambios: +2, −0", cut.Find(".barra-cambios").TextContent);
        await cut.InvokeAsync(() => cut.Find("input[type=text]").Input(""));
        cut.WaitForAssertion(() => Assert.Equal(5, cut.FindAll(".permiso-fila").Count), TimeSpan.FromSeconds(10));
        Assert.False(Marcada(cut, "Importar tenants"));
        Assert.True(Marcada(cut, "Borrar usuarios"));
        Assert.True(Marcada(cut, "Exportar usuarios"));
    }

    [Fact]
    public async Task Sin_PERMISOS_EDITAR_es_de_solo_lectura()
    {
        var cut = await RenderAsync(UnContexto, "PERMISOS_VER");

        Assert.All(cut.FindAll(".permiso-fila input[type=checkbox]"), c => Assert.True(c.HasAttribute("disabled")));
        Assert.Empty(cut.FindAll(".seleccionar-visibles"));
        Assert.Empty(cut.FindAll(".barra-cambios"));
        Assert.Empty(cut.FindAll("a.editar-rol"));                                       // sin ROLES_VER no hay enlace
    }

    [Fact]
    public async Task Cambiar_de_app_con_cambios_pide_confirmar()
    {
        var cut = await RenderAsync(DosApps);
        await cut.InvokeAsync(() => Casilla(cut, "Importar tenants").Change(true));
        Assert.Single(Handler.Peticiones, p => p.StartsWith("GET /api/UsuariosPermisos/editor/2", StringComparison.Ordinal));

        var selectorApp = cut.FindComponents<MudSelect<int>>().Single(s => s.Instance.Label == "Aplicación");
        // el manejador espera la respuesta del diálogo: no se espera aquí o la prueba se bloquea
        var cambio = cut.InvokeAsync(() => selectorApp.Instance.ValueChanged.InvokeAsync(2));

        cut.WaitForAssertion(() => Assert.Contains("Hay 1 cambios sin guardar", cut.Markup), TimeSpan.FromSeconds(10));
        Assert.Single(Handler.Peticiones, p => p.StartsWith("GET /api/UsuariosPermisos/editor/2", StringComparison.Ordinal));

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog button").First(b => b.TextContent.Trim() == "Descartar").Click());
        await cambio;

        cut.WaitForAssertion(() => Assert.Equal(2, Handler.Peticiones.Count(p => p.StartsWith("GET /api/UsuariosPermisos/editor/2", StringComparison.Ordinal))), TimeSpan.FromSeconds(10));
        Assert.Empty(cut.FindAll(".barra-cambios"));
    }

    [Fact]
    public async Task Sin_contextos_avisa_y_no_pide_el_editor()
    {
        Handler.Responder("/api/UsuariosPermisos/contextos/2", "[]");
        await AutenticarAsync(1, 1, "sistema", "PERMISOS_VER", "PERMISOS_EDITAR");
        var cut = Render<TestHost>(p => p.AddChildContent<UsuarioPermisos>(c => c.Add(x => x.UserId, 2).Add(x => x.IsActive, true)));

        cut.WaitForAssertion(() => Assert.Contains("no tiene acceso activo", cut.Markup), TimeSpan.FromSeconds(10));
        Assert.DoesNotContain(Handler.Peticiones, p => p.Contains("/editor/", StringComparison.Ordinal));
        Assert.Empty(cut.FindAll(".permiso-fila"));
    }
}
