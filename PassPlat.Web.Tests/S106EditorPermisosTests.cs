using System.Text.Json;
using Bunit;
using MudBlazor;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Web.Tests.Infra;
using RolPermisos = PassPlat.Web.Pages.Roles.Components.RolPermisos;
using UsuarioPermisos = PassPlat.Web.Pages.Usuarios.Components.UsuarioPermisos;

namespace PassPlat.Web.Tests;

/// <summary>
/// S106 · D3-D6 — el rol usa el mismo editor que el usuario (EditorPermisos), con selección por acción
/// (Leer · Crear · Editar · Eliminar · Otras) sobre los permisos visibles y guardado en una sola petición.
/// </summary>
public class S106EditorPermisosTests : WebTestBase
{
    private const string UnContexto = """
    [{ "idTenant": 1, "tenantNombre": "PLATFORM", "idApp": 1, "appNombre": "AccessPlat", "appCodigo": "PASSPLAT", "roles": [{ "idRol": 3, "nombre": "Admin" }] }]
    """;

    // usuario: ninguno marcado; dos _VER (10, 13), un EDITAR (11), un CREAR (12) y uno de «Otras» (14)
    private const string EditorUsuario = """
    { "idTenant": 1, "tenantNombre": "PLATFORM", "idApp": 1, "appNombre": "AccessPlat", "esSistema": false,
      "roles": [{ "idRol": 3, "nombre": "Admin" }],
      "permisos": [
        { "idPermiso": 10, "codigo": "APP_USUARIOS_VER", "nombre": "Ver usuarios", "idModulo": 7, "moduloNombre": "Usuarios", "orden": 1, "porRol": false, "directoVigente": false, "tiene": false },
        { "idPermiso": 11, "codigo": "APP_USUARIOS_EDITAR", "nombre": "Editar usuarios", "idModulo": 7, "moduloNombre": "Usuarios", "orden": 2, "porRol": false, "directoVigente": false, "tiene": false },
        { "idPermiso": 12, "codigo": "APP_USUARIOS_CREAR", "nombre": "Crear usuarios", "idModulo": 7, "moduloNombre": "Usuarios", "orden": 3, "porRol": false, "directoVigente": false, "tiene": false },
        { "idPermiso": 13, "codigo": "APP_TENANTS_VER", "nombre": "Ver tenants", "idModulo": 8, "moduloNombre": "Tenants", "orden": 1, "porRol": false, "directoVigente": false, "tiene": false },
        { "idPermiso": 14, "codigo": "APP_TENANTS_ADMIN", "nombre": "Administrar tenants", "idModulo": 8, "moduloNombre": "Tenants", "orden": 2, "porRol": false, "directoVigente": false, "tiene": false }
      ] }
    """;

    // rol 12 (tenant 3): mismos permisos, todos de AccessPlat; el rol solo tiene el 10
    private const string Activos = """
    [{ "id": 10, "codigo": "APP_USUARIOS_VER", "nombre": "Ver usuarios", "idModulo": 7, "orden": 1, "activo": true, "moduloNombre": "Usuarios", "idApp": 1, "appNombre": "AccessPlat" },
     { "id": 11, "codigo": "APP_USUARIOS_EDITAR", "nombre": "Editar usuarios", "idModulo": 7, "orden": 2, "activo": true, "moduloNombre": "Usuarios", "idApp": 1, "appNombre": "AccessPlat" },
     { "id": 13, "codigo": "APP_TENANTS_VER", "nombre": "Ver tenants", "idModulo": 8, "orden": 1, "activo": true, "moduloNombre": "Tenants", "idApp": 1, "appNombre": "AccessPlat" },
     { "id": 14, "codigo": "APP_TENANTS_ADMIN", "nombre": "Administrar tenants", "idModulo": 8, "orden": 2, "activo": true, "moduloNombre": "Tenants", "idApp": 1, "appNombre": "AccessPlat" }]
    """;

    // el mismo catálogo más un permiso de otra aplicación (Inventa)
    private static readonly string ActivosDosApps = Activos.TrimEnd().TrimEnd(']') +
        """, { "id": 20, "codigo": "INV_ARTICULOS_VER", "nombre": "Ver artículos", "idModulo": 9, "orden": 1, "activo": true, "moduloNombre": "Artículos", "idApp": 2, "appNombre": "Inventa" }]""";

