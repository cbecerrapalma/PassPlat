using Bunit;
using PassPlat.Web.Tests.Infra;
using UsuarioPermisos = PassPlat.Web.Pages.Usuarios.Components.UsuarioPermisos;

namespace PassPlat.Web.Tests;

// S103: la pestaña Permisos directos se unificó en UsuarioPermisos (editor por tenant y app); estas pruebas conservan su intención.
/// <summary>
/// S103 · D8 — pestaña Permisos: inactiva no pide nada; activa pide los contextos una sola vez y lista
/// los permisos; guardar confirma en un diálogo antes de escribir.
/// </summary>
public class S59RolesPermisosEstadoTests : WebTestBase
{
    private const string Contextos = """
    [{ "idTenant": 1, "tenantNombre": "PLATFORM", "idApp": 1, "appNombre": "AccessPlat", "appCodigo": "PASSPLAT", "roles": [{ "idRol": 3, "nombre": "Admin" }] }]
    """;

    private const string Editor = """
    { "idTenant": 1, "tenantNombre": "PLATFORM", "idApp": 1, "appNombre": "AccessPlat", "esSistema": false,
      "roles": [{ "idRol": 3, "nombre": "Admin" }],
      "permisos": [
        { "idPermiso": 10, "codigo": "USUARIOS_VER", "nombre": "Ver usuarios", "idModulo": 7, "moduloNombre": "Usuarios", "orden": 1, "porRol": false, "idTipoAsigDirecto": 1, "directoVigente": true, "tiene": true },
        { "idPermiso": 11, "codigo": "USUARIOS_EDITAR", "nombre": "Editar usuarios", "idModulo": 7, "moduloNombre": "Usuarios", "orden": 2, "porRol": true, "rolOrigen": "Admin", "idTipoAsigDirecto": 2, "directoVigente": true, "tiene": false }
      ] }
    """;

    public S59RolesPermisosEstadoTests()
    {
        Handler.Responder("/api/UsuariosPermisos/contextos/2", Contextos);
        Handler.Responder("/api/UsuariosPermisos/editor/2", Editor);
        Handler.Responder("/api/UsuariosPermisos/aplicar", """{ "concedidos": 0, "denegados": 0, "revocados": 1, "sinCambio": 0 }""");
    }

    private async Task<IRenderedComponent<TestHost>> RenderPestanaAsync(bool activa)
    {
        await AutenticarAsync(1, 1, "sistema", "PERMISOS_VER", "PERMISOS_EDITAR");
        return Render<TestHost>(p => p.AddChildContent<UsuarioPermisos>(c => c.Add(x => x.UserId, 2).Add(x => x.IsActive, activa)));
    }

    [Fact]
    public async Task Inactiva_no_pide_los_permisos()
    {
        var cut = await RenderPestanaAsync(activa: false);

        Assert.DoesNotContain(Handler.Peticiones, p => p.Contains("UsuariosPermisos"));
        Assert.DoesNotContain("Ver usuarios", cut.Markup);
    }

    [Fact]
    public async Task Activa_pide_los_contextos_una_vez_y_lista_los_permisos_con_su_origen()
    {
        var cut = await RenderPestanaAsync(activa: true);

        cut.WaitForAssertion(() => Assert.Contains("Ver usuarios", cut.Markup), TimeSpan.FromSeconds(10));
        Assert.Contains("Editar usuarios", cut.Markup);
        Assert.Contains("Agregado", cut.Markup);
        Assert.Contains("Quitado", cut.Markup);
        Assert.Single(Handler.Peticiones, p => p.StartsWith("GET /api/UsuariosPermisos/contextos/2"));
        Assert.Single(Handler.Peticiones, p => p.StartsWith("GET /api/UsuariosPermisos/editor/2"));
    }

    [Fact]
    public async Task Guardar_confirma_en_un_dialogo_antes_del_POST()
    {
        var cut = await RenderPestanaAsync(activa: true);
        cut.WaitForAssertion(() => Assert.Contains("Ver usuarios", cut.Markup), TimeSpan.FromSeconds(10));

        await cut.InvokeAsync(() => cut.FindAll(".permiso-fila").First(f => f.TextContent.Contains("Editar usuarios"))
            .QuerySelector("input[type=checkbox]")!.Change(true));
        await cut.InvokeAsync(() => cut.FindAll(".barra-cambios button").First(b => b.TextContent.Contains("Guardar cambios")).Click());

        cut.WaitForAssertion(() => Assert.Contains("Confirmar cambios de permisos", cut.Markup), TimeSpan.FromSeconds(10));
        Assert.DoesNotContain(Handler.Peticiones, p => p.StartsWith("POST"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=boton-guardar-dialogo]").Click());

        cut.WaitForAssertion(() => Assert.Contains(Handler.Peticiones, p => p.StartsWith("POST /api/UsuariosPermisos/aplicar")), TimeSpan.FromSeconds(10));
    }
}
