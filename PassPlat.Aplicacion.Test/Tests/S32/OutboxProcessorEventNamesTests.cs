namespace PassPlat.Aplicacion.Test.Tests.S32;

/// <summary>
/// V-01 — OutboxProcessor emite eventos Background_Job* con las constantes del
/// catálogo CBP (LoggingEvents/LoggingScopes/LoggingCategories), nunca con
/// literales sin guion. Este test de Regresión de naming lee el archivo fuente
/// y verifica el contrato, para que dashboards sobre `Background_Job*` vean
/// siempre los eventos reales del procesador.
/// </summary>
public class OutboxProcessorEventNamesTests
{
    private static string BuscarFuente()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var candidato = Path.Combine(
                dir.FullName, "PassPlat", "PassPlat.Aplicacion",
                "Services", "Infrastructure", "OutboxProcessor.cs");
            if (File.Exists(candidato)) return candidato;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("No se encontró OutboxProcessor.cs en el árbol del proyecto.");
    }

    [Fact]
    public void EmitBgLog_UsaConstantesDelCatalogo_SinLiteralesSinGuion()
    {
        var content = File.ReadAllText(BuscarFuente());

        Assert.DoesNotContain("\"BackgroundJobStarted\"", content);
        Assert.DoesNotContain("\"BackgroundJobFinished\"", content);
        Assert.DoesNotContain("\"BackgroundJobFailed\"", content);

        Assert.Contains("LoggingEvents.BackgroundJobStarted", content);
        Assert.Contains("LoggingEvents.BackgroundJobFinished", content);
        Assert.Contains("LoggingEvents.BackgroundJobFailed", content);
    }

    [Fact]
    public void EmitBgLog_UsaScopeCategoriaYOperacionDelCatalogo()
    {
        var content = File.ReadAllText(BuscarFuente());

        Assert.Contains("LoggingScopes.BackgroundJobs", content);
        Assert.Contains("LoggingCategories.Background", content);
        Assert.Contains("LoggingOperations.Execute", content);
        Assert.Contains("LoggingPropertyNames.ElapsedMs", content);
    }
}