using System.Text.RegularExpressions;

namespace PassPlat.Aplicacion.Test.Tests.S41;

/// <summary>
/// Guard estático S41 (S41.3) — Garantiza que ningún EmailJob vuelva a construirse
/// con CorrelationId = Guid.NewGuid() bajo otra forma.
/// Permitidos (fuera del contrato EmailJob.CorrelationId):
///   - AuthenticationTokenIssuer jti (JWT)
///   - ExternalAuthService state OAuth2
///   - IdenExtTokens.CorrelationId (SP / columnas de tabla IdenExtTokens)
///   - IPService.cs:97 (ya cumple A+B: HttpContext ?? Guid)
///   - PassPlatEmailService TrackingId skipped
///   - EmailQueue.cs fallback local (política B)
/// </summary>
public class EmailJobCorrelationGuardTests
{
    private static readonly string[] ArchivosLimpieza =
    {
        Path.Combine("Services", "SPro", "AuthService.cs"),
        Path.Combine("Services", "SPro", "PasswordService.cs"),
        Path.Combine("Services", "SPro", "AccesoService.cs"),
        Path.Combine("Services", "BBDD", "UsuarioService.cs"),
        Path.Combine("Services", "SPro", "BloqueoService.cs"),
        Path.Combine("Services", "SPro", "MfaService.cs"),
        Path.Combine("Services", "SPro", "TokenRestService.cs"),
        Path.Combine("Services", "SPro", "IntentoAccesoService.cs"),
    };

    private static readonly Regex GuidPattern = new("Guid\\.NewGuid\\(\\)", RegexOptions.Compiled);

    private static string ResolveSource(string relativePath)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "PassPlat", "PassPlat.Aplicacion", relativePath);
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"No se encontró el archivo: {relativePath}");
    }

    /// <summary>G6 — Los 8 call-sites request-scoped no usan Guid.NewGuid() como CorrelationId de EmailJob.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void G6_CallSitesRequestScoped_NoUsanGuidNewGuid(int index)
    {
        var source = File.ReadAllText(ResolveSource(ArchivosLimpieza[index]));
        Assert.False(GuidPattern.IsMatch(source),
            $"{ArchivosLimpieza[index]} no debe contener Guid.NewGuid() (EmailJob.CorrelationId lo resuelve EmailQueue.EnqueueAsync).");
    }

    /// <summary>D1 — EmailQueue resuelve con fallback local (política B) y persiste en el job.</summary>
    [Fact]
    public void D1_EmailQueue_ResuelveConFallbackYPersisteEnJob()
    {
        var source = File.ReadAllText(ResolveSource(Path.Combine("Services", "Email", "EmailQueue.cs")));

        Assert.Contains("?? Guid.NewGuid().ToString(\"N\")", source);
        Assert.Contains("job with { CorrelationId = correlationId }", source);
        Assert.Contains("_channel.Writer.WriteAsync(job with { CorrelationId = correlationId }, ct)",
            source);
        Assert.DoesNotContain("WriteAsync(job, ct)", source);
    }
}