    private static RolDto RolDe(int? idTenant) => new() { Id = 12, Codigo = "CONSULTA", Nombre = "VESTUARIO_CONSULTA", IdTenant = idTenant, Activo = true };

    private async Task<IRenderedComponent<TestHost>> RenderUsuarioAsync()
    {
        Handler.Responder("/api/UsuariosPermisos/contextos/2", UnContexto);
        Handler.Responder("/api/UsuariosPermisos/editor/2", EditorUsuario);
        await AutenticarAsync(1, 1, "sistema", "PERMISOS_VER", "PERMISOS_EDITAR");
        var cut = Render<TestHost>(p => p.AddChildContent<UsuarioPermisos>(c => c.Add(x => x.UserId, 2).Add(x => x.IsActive, true)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".permiso-fila")), TimeSpan.FromSeconds(10));
        return cut;
    }

    private async Task<IRenderedComponent<TestHost>> RenderRolAsync(int? idTenant, bool sistema, params string[] permisos) =>
        await RenderRolAsync(idTenant, sistema, Activos, permisos);

    private async Task<IRenderedComponent<TestHost>> RenderRolAsync(int? idTenant, bool sistema, string activos, string[] permisos)
    {
        Handler.Responder("/api/permisos/activos", activos);
        Handler.Responder("/api/permisos/rol/12", """[{ "idRol": 12, "idPermiso": 10 }]""");
        if (sistema) await AutenticarSistemaAsync(permisos);
        else await AutenticarAsync(1, 1, "admin", permisos);
        var cut = Render<TestHost>(p => p.AddChildContent<RolPermisos>(c => c.Add(x => x.Rol, RolDe(idTenant)).Add(x => x.UsuariosConRol, 3)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".permiso-fila")), TimeSpan.FromSeconds(10));
        return cut;
    }

    // JWT de prueba con is_system: la Web solo lee los claims; no es una clave real
    private async Task AutenticarSistemaAsync(string[] permisos)
    {
        static string B64(string texto) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(texto)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var payload = B64(JsonSerializer.Serialize(new Dictionary<string, object> { ["TenantId"] = "1", ["is_system"] = "true", ["permiso"] = permisos }));
        await Auth.RestoreSessionAsync($"{B64("""{"alg":"HS256","typ":"JWT"}""")}.{payload}.firma-de-prueba", null, 1, 1, null, "sistema");
    }

    private static AngleSharp.Dom.IElement Casilla(IRenderedComponent<TestHost> cut, string nombre) =>
        cut.FindAll(".permiso-fila").First(f => f.TextContent.Contains(nombre)).QuerySelector("input[type=checkbox]")!;

    private static AngleSharp.Dom.IElement CasillaAccion(IRenderedComponent<TestHost> cut, string clave) =>
        cut.Find($".accion-{clave} input[type=checkbox]");

    private static string Compacto(string texto) => System.Text.RegularExpressions.Regex.Replace(texto, @"\s+", " ").Trim();

    private static IEnumerable<string> Posts(FakeHttpHandler handler) =>
        handler.Peticiones.Where(p => p.StartsWith("POST /api/permisos/rol/12/aplicar", StringComparison.Ordinal));

    [Fact]
    public async Task Usuario_accion_Leer_marca_solo_los_VER_visibles()
    {
        var cut = await RenderUsuarioAsync();

        // la fila ofrece las cuatro acciones presentes; «Eliminar» no tiene permisos y no aparece
        Assert.Equal(["Leer (2)", "Crear (1)", "Editar (1)", "Otras (1)"],
            cut.FindAll(".accion-casilla").Select(c => Compacto(c.TextContent)).ToList());
        Assert.Empty(cut.FindAll(".accion-ELIMINAR"));

        await cut.InvokeAsync(() => CasillaAccion(cut, "VER").Change(true));

        Assert.Equal(2, cut.FindAll(".permiso-cambio").Count);
        Assert.Contains("Cambios: +2, −0", cut.Find(".barra-cambios").TextContent);
        // solo los permisos _VER quedan como cambio
        await cut.InvokeAsync(() => cut.FindAll(".vista-chip").First(c => c.TextContent.Contains("Cambios sin guardar")).Click());
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".permiso-fila").Count), TimeSpan.FromSeconds(10));
        Assert.All(cut.FindAll(".permiso-fila"), f => Assert.Contains("_VER", f.TextContent));
    }

    [Fact]
    public async Task Usuario_accion_con_todos_marcados_los_desmarca()
    {
        var cut = await RenderUsuarioAsync();

        await cut.InvokeAsync(() => CasillaAccion(cut, "VER").Change(true));
        Assert.Contains("Cambios: +2, −0", cut.Find(".barra-cambios").TextContent);

        // con todos los _VER marcados, el mismo clic los desmarca: vuelven a lo que tenían y no queda ningún cambio
        await cut.InvokeAsync(() => CasillaAccion(cut, "VER").Change(false));
        Assert.Empty(cut.FindAll(".barra-cambios"));
        Assert.Empty(cut.FindAll(".permiso-cambio"));
    }

    [Fact]
    public async Task Rol_muestra_el_mismo_editor_con_app_y_vistas()
    {
        var cut = await RenderRolAsync(3, false, "ROLES_DETALLE_VER", "ROLES_EDITAR");

        Assert.Contains(cut.FindAll(".permiso-contexto"), c => c.TextContent.Contains("Aplicación: AccessPlat"));
        Assert.Equal("Tiene 1 de 4 en esta aplicación", cut.Find(".resumen-permisos").TextContent.Trim());
        // las vistas del usuario menos «Excepciones», que no existe en un rol
        Assert.Equal(["Todos (4)", "Los que tiene (1)", "Cambios sin guardar (0)"], cut.FindAll(".vista-chip").Select(c => Compacto(c.TextContent)).ToList());
        Assert.Equal(2, cut.FindAll(".permiso-grupo").Count);
        Assert.Equal(4, cut.FindAll(".permiso-fila").Count);
        Assert.NotEmpty(cut.FindAll(".seleccionar-visibles"));
        Assert.Equal(["Leer (2)", "Editar (1)", "Otras (1)"], cut.FindAll(".accion-casilla").Select(c => Compacto(c.TextContent)).ToList());
        Assert.DoesNotContain("viene del rol", cut.Markup);
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Contains("Gestionar Permisos"));
    }

    [Fact]
    public async Task Rol_marca_y_guarda_un_solo_POST_aplicar_con_los_cambios()
    {
        Handler.Responder("/api/permisos/rol/12/aplicar", """{ "agregados": 1, "quitados": 1, "sinCambio": 0 }""");
        var cut = await RenderRolAsync(3, false, "ROLES_DETALLE_VER", "ROLES_EDITAR");

        await cut.InvokeAsync(() => Casilla(cut, "Editar usuarios").Change(true));    // el rol no lo tiene: se agrega
        await cut.InvokeAsync(() => Casilla(cut, "Ver usuarios").Change(false));      // el rol lo tiene: se quita
        Assert.Contains("Cambios: +1, −1", cut.Find(".barra-cambios").TextContent);
        Assert.Empty(Posts(Handler));                                                  // nada se escribe al marcar

        await cut.InvokeAsync(() => cut.FindAll(".barra-cambios button").First(b => b.TextContent.Contains("Guardar cambios")).Click());
        cut.WaitForAssertion(() => Assert.Contains("Se agregarán 1 y se quitarán 1 permisos del rol «VESTUARIO_CONSULTA». Afecta a 3 usuarios con este rol.", cut.Markup), TimeSpan.FromSeconds(10));
        Assert.Empty(Posts(Handler));                                                  // la confirmación no escribe
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog button").First(b => b.TextContent.Trim() == "Guardar").Click());

        cut.WaitForAssertion(() => Assert.Single(Posts(Handler)), TimeSpan.FromSeconds(10));
        var cuerpo = Posts(Handler).Single()["POST /api/permisos/rol/12/aplicar ".Length..];
        using var json = JsonDocument.Parse(cuerpo);
        var cambios = json.RootElement.GetProperty("cambios").EnumerateArray()
            .Select(c => (c.GetProperty("idPermiso").GetInt32(), c.GetProperty("tiene").GetBoolean())).Order().ToList();
        Assert.Equal([(10, false), (11, true)], cambios);
        // recarga lo asignado y limpia los cambios pendientes
        cut.WaitForAssertion(() => Assert.Equal(2, Handler.Peticiones.Count(p => p.StartsWith("GET /api/permisos/rol/12", StringComparison.Ordinal))), TimeSpan.FromSeconds(10));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".barra-cambios")), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Rol_global_sin_sistema_es_de_solo_lectura()
    {
        var cut = await RenderRolAsync(null, false, "ROLES_DETALLE_VER", "ROLES_EDITAR");

        Assert.Contains("Rol global: solo el usuario de sistema puede cambiar sus permisos", cut.Markup);
        Assert.All(cut.FindAll(".permiso-fila input[type=checkbox]"), c => Assert.True(c.HasAttribute("disabled")));
        Assert.Empty(cut.FindAll(".accion-casilla"));
        Assert.Empty(cut.FindAll(".seleccionar-visibles"));

        // el usuario de sistema sí lo edita
        await DisposeComponentsAsync();
        var sistema = await RenderRolAsync(null, true, "ROLES_DETALLE_VER", "ROLES_EDITAR");
        Assert.DoesNotContain("Rol global", sistema.Markup);
        Assert.All(sistema.FindAll(".permiso-fila input[type=checkbox]"), c => Assert.False(c.HasAttribute("disabled")));
        Assert.NotEmpty(sistema.FindAll(".accion-casilla"));
    }

    [Fact]
    public async Task Rol_cambiar_de_app_con_cambios_pide_confirmar()
    {
        var cut = await RenderRolAsync(3, false, ActivosDosApps, ["ROLES_DETALLE_VER", "ROLES_EDITAR"]);
        // dos aplicaciones: selector en vez de texto; arranca en la que el rol ya tiene algo (AccessPlat)
        var selector = Assert.Single(cut.FindComponents<MudSelect<int>>(), s => s.Instance.Label == "Aplicación");
        Assert.DoesNotContain("Ver artículos", cut.Markup);

        await cut.InvokeAsync(() => Casilla(cut, "Editar usuarios").Change(true));
        // el manejador espera la respuesta del diálogo: no se espera aquí o la prueba se bloquea
        var cambio = cut.InvokeAsync(() => selector.Instance.ValueChanged.InvokeAsync(2));
        cut.WaitForAssertion(() => Assert.Contains("Hay 1 cambios sin guardar", cut.Markup), TimeSpan.FromSeconds(10));

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog button").First(b => b.TextContent.Trim() == "Descartar").Click());
        await cambio;

        cut.WaitForAssertion(() => Assert.Contains("Ver artículos", cut.Markup), TimeSpan.FromSeconds(10));
        Assert.DoesNotContain("Ver usuarios", cut.Markup);
        Assert.Empty(cut.FindAll(".barra-cambios"));
    }

    [Fact]
    public async Task Rol_accion_Leer_respeta_la_busqueda()
    {
        var cut = await RenderRolAsync(3, false, "ROLES_DETALLE_VER", "ROLES_EDITAR");

        // «tenants» deja 2 de los 4: Leer cuenta solo el _VER visible (Ver tenants) y Otras, Administrar tenants
        await cut.InvokeAsync(() => cut.Find("input[type=text]").Input("tenants"));
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".permiso-fila").Count), TimeSpan.FromSeconds(10));
        Assert.Equal(["Leer (1)", "Otras (1)"], cut.FindAll(".accion-casilla").Select(c => Compacto(c.TextContent)).ToList());

        await cut.InvokeAsync(() => CasillaAccion(cut, "VER").Change(true));

        // solo «Ver tenants» (visible); «Ver usuarios» ya estaba marcado y «Editar usuarios» quedó fuera del filtro
        var cambio = Assert.Single(cut.FindAll(".permiso-cambio"));
        Assert.Contains("Se agregará", cambio.TextContent);
        Assert.Contains("Cambios: +1, −0", cut.Find(".barra-cambios").TextContent);
        await cut.InvokeAsync(() => cut.Find("input[type=text]").Input(""));
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll(".permiso-fila").Count), TimeSpan.FromSeconds(10));
        Assert.True(Casilla(cut, "Ver tenants").HasAttribute("checked"));
        Assert.False(Casilla(cut, "Editar usuarios").HasAttribute("checked"));
        Assert.False(Casilla(cut, "Administrar tenants").HasAttribute("checked"));
    }
}
