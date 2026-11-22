using System.Text.RegularExpressions;

namespace PassPlat.CBP.Architecture.Tests;

/// <summary>
/// S65.1 — candado: ningún controlador lee el mensaje de la excepción de un <c>catch</c>
/// (<c>ex.Message</c>, <c>ex.InnerException?.Message</c>…). Recorrer <c>InnerException</c>
/// para clasificar el error está permitido; devolver o redirigir su texto, no.
/// </summary>
public sealed class S651SinTextoDeExcepcionEnControladoresTests
{
    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    [Fact]
    public void S651_ningun_controlador_usa_el_mensaje_de_la_excepcion()
    {
        var hallazgos = new List<string>();
        var carpeta = Path.Combine(RaizDelRepo(), "PassPlat.WebAPI", "Controllers");

        foreach (var archivo in Directory.GetFiles(carpeta, "*.cs", SearchOption.AllDirectories))
        {
            var fuente = File.ReadAllText(archivo);
            var variables = Regex.Matches(fuente, @"catch \(\w+ (\w+)\)").Select(m => m.Groups[1].Value).Distinct();
            foreach (var v in variables)
            {
                foreach (Match m in Regex.Matches(fuente, $@"\b{v}(?:\.InnerException\??)*\.Message\b"))
                {
                    var linea = fuente[..m.Index].Count(c => c == '\n') + 1;
                    hallazgos.Add($"{Path.GetFileName(archivo)}:{linea}");
                }
            }
        }

        Assert.True(hallazgos.Count == 0, "Texto de excepción en controladores: " + string.Join(", ", hallazgos));
    }
}
