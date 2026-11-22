using System.Text.RegularExpressions;

namespace PassPlat.CBP.Architecture.Tests;

/// <summary>
/// S65 — candado: los 500 escritos a mano en AuthController y ExternalAuthController no
/// devuelven el texto de la excepción; usan ErrorInterno (mensaje genérico + traceId, log).
/// </summary>
public sealed class S65SinMensajeDeExcepcionTests
{
    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static string Controlador(string nombre) =>
        File.ReadAllText(Path.Combine(RaizDelRepo(), "PassPlat.WebAPI", "Controllers", nombre));

    [Theory]
    [InlineData("AuthController.cs")]
    [InlineData("ExternalAuthController.cs")]
    public void S65_ningun_500_expone_el_mensaje_de_la_excepcion(string archivo)
    {
        var fuente = Controlador(archivo);
        var expuestos = Regex.Matches(fuente, @"StatusCode\(500[^;]*(?:\.Message|InnerException)[^;]*;")
            .Select(m => m.Value)
            .ToList();

        Assert.True(expuestos.Count == 0, $"{archivo}: " + string.Join(" | ", expuestos));
        Assert.Contains("ErrorInterno(", fuente);
    }
}
