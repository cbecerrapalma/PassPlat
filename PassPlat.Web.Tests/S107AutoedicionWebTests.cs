using Bunit;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Web.Tests.Infra;
using RolPermisos = PassPlat.Web.Pages.Roles.Components.RolPermisos;
using UsuarioPermisos = PassPlat.Web.Pages.Usuarios.Components.UsuarioPermisos;

namespace PassPlat.Web.Tests;

/// <summary>
/// S107 · D4 (P67) — la pestaña Permisos de la propia ficha y la de un rol que el usuario de la sesión tiene asignado
/// se ven en solo lectura, con un aviso; la de otro usuario y la de otro rol siguen editables.
/// </summary>
public class S107AutoedicionWebTests : WebTestBase
{
    private const string Contexto = """
    [{ "idTenant": 1, "tenantNombre": "PLATFORM", "idApp": 1, "appNombre": "AccessPlat", "appCodigo": "PASSPLAT", "roles": [{ "idRol": 3, "nombre": "Admin" }] }]
    """;

    private const string Editor = """
    { "idTenant": 1, "tenantNombre": "PLATFORM", "idApp": 1, "appNombre": "AccessPlat", "esSistema": false,
      "roles": [{ "idRol": 3, "nombre": "Admin" }],
      "permisos": [
        { "idPermiso": 10, "codigo": "APP_USUARIOS_VER", "nombre": "Ver usuarios", "idModulo": 7, "moduloNombre": "Usuarios", "orden": 1, "porRol": false, "directoVigente": false, "tiene": false },
        { "idPermiso": 11, "codigo": "APP_USUARIOS_EDITAR", "nombre": "Editar usuarios", "idModulo": 7, "moduloNombre": "Usuarios", "orden": 2, "porRol": false, "directoVigente": false, "tiene": false }
      ] }
    """;

    private const string Activos = """
    [{ "id": 10, "codigo": "APP_USUARIOS_VER", "nombre": "Ver usuarios", "idModulo": 7, "orden": 1, "activo": true, "moduloNombre": "Usuarios", "idApp": 1, "appNombre": "AccessPlat" },
     { "id": 11, "codigo": "APP_USUARIOS_EDITAR", "nombre": "Editar usuarios", "idModulo": 7, "orden": 2, "activo": true, "moduloNombre": "Usuarios", "idApp": 1, "appNombre": "AccessPlat" }]
    """;

    private static readonly RolDto Rol12 = new() { Id = 12, Codigo = "CONSULTA", Nombre = "VESTUARIO_CONSULTA", IdTenant = 1, Activo = true };

    private async Task<IRenderedComponent<TestHost>> RenderUsuarioAsync(int userId)
    {
        Handler.Responder($"/api/UsuariosPermisos/contextos/{userId}", Contexto);
        Handler.Responder($"/api/UsuariosPermisos/editor/{userId}", Editor);
        await AutenticarAsync(1, 1, "admin", "PERMISOS_VER", "PERMISOS_EDITAR");
        var cut = Render<TestHost>(p => p.AddChildContent<UsuarioPermisos>(c => c.Add(x => x.UserId, userId).Add(x => x.IsActive, true)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".permiso-fila")), TimeSpan.FromSeconds(10));
        return cut;
    }

    private async Task<IRenderedComponent<TestHost>> RenderRolAsync(int idRolDeLaSesion)
    {
        Handler.Responder("/api/permisos/activos", Activos);
        Handler.Responder("/api/permisos/rol/12", """[{ "idRol": 12, "idPermiso": 10 }]""");
        Handler.Responder("/api/accesos/usuario/1", Datos.AccesosJson((1, 1, idRolDeLaSesion, "Rol de la sesión")));
        await AutenticarAsync(1, 1, "admin", "ROLES_DETALLE_VER", "ROLES_EDITAR");
        var cut = Render<TestHost>(p => p.AddChildContent<RolPermisos>(c => c.Add(x => x.Rol, Rol12).Add(x => x.UsuariosConRol, 3)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".permiso-fila")), TimeSpan.FromSeconds(10));
        return cut;
    }

    [Fact]
    public async Task Ficha_propia_muestra_los_permisos_en_solo_lectura()
    {
        var cut = await RenderUsuarioAsync(1);

        Assert.NotEmpty(cut.FindAll(".autoedicion"));
        Assert.All(cut.FindAll(".permiso-fila input[type=checkbox]"), c => Assert.True(c.HasAttribute("disabled")));
    }

    [Fact]
    public async Task Ficha_de_otro_usuario_sigue_editable()
    {
        var cut = await RenderUsuarioAsync(2);

        Assert.Empty(cut.FindAll(".autoedicion"));
        Assert.All(cut.FindAll(".permiso-fila input[type=checkbox]"), c => Assert.False(c.HasAttribute("disabled")));
    }

    [Fact]
    public async Task Rol_propio_es_de_solo_lectura()
    {
        var cut = await RenderRolAsync(12);

        Assert.NotEmpty(cut.FindAll(".autoedicion"));
        Assert.All(cut.FindAll(".permiso-fila input[type=checkbox]"), c => Assert.True(c.HasAttribute("disabled")));
    }

    [Fact]
    public async Task Rol_ajeno_sigue_editable()
    {
        var cut = await RenderRolAsync(5);

        Assert.Empty(cut.FindAll(".autoedicion"));
        Assert.All(cut.FindAll(".permiso-fila input[type=checkbox]"), c => Assert.False(c.HasAttribute("disabled")));
    }
}
