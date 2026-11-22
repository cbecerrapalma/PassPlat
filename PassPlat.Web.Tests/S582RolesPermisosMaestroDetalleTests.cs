using Bunit;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Web.Tests.Infra;
using RolPoliticaPwd = PassPlat.Web.Pages.Roles.Components.RolPoliticaPwd;

namespace PassPlat.Web.Tests;

// S102: la página maestro-detalle se retiró; ahora prueba la pestaña Política de RolDetail (RolPoliticaPwd).
/// <summary>
/// S102 · D1 — pestaña Política del rol: aviso sin política, nombre y requisitos con política,
/// quitar con confirmación y botones de escritura solo con POLITICASPWD_EDITAR.
/// </summary>
public class S582RolesPermisosMaestroDetalleTests : WebTestBase
{
    private static readonly RolDto Rol = new() { Id = 1, Codigo = "ADMIN", Nombre = "Administrador", Activo = true };

    private const string Politicas = """
    [{ "id": 3, "codigo": "FUERTE", "nombre": "Fuerte", "longMin": 12, "longMax": 64, "reqMayuscula": true, "reqMinuscula": true, "reqNumero": true, "reqEspecial": false, "maxIntentos": 5, "durBloqueoMin": 15, "diasVigencia": 90, "pwdRecordadas": 5 }]
    """;

    private const string ConPolitica = """
    [{ "id": 7, "idTenant": 1, "idRol": 1, "idPolitica": 3, "activo": true, "politicaNombre": "Fuerte" }]
    """;

    public S582RolesPermisosMaestroDetalleTests() => Handler.Responder("/api/politicaspwd", Politicas);

    private async Task<IRenderedComponent<TestHost>> RenderPestanaAsync(params string[] permisos)
    {
        await AutenticarAsync(1, 1, "sistema", permisos.Length > 0 ? permisos : ["POLITICASPWD_VER", "POLITICASPWD_EDITAR"]);
        var cut = Render<TestHost>(p => p.AddChildContent<RolPoliticaPwd>(c => c.Add(x => x.Rol, Rol).Add(x => x.IsActive, true)));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-skeleton")), TimeSpan.FromSeconds(15));
        return cut;
    }

    [Fact]
    public async Task Sin_politica_muestra_el_aviso_y_Asignar()
    {
        Handler.Responder("/api/rolespoliticaspwd/rol/1", "[]");

        var cut = await RenderPestanaAsync();

        Assert.Contains("Este rol no tiene política de contraseña asignada", cut.Markup);
        Assert.Contains("Asignar Política", cut.Markup);
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Trim() == "Quitar");
    }

    [Fact]
    public async Task Con_politica_muestra_su_nombre_Cambiar_y_Quitar()
    {
        Handler.Responder("/api/rolespoliticaspwd/rol/1", ConPolitica);

        var cut = await RenderPestanaAsync();

        Assert.Contains("Fuerte", cut.Markup);
        Assert.Contains("Cambiar Política", cut.Markup);
        Assert.Contains(cut.FindAll("button"), b => b.TextContent.Trim() == "Quitar");
    }

    [Fact]
    public async Task Con_politica_muestra_sus_requisitos()
    {
        Handler.Responder("/api/rolespoliticaspwd/rol/1", ConPolitica);

        var cut = await RenderPestanaAsync();

        Assert.Contains("Min 12 - Max 64", cut.Markup);
        Assert.Contains("Mayúscula", cut.Markup);
        Assert.Contains("MaxIntentos: 5", cut.Markup);
        Assert.Contains("Vigencia: 90 días", cut.Markup);
    }

    [Fact]
    public async Task Quitar_confirma_y_llama_a_desactivar()
    {
        Handler.Responder("/api/rolespoliticaspwd/rol/1", ConPolitica);
        Handler.Responder("/api/rolespoliticaspwd/7/desactivar", "{}");

        var cut = await RenderPestanaAsync();
        await cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Trim() == "Quitar").Click());

        cut.WaitForAssertion(() => Assert.Contains("¿Quitar la política 'Fuerte' de este rol?", cut.Markup), TimeSpan.FromSeconds(10));
        Assert.DoesNotContain(Handler.Peticiones, p => p.StartsWith("POST /api/rolespoliticaspwd/7/desactivar"));

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog button").First(b => b.TextContent.Trim() == "Quitar").Click());

        cut.WaitForAssertion(() => Assert.Contains(Handler.Peticiones, p => p.StartsWith("POST /api/rolespoliticaspwd/7/desactivar")), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Sin_POLITICASPWD_EDITAR_no_hay_botones_de_escritura()
    {
        Handler.Responder("/api/rolespoliticaspwd/rol/1", ConPolitica);

        var cut = await RenderPestanaAsync("POLITICASPWD_VER");

        Assert.Contains("Fuerte", cut.Markup);
        Assert.DoesNotContain("Cambiar Política", cut.Markup);
        Assert.DoesNotContain("Asignar Política", cut.Markup);
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Trim() == "Quitar");
    }
}
