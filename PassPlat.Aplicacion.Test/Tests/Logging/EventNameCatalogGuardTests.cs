using System.Text.RegularExpressions;
using CBP.Logging;

namespace PassPlat.Aplicacion.Test.Tests.Logging;

/// <summary>
/// S33.2 — Guard de gobernanza de EventName (T1–T6).
/// Precisiones contractuales del usuario (S33.1 aprobado):
///  - LoggingEvents.cs es la fuente de verdad EJECUTABLE (reflexión).
///  - Logging.EventCatalog.md es DOCUMENTACIÓN sincronizada (T5B), nunca fuente
///    de autorización de eventos.
///  - T6 debe entregar diagnóstico accionable (file+line+literal+constante sugerida).
/// </summary>
public class EventNameCatalogGuardTests
{
    private readonly IEventNameCatalogGuard _guard = new RoslynEventNameCatalogGuard();

    private static readonly Regex EventTokenPattern = new(@"^[A-Z][A-Za-z]*(?:_[A-Za-z]+)+$", RegexOptions.Compiled);

    private static (string PassPlatRoot, string CbpRoot) FindRoots()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var passPlat = Path.Combine(dir.FullName, "PassPlat");
            var cbp = Path.Combine(dir.FullName, "CBP");
            if (Directory.Exists(Path.Combine(passPlat, "PassPlat.Aplicacion")) &&
                Directory.Exists(Path.Combine(cbp, "CBP.Core")))
                return (passPlat, cbp);
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("No se encontró la raíz del workspace (PassPlat + CBP).");
    }

    private static string ResolveSource(string relativePath)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"No se encontró el archivo: {relativePath}");
    }

    private static string ResolveDoc(string relativePath)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"No se encontró el documento: {relativePath}");
    }

    /// <summary>T1 — La constante existe con el valor exacto.</summary>
    [Fact]
    public void T1_EventQueued_Constante_Existe_ConValorExacto()
    {
        Assert.True(LoggingEvents.EventQueued == "Event_Queued",
            $"LoggingEvents.EventQueued debe ser \"Event_Queued\" pero es \"{LoggingEvents.EventQueued}\".");

        Assert.True(_guard.CatalogByValue.TryGetValue("Event_Queued", out var suggested),
            "El catálogo por reflexión debe contener el valor \"Event_Queued\".");
        Assert.Equal("EventQueued", suggested);
    }

    /// <summary>T2 — IPService no usa el literal; usa la constante.</summary>
    [Fact]
    public void T2_IPService_NoUsaLiteral_UsaConstante()
    {
        var source = File.ReadAllText(ResolveSource(
            Path.Combine("PassPlat", "PassPlat.Aplicacion", "Services", "BBDD", "IPService.cs")));

        Assert.DoesNotContain("\"Event_Queued\"", source);
        Assert.Contains("LoggingEvents.EventQueued", source);
    }

    /// <summary>T3 — Cadena certificada de 5 eventos preservada como invariante (emisor→constante).</summary>
    [Fact]
    public void T3_CadenaCincoEventos_Preservada_ComoInvariante()
    {
        var ipService = File.ReadAllText(ResolveSource(
            Path.Combine("PassPlat", "PassPlat.Aplicacion", "Services", "BBDD", "IPService.cs")));
        var eventDispatcher = File.ReadAllText(ResolveSource(
            Path.Combine("CBP", "CBP.Core", "CBP.Events", "EventDispatcher.cs")));
        var emailQueue = File.ReadAllText(ResolveSource(
            Path.Combine("PassPlat", "PassPlat.Aplicacion", "Services", "Email", "EmailQueue.cs")));
        var emailService = File.ReadAllText(ResolveSource(
            Path.Combine("PassPlat", "PassPlat.Aplicacion", "Services", "Email", "PassPlatEmailService.cs")));

        Assert.Contains("LoggingEvents.EventQueued", ipService);
        Assert.Contains("LoggingEvents.EventPublished", eventDispatcher);
        Assert.Contains("LoggingEvents.EventHandled", eventDispatcher);
        Assert.Contains("LoggingEvents.EmailQueued", emailQueue);
        Assert.Contains("LoggingEvents.EmailSent", emailService);

        Assert.Equal("Event_Queued", LoggingEvents.EventQueued);
        Assert.Equal("Event_Published", LoggingEvents.EventPublished);
        Assert.Equal("Event_Handled", LoggingEvents.EventHandled);
        Assert.Equal("Email_Queued", LoggingEvents.EmailQueued);
        Assert.Equal("Email_Sent", LoggingEvents.EmailSent);
    }

    /// <summary>T4 — CorrelationId W3C intacto en IPService (no se tocó la cadena de correlación).</summary>
    [Fact]
    public void T4_IPService_CorrelationId_Intacto()
    {
        var source = File.ReadAllText(ResolveSource(
            Path.Combine("PassPlat", "PassPlat.Aplicacion", "Services", "BBDD", "IPService.cs")));

        Assert.Contains("LoggingPropertyNames.HttpCorrelationIdKey", source);
        Assert.Contains("[LoggingPropertyNames.CorrelationId] = corrId", source);
    }

    /// <summary>
    /// T5A — ENFORCEMENT. El catálogo del guard proviene exclusivamente de
    /// LoggingEvents (reflexión). Un literal cuyo valor ∈ catálogo se detecta
    /// SIEMPRE, sin depender de Logging.EventCatalog.md.
    /// </summary>
    [Fact]
    public void T5A_Enforcement_CatalogoReflexion_DetectaLiteralDelCatalogo()
    {
        Assert.True(_guard.CatalogByValue.ContainsKey("Event_Queued"),
            "El catálogo por reflexión debe contener Event_Queued (fuente de verdad LoggingEvents.cs).");
        Assert.True(_guard.CatalogByValue.ContainsKey("Cache_Hit"));
        Assert.True(_guard.CatalogByValue.ContainsKey("Event_Published"));

        var sampleDir = Path.Combine(Path.GetTempPath(), "s33.2-guard-sample");
        Directory.CreateDirectory(sampleDir);
        var sampleFile = Path.Combine(sampleDir, "SampleEmitter.cs");
        try
        {
            File.WriteAllText(sampleFile, """
                public class SampleEmitter
                {
                    public void Emit(object log)
                    {
                        // Violación contractual: literal del catálogo sin constante.
                        System.Console.WriteLine("fila 6");
                        var evt = new LogEvent { EventName = "Event_Queued" };
                    }
                }
                """);

            var violations = _guard.ScanFiles(new[] { sampleFile });
            var violation = Assert.Single(violations);

            Assert.Equal(sampleFile, violation.FilePath);
            Assert.Equal(7, violation.Line);
            Assert.Equal("Event_Queued", violation.LiteralValue);
            Assert.Equal("EventQueued", violation.SuggestedConstant);
        }
        finally
        {
            if (File.Exists(sampleFile)) File.Delete(sampleFile);
            if (Directory.Exists(sampleDir) && !Directory.EnumerateFileSystemEntries(sampleDir).Any())
                Directory.Delete(sampleDir);
        }
    }

    /// <summary>
    /// T5B — DOCUMENTACIÓN SINCRONIZADA (nunca fuente de autorización).
    /// Todo token de evento del catálogo está en Logging.EventCatalog.md y todo
    /// token marcado como evento en el documento existe en LoggingEvents.
    /// </summary>
    [Fact]
    public void T5B_Documentacion_Sincronizada_NoFuenteDeAutorizacion()
    {
        var docPath = ResolveDoc(Path.Combine("Docs", "Framework", "Logging", "Logging.EventCatalog.md"));
        var doc = File.ReadAllText(docPath);

        Assert.Contains("Event_Queued", doc);

        var docTokens = Regex.Matches(doc, @"`([^`\r\n]+)`")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var docEventTokens = docTokens.Where(t => EventTokenPattern.IsMatch(t)).ToList();

        var docOnly = docEventTokens.Except(_guard.CatalogByValue.Keys, StringComparer.Ordinal).ToList();
        Assert.True(docOnly.Count == 0,
            $"Documento 'autoriza' eventos que no existen en LoggingEvents (doc nunca es fuente): {string.Join(", ", docOnly)}");

        var catalogOnly = _guard.CatalogByValue.Keys
            .Where(k => !docTokens.Contains(k, StringComparer.Ordinal))
            .ToList();
        Assert.True(catalogOnly.Count == 0,
            $"Constantes de LoggingEvents sin documentar en EventCatalog.md: {string.Join(", ", catalogOnly)}");
    }

    /// <summary>
    /// T6 — Garantía principal: 0 violaciones en los árboles PassPlat + CBP,
    /// con diagnóstico accionable (file+line+literal+constante sugerida).
    /// </summary>
    [Fact]
    public void T6_CeroViolaciones_EnArboles_PassPlatYCBP_ConDiagnosticoAccionable()
    {
        var (passPlatRoot, cbpRoot) = FindRoots();

        var violations = _guard.Scan(passPlatRoot)
            .Concat(_guard.Scan(cbpRoot))
            .OrderBy(v => v.FilePath)
            .ThenBy(v => v.Line)
            .ToList();

        var report = violations.Count == 0
            ? "Sin violaciones de EventName."
            : string.Join(Environment.NewLine + Environment.NewLine, violations.Select(v => v.ToString()));

        Assert.True(
            violations.Count == 0,
            $"Se detectaron {violations.Count} literales de EventName del catálogo en el código:" +
            Environment.NewLine + report);

        Assert.NotEmpty(_guard.CatalogByValue);
    }
}