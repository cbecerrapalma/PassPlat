using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PassPlat.Web.Tests.Infra;
using MatrizIndex = PassPlat.Web.Pages.MatrizPermisos.Index;

namespace PassPlat.Web.Tests;

/// <summary>
/// S58 · K2 (D2) — la matriz de permisos: un modulo y un permiso con el mismo Id
/// se dibujan sin excepcion, el selector de rol muestra «Nombre (CODIGO)» agrupado
/// por ambito y los datos se piden una sola vez.
/// </summary>
public class S58MatrizPermisosTests : WebTestBase
{
    private const string Roles = """
    [
      { "id": 1, "idTenant": null, "codigo": "ADMIN", "nombre": "Administrador", "activo": true },
      { "id": 2, "idTenant": 3, "codigo": "ADMIN_ABARROTES", "nombre": "Administrador", "tenantNombre": "Abarrotes del Sur", "activo": true }
    ]
    """;

    /// <summary>El modulo 210 y el permiso 210 chocan: la clave de fila debe ser distinta.</summary>
    private const string Matriz = """
    [
      { "idRol": 1, "rolNombre": "Administrador", "rolCodigo": "ADMIN", "idPermiso": 210, "codigoPermiso": "INVENTA_PRODUCTO_VER", "nombrePermiso": "Ver Productos", "ordenPermiso": 1, "idModulo": 210, "nombreModulo": "FEDERACION.PROVEEDORES", "codigoModulo": "FEDERACION", "ordenModulo": 1, "estado": "on", "esDirecto": true, "esHeredado": false },
      { "idRol": 2, "rolNombre": "Administrador", "rolCodigo": "ADMIN_ABARROTES", "idPermiso": 210, "codigoPermiso": "INVENTA_PRODUCTO_VER", "nombrePermiso": "Ver Productos", "ordenPermiso": 1, "idModulo": 210, "nombreModulo": "FEDERACION.PROVEEDORES", "codigoModulo": "FEDERACION", "ordenModulo": 1, "estado": "off", "esDirecto": false, "esHeredado": false }
    ]
    """;

    public S58MatrizPermisosTests()
    {
        Handler.Responder("/api/roles", Roles);
        Handler.Responder("/api/MatrizPermisos", Matriz);
    }

    private async Task<IRenderedComponent<MatrizIndex>> RenderMatrizAsync()
    {
        await AutenticarAsync(1, 1, "sistema", "MATRIZ_PERMISOS_VER");
        return Render<MatrizIndex>();
    }

    [Fact]
    public async Task Un_modulo_y_un_permiso_con_el_mismo_Id_se_dibujan_sin_excepcion()
    {
        var cut = await RenderMatrizAsync();

        cut.WaitForAssertion(() => Assert.Contains("FEDERACION.PROVEEDORES", cut.Markup));

        // Elegir un rol vuelve a pintar la tabla: con la misma clave de fila Blazor lanza
        // «More than one sibling of element 'tr' has the same key value».
        var botonRol = cut.FindAll("button.matrix-role-item").First(b => b.TextContent.Contains("ADMIN_ABARROTES"));
        await cut.InvokeAsync(() => botonRol.Click());

        cut.WaitForAssertion(() => Assert.Contains("Ver Productos", cut.Markup));
    }

    [Fact]
    public async Task El_selector_de_rol_muestra_nombre_codigo_y_ambito()
    {
        var cut = await RenderMatrizAsync();

        cut.WaitForAssertion(() => Assert.Contains("Global", cut.Markup));
        cut.WaitForAssertion(() => Assert.Contains("Abarrotes del Sur", cut.Markup));
        cut.WaitForAssertion(() => Assert.Contains("ADMIN_ABARROTES", cut.Markup));
    }

    [Fact]
    public async Task Los_datos_de_la_matriz_se_piden_una_sola_vez()
    {
        var cut = await RenderMatrizAsync();

        cut.WaitForAssertion(() => Assert.Contains("FEDERACION.PROVEEDORES", cut.Markup));
        Assert.Single(Handler.Peticiones, p => p.Contains("GET /api/MatrizPermisos"));
        Assert.Single(Handler.Peticiones, p => p.Contains("GET /api/roles"));
    }

    [Fact]
    public void Las_filas_de_modulo_y_de_permiso_usan_claves_distintas()
    {
        var raiz = RaizDelRepo();
        var pagina = File.ReadAllText(Path.Combine(raiz, "PassPlat.Web", "Pages", "MatrizPermisos", "Index.razor"));

        Assert.Contains("@key=\"@($\"m-{mod.IdModulo}\")\"", pagina);
        Assert.Contains("@key=\"@($\"p-{perm.IdPermiso}\")\"", pagina);
    }

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }
}
