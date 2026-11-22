using System.Text.Json;
using System.Text.RegularExpressions;

namespace PassPlat.CBP.Architecture.Tests;

/// <summary>
/// S63 — los limitadores de peticiones se particionan (IP / usuario) desde la configuración:
/// ya no quedan contadores globales con nombre en <c>Program.cs</c>, las lecturas de
/// <c>AuthController</c> no comparten el cupo del login y toda política usada en un
/// controlador está definida en <c>appsettings.json</c>.
/// </summary>
public sealed class S63LimitadoresTests
{
    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static string WebApi(params string[] partes) =>
        Path.Combine([RaizDelRepo(), "PassPlat.WebAPI", .. partes]);

    [Fact]
    public void S63_Program_no_define_limitadores_globales_con_nombre()
    {
        var program = File.ReadAllText(WebApi("Program.cs"));

        Assert.DoesNotContain("AddFixedWindowLimiter(", program);
        Assert.DoesNotContain("AddSlidingWindowLimiter(", program);
        Assert.DoesNotContain("AddRateLimiter(", program);
        Assert.Contains("AddPassPlatRateLimiting(builder.Configuration)", program);
    }

    [Fact]
    public void S63_UseRateLimiter_va_despues_de_la_autenticacion()
    {
        var program = File.ReadAllText(WebApi("Program.cs"));

        var autenticacion = program.IndexOf("app.UseCbpAuthentication();", StringComparison.Ordinal);
        var limitador = program.IndexOf("app.UseRateLimiter();", StringComparison.Ordinal);
        Assert.True(autenticacion >= 0 && limitador > autenticacion,
            "app.UseRateLimiter() debe ir después de app.UseCbpAuthentication() para particionar por usuario");
    }

    [Fact]
    public void S63_AuthController_usa_AuthReadPolicy_a_nivel_de_clase()
    {
        var auth = File.ReadAllText(WebApi("Controllers", "AuthController.cs"));

        Assert.Matches(new Regex(@"\[EnableRateLimiting\(""AuthReadPolicy""\)\]\s*public class AuthController"), auth);
    }

    [Fact]
    public void S63_toda_politica_usada_esta_configurada_y_particionada()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(WebApi("appsettings.json")));
        var politicas = json.RootElement.GetProperty("RateLimiting").GetProperty("Politicas");
        var definidas = politicas.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

        var usadas = Directory.GetFiles(WebApi("Controllers"), "*.cs")
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"EnableRateLimiting\(""([^""]+)""\)").Select(m => m.Groups[1].Value))
            .Distinct()
            .ToList();

        Assert.NotEmpty(usadas);
        foreach (var nombre in usadas)
        {
            Assert.True(definidas.ContainsKey(nombre), $"La política '{nombre}' no está en appsettings.json");
            var particion = definidas[nombre].GetProperty("Particion").GetString();
            // Solo PurgePolicy es global a propósito (una purga por hora para toda la plataforma).
            if (nombre != "PurgePolicy")
                Assert.NotEqual("global", particion);
        }
    }
}
