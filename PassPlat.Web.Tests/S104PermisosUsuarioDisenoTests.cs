using Bunit;
using MudBlazor;
using PassPlat.Web.Tests.Infra;
using RolDetail = PassPlat.Web.Pages.Roles.RolDetail;
using UsuarioPermisos = PassPlat.Web.Pages.Usuarios.Components.UsuarioPermisos;

namespace PassPlat.Web.Tests;

/// <summary>
/// S104 · D5-D6 — pestaña «Permisos» de un usuario en módulos plegados con grilla, progreso, casilla tri-estado
/// y chips de vista con contador; RolDetail sin ROLES_EDITAR no edita permisos. La lógica de S103 no cambia.
/// </summary>
public class S104PermisosUsuarioDisenoTests : WebTestBase
{
    private const string UnContexto = """
    [{ "idTenant": 1, "tenantNombre": "PLATFORM", "idApp": 1, "appNombre": "AccessPlat", "appCodigo": "PASSPLAT", "roles": [{ "idRol": 3, "nombre": "Admin" }] }]
    """;

    private const string DosApps = """
    [{ "idTenant": 1, "tenantNombre": "PLATFORM", "idApp": 1, "appNombre": "AccessPlat", "appCodigo": "PASSPLAT", "roles": [{ "idRol": 3, "nombre": "Admin" }] },
     { "idTenant": 1, "tenantNombre": "PLATFORM", "idApp": 2, "appNombre": "Inventa", "appCodigo": "INVENTA", "roles": [{ "idRol": 3, "nombre": "Admin" }] }]
    """;

    // mismo editor de S103: 10 por rol, 11 agregado, 12 por rol y quitado, 13 agregado vencido, 14 nada (módulo Tenants)
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

