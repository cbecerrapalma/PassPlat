using System.Text.RegularExpressions;

namespace PassPlat.CBP.Architecture.Tests;

/// <summary>
/// S92 · P52 — los controladores de grupos siguen AGENTS.md §6: heredan BaseApiController,
/// responden con FromResult (sin <c>new { error }</c> propio), cada escritura exige su política
/// GRUPOS_* y crear un grupo tiene un solo dueño del commit (GrupoService.CrearAsync).
/// </summary>
public sealed class S92GruposControladoresTests
{
    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static string Leer(string controlador) =>
        File.ReadAllText(Path.Combine(RaizDelRepo(), "PassPlat.WebAPI", "Controllers", controlador));

    /// <summary>Verbo de cada acción de escritura y la política de la línea siguiente (null si no hay).</summary>
    private static List<(string Verbo, string? Politica)> AccionesDeEscritura(string fuente)
    {
        var lineas = fuente.Split('\n');
        var acciones = new List<(string, string?)>();
        for (var i = 0; i < lineas.Length; i++)
        {
            var verbo = Regex.Match(lineas[i], @"^\s*\[Http(Post|Put|Delete)\b");
            if (!verbo.Success) continue;
            var politica = i + 1 < lineas.Length ? Regex.Match(lineas[i + 1], @"\[Authorize\(Policy = ""(\w+)""\)\]") : Match.Empty;
            acciones.Add((verbo.Groups[1].Value, politica.Success ? politica.Groups[1].Value : null));
        }
        return acciones;
    }

    [Theory]
    [InlineData("GruposController.cs", "GruposController")]
    [InlineData("GruposUsuariosController.cs", "GruposUsuariosController")]
    public void S92_controladores_de_grupos_heredan_BaseApiController_y_no_arman_errores_propios(string archivo, string clase)
    {
        var fuente = Leer(archivo);

        Assert.Matches(new Regex($@"\[Authorize\(Policy = ""GRUPOS_VER""\)\]\s*public class {clase} : BaseApiController\b"), fuente);
        Assert.DoesNotContain("new { error", fuente);
        Assert.DoesNotContain("BadRequest(", fuente);
    }

    [Fact]
    public void S92_acciones_de_escritura_de_grupos_exigen_su_politica_GRUPOS()
    {
        Assert.Equal(
            new List<(string, string?)> { ("Post", "GRUPOS_CREAR"), ("Put", "GRUPOS_EDITAR"), ("Delete", "GRUPOS_ELIMINAR") },
            AccionesDeEscritura(Leer("GruposController.cs")));
        Assert.Equal(
            new List<(string, string?)> { ("Post", "GRUPOS_EDITAR"), ("Delete", "GRUPOS_EDITAR") },
            AccionesDeEscritura(Leer("GruposUsuariosController.cs")));
    }

    [Fact]
    public void S92_crear_grupo_no_repite_el_commit_del_servicio()
    {
        var fuente = Leer("GruposController.cs");
        var inicio = fuente.IndexOf("public async Task<IActionResult> Create(", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "No se encontro GruposController.Create");
        var fin = fuente.IndexOf("[Http", inicio, StringComparison.Ordinal);
        Assert.True(fin > inicio, "No se encontro la accion siguiente a Create");

        Assert.DoesNotContain("SaveChangesAsync", fuente[inicio..fin]);
    }
}
