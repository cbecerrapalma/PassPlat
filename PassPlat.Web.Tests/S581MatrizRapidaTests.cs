using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PassPlat.Web.Tests.Infra;
using MatrizIndex = PassPlat.Web.Pages.MatrizPermisos.Index;

namespace PassPlat.Web.Tests;

/// <summary>
/// S58.1 · K1 (D1) — la matriz se dibuja una sola vez: los datos se indexan al
/// cargarlos y las celdas se buscan en un diccionario (sin reagrupar por celda).
/// </summary>
public class S581MatrizRapidaTests : WebTestBase
{
    private const int Roles = 16;
    private const int Permisos = 89;

    public S581MatrizRapidaTests()
    {
        Handler.Responder("/api/roles", RolesJson());
        Handler.Responder("/api/MatrizPermisos", MatrizJson());
    }

    private static string RolesJson()
    {
        var items = Enumerable.Range(1, Roles).Select(i => new
        {
            id = i,
            idTenant = i == 1 ? (int?)null : 3,
            codigo = $"ROL_{i:00}",
            nombre = $"Rol {i}",
            tenantNombre = i == 1 ? (string?)null : "Abarrotes del Sur",
            activo = true
        });
        return System.Text.Json.JsonSerializer.Serialize(items);
    }

    private static string MatrizJson()
    {
        var idsPermiso = Enumerable.Range(1, Permisos - 1).Append(210).ToArray();
        var filas = new List<object>();
        foreach (var p in idsPermiso)
        {
            var idModulo = p == 210 ? 210 : 100 + (p % 12);
            for (var r = 1; r <= Roles; r++)
            {
                filas.Add(new
                {
                    idRol = r,
                    rolNombre = $"Rol {r}",
                    rolCodigo = $"ROL_{r:00}",
                    idPermiso = p,
                    codigoPermiso = $"PERM_{p:000}",
                    nombrePermiso = $"Permiso {p}",
                    ordenPermiso = (byte)(p % 250),
                    idModulo,
                    nombreModulo = idModulo == 210 ? "FEDERACION.PROVEEDORES" : $"MODULO_{idModulo}",
                    codigoModulo = $"M{idModulo}",
                    ordenModulo = (byte)(idModulo % 250),
                    estado = "on",
                    esDirecto = true,
                    esHeredado = false
                });
            }
        }
        return System.Text.Json.JsonSerializer.Serialize(filas);
    }

    private async Task<IRenderedComponent<MatrizIndex>> RenderMatrizAsync()
    {
        await AutenticarAsync(1, 1, "sistema", "MATRIZ_PERMISOS_VER");
        return Render<MatrizIndex>();
    }

    [Fact]
    public async Task La_matriz_se_dibuja_sin_excepcion_con_1424_filas()
    {
        var cut = await RenderMatrizAsync();

        cut.WaitForAssertion(() => Assert.Contains("FEDERACION.PROVEEDORES", cut.Markup), TimeSpan.FromSeconds(15));
        Assert.Contains(Handler.Peticiones, p => p.Contains("GET /api/MatrizPermisos"));
    }

    [Fact]
    public async Task Un_modulo_y_un_permiso_con_el_mismo_Id_no_chocan()
    {
        var cut = await RenderMatrizAsync();

        cut.WaitForAssertion(() => Assert.Contains("FEDERACION.PROVEEDORES", cut.Markup), TimeSpan.FromSeconds(20));
        // El modulo 210 y el permiso 210 comparten Id: con claves distintas se dibujan
        // (el modulo aparece plegado, con su contador de permisos).
        Assert.Contains("MODULO_100", cut.Markup);
        Assert.True(cut.FindAll("tr").Count > 10);
    }

    [Fact]
    public async Task Los_datos_se_piden_una_sola_vez()
    {
        await RenderMatrizAsync();

        Assert.Single(Handler.Peticiones, p => p.Contains("GET /api/MatrizPermisos"));
        Assert.Single(Handler.Peticiones, p => p.Contains("GET /api/roles"));
    }

    [Fact]
    public void La_matriz_no_reagrupa_en_cada_lectura()
    {
        var pagina = LeerArchivo("Pages", "MatrizPermisos", "Index.razor");

        // El agrupado es un metodo (se llama una vez al cargar), no una propiedad
        // calculada que se reevalua en cada lectura.
        Assert.DoesNotContain("List<ModuloMatriz> _modulos =>", pagina);
        Assert.Contains("private void ConstruirIndice()", pagina);
        // La celda se busca en el indice, no recorriendo los modulos.
        Assert.Contains("_celdas.TryGetValue((idPermiso, idRol)", pagina);
        Assert.Contains("private void RecalcularVisibles()", pagina);
    }

    internal static string LeerArchivo(params string[] partes)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        var raiz = dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
        return File.ReadAllText(Path.Combine([raiz, "PassPlat.Web", .. partes]));
    }
}
