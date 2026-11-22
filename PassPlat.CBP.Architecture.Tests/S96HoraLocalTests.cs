using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PassPlat.CBP.Architecture.Tests.S96;

/// <summary>
/// S96 · D2 y S96.1 · D2/D3 — el servidor trabaja en hora local y Outbox no lleva SQL en strings.
/// </summary>
public class S96HoraLocalTests
{
    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static IEnumerable<string> FuentesCs(string proyecto) =>
        Directory.EnumerateFiles(Path.Combine(RaizDelRepo(), proyecto), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(p => p is "obj" or "bin"));

    [Fact]
    public void Servidor_SinDateTimeUtcNow()
    {
        var conUtcNow = new[] { "PassPlat.Aplicacion", "PassPlat.Datos", "PassPlat.Dominio", "PassPlat.WebAPI" }
            .SelectMany(FuentesCs)
            .Where(f => File.ReadAllText(f).Contains("DateTime.UtcNow"))
            .ToList();

        Assert.Empty(conUtcNow);
    }

    [Fact]
    public void Datos_SinSysutcdatetime()
    {
        // Ningún literal de PassPlat.Datos nombra una función de fecha UTC (el modelo EF lo prueba S96ModeloHoraLocalTests)
        var conUtc = FuentesCs("PassPlat.Datos")
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), "\"(?<lit>[^\"\\r\\n]*)\"").Select(m => (Archivo: f, Literal: m.Groups["lit"].Value)))
            .Where(x => x.Literal.Contains("utc", StringComparison.OrdinalIgnoreCase)
                     && x.Literal.Contains("datetime", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(conUtc);
    }

    [Fact]
    public void OutboxRepository_SinExecuteSqlRaw()
    {
        var texto = File.ReadAllText(Path.Combine(RaizDelRepo(), "PassPlat.Datos", "Repositories", "OutboxRepository.cs"));

        // Sin vía de SQL crudo de EF Core, y los 5 updates (D3) por ExecuteUpdateAsync
        foreach (var crudo in new[]
                 {
                     nameof(RelationalDatabaseFacadeExtensions.ExecuteSqlRaw),
                     nameof(RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync),
                     nameof(RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolated),
                     nameof(RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync),
                     nameof(RelationalQueryableExtensions.FromSqlRaw),
                     nameof(RelationalQueryableExtensions.FromSqlInterpolated)
                 })
            Assert.DoesNotContain(crudo, texto);
        Assert.True(Regex.Matches(texto, @"\.ExecuteUpdateAsync\(").Count >= 5, "Los 5 updates de Outbox deben usar ExecuteUpdateAsync");
    }
}
