using System.Text.RegularExpressions;

namespace PassPlat.CBP.Architecture.Tests;

/// <summary>
/// S93 · P53 — <c>api/usuarios/page?miembroDe=</c>: un no-sistema queda en su propio tenant y,
/// con miembroDe, no se combina con el filtro por tenant de origen.
/// </summary>
public sealed class S93UsuariosPaginaMiembroTests
{
    private static string FuenteGetPaged()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        var raiz = dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
        var fuente = File.ReadAllText(Path.Combine(raiz, "PassPlat.WebAPI", "Controllers", "UsuariosController.cs"));
        var inicio = fuente.IndexOf("public async Task<IActionResult> GetPaged(", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "No se encontro GetPaged en UsuariosController");
        var fin = fuente.IndexOf("[HttpGet(\"nomusuario/{nomUsuario}\")]", inicio, StringComparison.Ordinal);
        return fin > inicio ? fuente[inicio..fin] : fuente[inicio..];
    }

    [Fact]
    public void S93_GetPaged_fuerza_miembroDe_al_tenant_de_la_sesion_para_un_no_sistema()
    {
        var metodo = FuenteGetPaged();

        Assert.Contains("[FromQuery] int? miembroDe", metodo);
        Assert.Matches(new Regex(@"miembroDe\.HasValue \? \(esSistema \? miembroDe : _tenantContext\.CurrentId\) : null"), metodo);
        Assert.Matches(new Regex(@"miembro\.HasValue \? null : \(esSistema \? idTenant : _tenantContext\.CurrentId\)"), metodo);
        Assert.Contains("ObtenerPaginadoFiltradoAsync(tenant, idApp, miembro, search, options, ct)", metodo);
    }
}
