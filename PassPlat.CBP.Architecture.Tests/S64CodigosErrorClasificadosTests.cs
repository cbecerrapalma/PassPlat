using System.Text.RegularExpressions;

namespace PassPlat.CBP.Architecture.Tests;

/// <summary>
/// S64 — candado: todo código de error usado en PassPlat.Aplicacion y PassPlat.Datos
/// (<c>Failure("X", …)</c> o <c>new Error("X", …)</c>) debe estar en ClasificacionErrores,
/// sea como código de negocio o como técnico. Un código nuevo sin clasificar volvería a
/// responder 500 sin que nadie lo decida.
/// </summary>
public sealed class S64CodigosErrorClasificadosTests
{
    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static IEnumerable<string> Fuentes(string proyecto) =>
        Directory.GetFiles(Path.Combine(RaizDelRepo(), proyecto), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static HashSet<string> CodigosClasificados()
    {
        var tabla = File.ReadAllText(Path.Combine(RaizDelRepo(), "PassPlat.Aplicacion", "Errors", "ClasificacionErrores.cs"));
        return Regex.Matches(tabla, "\"([A-Za-z0-9_]+)\"").Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
    }

    [Fact]
    public void S64_todo_codigo_de_error_esta_clasificado()
    {
        var clasificados = CodigosClasificados();
        var sinClasificar = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var archivo in Fuentes("PassPlat.Aplicacion").Concat(Fuentes("PassPlat.Datos")))
        {
            if (archivo.EndsWith("ClasificacionErrores.cs", StringComparison.Ordinal))
                continue;
            // S116 · D6 — también Failure($"X_{n}", …): un código interpolado cuenta por su prefijo y nunca está clasificado
            foreach (Match m in Regex.Matches(File.ReadAllText(archivo), @"(?:Failure|new Error)\(\$?""([A-Za-z0-9_]+)[""{]"))
            {
                if (!clasificados.Contains(m.Groups[1].Value))
                    sinClasificar.Add($"{m.Groups[1].Value} ({Path.GetFileName(archivo)})");
            }
        }

        Assert.True(sinClasificar.Count == 0,
            "Códigos sin clasificar en ClasificacionErrores: " + string.Join(", ", sinClasificar));
    }

    [Fact]
    public void S64_el_filtro_esta_registrado()
    {
        var program = File.ReadAllText(Path.Combine(RaizDelRepo(), "PassPlat.WebAPI", "Program.cs"));
        Assert.Contains("Filters.Add(new ErrorCodigoHttpFilter())", program);
    }
}
