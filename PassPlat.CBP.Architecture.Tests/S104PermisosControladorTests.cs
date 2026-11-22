using System.Text.RegularExpressions;

namespace PassPlat.CBP.Architecture.Tests;

/// <summary>
/// S104 · D1 (P65) — <c>PermisosController</c>: leer con PERMISOS_VER (clase); crear, editar y eliminar
/// permisos con PERMISOS_CREAR / PERMISOS_EDITAR; asignar y desasignar permisos a un rol con ROLES_EDITAR.
/// </summary>
public sealed class S104PermisosControladorTests
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

    /// <summary>Verbo, nombre del método y política de la línea siguiente (null si no hay) de cada escritura.</summary>
    private static List<(string Verbo, string Metodo, string? Politica)> AccionesDeEscritura(string fuente)
    {
        var lineas = fuente.Split('\n');
        var acciones = new List<(string, string, string?)>();
        for (var i = 0; i < lineas.Length; i++)
        {
            var verbo = Regex.Match(lineas[i], @"^\s*\[Http(Post|Put|Delete)\b");
            if (!verbo.Success) continue;
            var politica = i + 1 < lineas.Length ? Regex.Match(lineas[i + 1], @"\[Authorize\(Policy = ""(\w+)""\)\]") : Match.Empty;
            var firma = i + 2 < lineas.Length ? Regex.Match(lineas[i + 2], @"Task<IActionResult>\s+(\w+)\(") : Match.Empty;
            acciones.Add((verbo.Groups[1].Value, firma.Success ? firma.Groups[1].Value : "?", politica.Success ? politica.Groups[1].Value : null));
        }
        return acciones;
    }

    [Fact]
    public void Permisos_escribe_con_CREAR_EDITAR_y_ROLES_EDITAR()
    {
        var fuente = Leer("PermisosController.cs");

        Assert.Matches(new Regex(@"\[Authorize\(Policy = ""PERMISOS_VER""\)\]\s*public class PermisosController : BaseApiController\b"), fuente);
        Assert.Equal(
            new List<(string, string, string?)>
            {
                ("Post", "Create", "PERMISOS_CREAR"),
                ("Post", "AsignarPermiso", "ROLES_EDITAR"),
                ("Post", "AplicarPermisosRol", "ROLES_EDITAR"),
                ("Put", "Update", "PERMISOS_EDITAR"),
                ("Delete", "DesasignarPermiso", "ROLES_EDITAR"),
                ("Delete", "Eliminar", "PERMISOS_EDITAR")
            },
            AccionesDeEscritura(fuente));
    }
}