    private async Task<IRenderedComponent<TestHost>> RenderAsync(string contextos = UnContexto)
    {
        Handler.Responder("/api/UsuariosPermisos/contextos/2", contextos);
        Handler.Responder("/api/UsuariosPermisos/editor/2", Editor);
        await AutenticarAsync(1, 1, "sistema", "PERMISOS_VER", "PERMISOS_EDITAR", "ROLES_VER");
        var cut = Render<TestHost>(p => p.AddChildContent<UsuarioPermisos>(c => c.Add(x => x.UserId, 2).Add(x => x.IsActive, true)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".permiso-fila")), TimeSpan.FromSeconds(10));
        return cut;
    }

    private static int Plegados(IRenderedComponent<TestHost> cut) => cut.FindAll(".permiso-grilla[hidden]").Count;

    private static List<IRenderedComponent<MudCheckBox<bool?>>> CasillasDeGrupo(IRenderedComponent<TestHost> cut) =>
        [.. cut.FindComponents<MudCheckBox<bool?>>().Where(c => c.Instance.Class?.Contains("permiso-grupo-casilla") == true)];

    // el estado de la casilla sale del markup (clase mud-checkbox-true|false|null): leer .Value de un parámetro está prohibido (MUD0012)
    private static string Estado(IRenderedComponent<MudCheckBox<bool?>> casilla) =>
        System.Text.RegularExpressions.Regex.Match(casilla.Markup, @"mud-checkbox-(true|false|null)\b").Groups[1].Value;
    [Fact]
    public async Task Grupos_inician_plegados_y_se_abren_al_buscar_o_filtrar()
    {
        var cut = await RenderAsync();

        // dos módulos (Tenants, Usuarios), plegados; las filas siguen en el DOM dentro de la grilla oculta
        Assert.Equal(2, cut.FindAll(".permiso-grupo").Count);
        Assert.Equal(2, Plegados(cut));
        Assert.Equal(5, cut.FindAll(".permiso-fila").Count);

        // un clic en la cabecera abre solo ese módulo
        await cut.InvokeAsync(() => cut.FindAll(".permiso-grupo-cabecera")[1].Click());
        Assert.Equal(1, Plegados(cut));
        await cut.InvokeAsync(() => cut.FindAll(".permiso-grupo-cabecera")[1].Click());
        Assert.Equal(2, Plegados(cut));

        // al buscar se abren solos
        await cut.InvokeAsync(() => cut.Find("input[type=text]").Input("usuarios"));
        cut.WaitForAssertion(() => Assert.Equal(0, Plegados(cut)), TimeSpan.FromSeconds(10));
        await cut.InvokeAsync(() => cut.Find("input[type=text]").Input(""));
        cut.WaitForAssertion(() => Assert.Equal(2, Plegados(cut)), TimeSpan.FromSeconds(10));

        // y al filtrar por una vista distinta de «Todos»
        await cut.InvokeAsync(() => cut.FindAll(".vista-chip").First(c => c.TextContent.Contains("Excepciones")).Click());
        cut.WaitForAssertion(() => Assert.Equal(0, Plegados(cut)), TimeSpan.FromSeconds(10));
        await cut.InvokeAsync(() => cut.FindAll(".vista-chip").First(c => c.TextContent.Contains("Todos")).Click());
        cut.WaitForAssertion(() => Assert.Equal(2, Plegados(cut)), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Expandir_y_contraer_todo()
    {
        var cut = await RenderAsync();
        Assert.Equal("Expandir todo", cut.Find(".expandir-todo").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find(".expandir-todo").Click());
        Assert.Equal(0, Plegados(cut));
        Assert.Equal("Contraer todo", cut.Find(".expandir-todo").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find(".expandir-todo").Click());
        Assert.Equal(2, Plegados(cut));
        Assert.Equal("Expandir todo", cut.Find(".expandir-todo").TextContent.Trim());
    }

    [Fact]
    public async Task Casilla_de_grupo_tri_estado_marca_los_faltantes()
    {
        var cut = await RenderAsync();
        // los grupos van por nombre: Tenants (0/1) y Usuarios (2/4, parcial)
        var casillas = CasillasDeGrupo(cut);
        Assert.Equal(2, casillas.Count);
        Assert.Equal("false", Estado(casillas[0]));
        Assert.Equal("null", Estado(casillas[1]));
        Assert.Contains("2/4", cut.FindAll(".permiso-grupo-conteo")[1].TextContent);

        // parcial → marca los dos que faltan (Borrar y Exportar)
        await cut.InvokeAsync(() => casillas[1].Instance.ValueChanged.InvokeAsync(true));
        Assert.Equal(2, cut.FindAll(".permiso-cambio").Count);
        Assert.Equal("true", Estado(casillas[1]));
        Assert.Contains("4/4", cut.FindAll(".permiso-grupo-conteo")[1].TextContent);

        // todos marcados → desmarca el grupo: vuelven los dos originales y quedan como cambio Ver y Editar
        await cut.InvokeAsync(() => casillas[1].Instance.ValueChanged.InvokeAsync(false));
        
        Assert.Equal("false", Estado(casillas[1]));
        Assert.Equal(2, cut.FindAll(".permiso-cambio").Count);
        Assert.Contains("Cambios: +0, −2", cut.Find(".barra-cambios").TextContent);
    }

    [Fact]
    public async Task Chips_de_vista_muestran_contadores_y_filtran()
    {
        var cut = await RenderAsync();

        Assert.Equal(
            ["Todos (5)", "Los que tiene (2)", "Excepciones (3)", "Cambios sin guardar (0)"],
            cut.FindAll(".vista-chip").Select(c => System.Text.RegularExpressions.Regex.Replace(c.TextContent, @"\s+", " ").Trim()).ToList());

        await cut.InvokeAsync(() => cut.FindAll(".vista-chip").First(c => c.TextContent.Contains("Excepciones")).Click());
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".permiso-grilla:not([hidden]) .permiso-fila").Count), TimeSpan.FromSeconds(10));
        Assert.Equal(3, cut.FindAll(".permiso-fila").Count);

        // un cambio sube el contador de «Cambios sin guardar»
        await cut.InvokeAsync(() => cut.FindAll(".permiso-fila").First(f => f.TextContent.Contains("Editar usuarios")).QuerySelector("input[type=checkbox]")!.Change(false));
        Assert.Contains(cut.FindAll(".vista-chip"), c => c.TextContent.Contains("Cambios sin guardar (1)"));
    }

    [Fact]
    public async Task Un_solo_tenant_y_una_app_se_muestran_como_texto()
    {
        var cut = await RenderAsync();

        Assert.Empty(cut.FindComponents<MudSelect<int>>());
        var contexto = cut.FindAll(".permiso-contexto").Select(c => c.TextContent.Trim()).ToList();
        Assert.Contains("Tenant: PLATFORM", contexto);
        Assert.Contains("Aplicación: AccessPlat", contexto);

        // con dos apps el tenant sigue siendo texto y la aplicación pasa a selector (un solo TestHost por contexto)
        await DisposeComponentsAsync();
        var dos = await RenderAsync(DosApps);
        Assert.Contains(dos.FindAll(".permiso-contexto"), c => c.TextContent.Contains("Tenant: PLATFORM"));
        Assert.Equal("Aplicación", Assert.Single(dos.FindComponents<MudSelect<int>>()).Instance.Label);
    }

    [Fact]
    public async Task RolDetail_sin_ROLES_EDITAR_no_edita_permisos()
    {
        // S106 · D8 — la pestaña es el editor compartido: sin ROLES_EDITAR las casillas quedan deshabilitadas y no hay selección por acción
        const string Rol = """[{ "id": 1, "codigo": "CONSULTA", "nombre": "Consulta", "idTenant": 1, "tenantNombre": "PLATFORM", "activo": true }]""";
        Handler.Responder("/api/roles/para-tenant/1", Rol);
        Handler.Responder("/api/roles", Rol);
        Handler.Responder("/api/permisos/activos", """[{ "id": 1, "codigo": "USUARIOS_VER", "nombre": "Ver usuarios", "idModulo": 7, "orden": 1, "activo": true, "moduloNombre": "Usuarios", "idApp": 1, "appNombre": "AccessPlat" }]""");
        Handler.Responder("/api/permisos/rol/1", """[{ "idRol": 1, "idPermiso": 1 }]""");
        Handler.Responder("/api/accesos/rol/1", "[]");

        async Task<IRenderedComponent<RolDetail>> AbrirPermisosAsync(params string[] permisos)
        {
            await AutenticarAsync(1, 1, "sistema", permisos);
            var cut = Render<RolDetail>(p => p.Add(x => x.Id, 1));
            cut.WaitForAssertion(() => Assert.Contains(cut.FindAll(".mud-tab"), t => t.TextContent.Contains("Permisos")), TimeSpan.FromSeconds(15));
            await cut.InvokeAsync(() => cut.FindAll(".mud-tab").First(t => t.TextContent.Contains("Permisos")).Click());
            cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".permiso-fila")), TimeSpan.FromSeconds(10));
            return cut;
        }

        var sin = await AbrirPermisosAsync("ROLES_DETALLE_VER");
        Assert.All(sin.FindAll(".permiso-fila input[type=checkbox]"), c => Assert.True(c.HasAttribute("disabled")));
        Assert.Empty(sin.FindAll(".accion-casilla"));
        Assert.DoesNotContain(sin.FindAll("button"), b => b.TextContent.Contains("Gestionar Permisos"));

        var con = await AbrirPermisosAsync("ROLES_DETALLE_VER", "ROLES_EDITAR");
        Assert.All(con.FindAll(".permiso-fila input[type=checkbox]"), c => Assert.False(c.HasAttribute("disabled")));
        Assert.NotEmpty(con.FindAll(".accion-casilla"));
        Assert.DoesNotContain(con.FindAll("button"), b => b.TextContent.Contains("Gestionar Permisos"));
    }
}