using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace PassPlat.WebAPI.RateLimiting;

/// <summary>
/// S63: limitadores de peticiones particionados (por IP, por usuario o global) con tope global
/// opcional por política. Los valores vienen de la sección "RateLimiting" de appsettings.
/// <para>
/// IP: se usa <see cref="ConnectionInfo.RemoteIpAddress"/>. Detrás de un proxy hay que configurar
/// ForwardedHeaders con KnownProxies antes de confiar en ella (S63 D5); si no, todas las
/// peticiones compartirían la IP del proxy.
/// </para>
/// <para>
/// Usuario: requiere que app.UseRateLimiter() vaya después de la autenticación (S63 H10).
/// </para>
/// </summary>
public static class PassPlatRateLimiting
{
    public const string SectionName = "RateLimiting";

    public static IServiceCollection AddPassPlatRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(SectionName).Get<RateLimitingSettings>()
            ?? throw new InvalidOperationException($"Falta la sección '{SectionName}' en la configuración.");
        if (settings.Politicas.Count == 0)
            throw new InvalidOperationException($"'{SectionName}:Politicas' no define ninguna política.");

        var confiables = new HashSet<string>(
            settings.ClientesConfiables.Select(NormalizarIp).Where(ip => ip is not null)!,
            StringComparer.OrdinalIgnoreCase);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            foreach (var (nombre, politica) in settings.Politicas)
            {
                politica.Validar(nombre);
                options.AddPolicy(nombre, httpContext => Particion(httpContext, politica, confiables));
            }

            // Tope global por política: se aplica además del límite particionado, para acotar
            // los ataques repartidos entre muchas IP o cuentas.
            var topes = settings.Politicas
                .Where(p => p.Value.TopeGlobal is > 0)
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var nombre = PoliticaDelEndpoint(httpContext);
                if (nombre is not null && topes.TryGetValue(nombre, out var politica))
                {
                    return RateLimitPartition.GetFixedWindowLimiter($"tope:{nombre}", _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = politica.TopeGlobal!.Value,
                        Window = TimeSpan.FromSeconds(politica.WindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
                }
                return RateLimitPartition.GetNoLimiter("sin-tope");
            });

            options.OnRejected = (context, ct) => EscribirRechazoAsync(context, settings, ct);
        });

        return services;
    }

    /// <summary>Clave por IP: «ip:1.2.3.4» (IPv4 mapeada a IPv6 se normaliza).</summary>
    public static string ClaveIp(HttpContext httpContext) => $"ip:{NormalizarIp(httpContext.Connection.RemoteIpAddress) ?? "sin-ip"}";

    /// <summary>Clave por usuario autenticado («u:{NameIdentifier}»); si es anónimo, por IP.</summary>
    public static string ClaveUsuarioOIp(HttpContext httpContext)
    {
        var usuario = UsuarioAutenticado(httpContext);
        return usuario is null ? ClaveIp(httpContext) : $"u:{usuario}";
    }

    private static RateLimitPartition<string> Particion(HttpContext httpContext, PoliticaLimite politica, HashSet<string> confiables)
    {
        string clave;
        int limite;
        switch (politica.Particion)
        {
            case "global":
                clave = "global";
                limite = politica.PermitLimit;
                break;
            case "usuario":
                clave = ClaveUsuarioOIp(httpContext);
                limite = UsuarioAutenticado(httpContext) is null
                    ? politica.PermitLimitAnonimo ?? politica.PermitLimit
                    : politica.PermitLimit;
                break;
            default: // "ip"
                clave = ClaveIp(httpContext);
                var ip = NormalizarIp(httpContext.Connection.RemoteIpAddress);
                limite = ip is not null && confiables.Contains(ip)
                    ? politica.PermitLimitConfiable ?? politica.PermitLimit
                    : politica.PermitLimit;
                break;
        }

        var ventana = TimeSpan.FromSeconds(politica.WindowSeconds);
        return politica.Tipo == "deslizante"
            ? RateLimitPartition.GetSlidingWindowLimiter(clave, _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = limite,
                Window = ventana,
                SegmentsPerWindow = politica.Segments,
                QueueLimit = 0,
                AutoReplenishment = true
            })
            : RateLimitPartition.GetFixedWindowLimiter(clave, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limite,
                Window = ventana,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    }

    private static async ValueTask EscribirRechazoAsync(OnRejectedContext context, RateLimitingSettings settings, CancellationToken ct)
    {
        var httpContext = context.HttpContext;
        var nombre = PoliticaDelEndpoint(httpContext);

        var segundos = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? (int)Math.Ceiling(retryAfter.TotalSeconds)
            : nombre is not null && settings.Politicas.TryGetValue(nombre, out var p) ? p.WindowSeconds : 60;
        httpContext.Response.Headers.RetryAfter = Math.Max(1, segundos).ToString(CultureInfo.InvariantCulture);

        var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("PassPlat.RateLimiting");
        logger.LogWarning("Petición rechazada por límite: política {Politica}, IP {Ip}, ruta {Ruta}",
            nombre ?? "-", EnmascararIp(httpContext.Connection.RemoteIpAddress), httpContext.Request.Path.Value);

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc6585#section-4",
            title = "Too Many Requests",
            status = StatusCodes.Status429TooManyRequests,
            detail = "Demasiadas solicitudes. Intenta de nuevo en unos segundos.",
            instance = httpContext.Request.Path.Value,
            errorCode = "RATE_LIMITED",
            traceId = httpContext.TraceIdentifier
        }, options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json", ct);
    }

    // La política más específica gana: MVC agrega primero los atributos del controlador y luego los de la acción.
    private static string? PoliticaDelEndpoint(HttpContext httpContext) =>
        httpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;

    private static string? UsuarioAutenticado(HttpContext httpContext) =>
        httpContext.User?.Identity?.IsAuthenticated == true
            ? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            : null;

    private static string? NormalizarIp(string? ip) =>
        IPAddress.TryParse(ip, out var parsed) ? NormalizarIp(parsed) : null;

    private static string? NormalizarIp(IPAddress? ip)
    {
        if (ip is null) return null;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        return ip.ToString();
    }

    private static string EnmascararIp(IPAddress? ip)
    {
        var texto = NormalizarIp(ip);
        if (texto is null) return "sin-ip";
        var corte = texto.LastIndexOfAny(['.', ':']);
        return corte > 0 ? texto[..corte] + (texto[corte] == '.' ? ".x" : ":x") : "x";
    }
}

public sealed class RateLimitingSettings
{
    /// <summary>IP con cupo mayor (p. ej. el servidor de InventaNet). En producción se completa al desplegar.</summary>
    public List<string> ClientesConfiables { get; set; } = [];

    public Dictionary<string, PoliticaLimite> Politicas { get; set; } = new(StringComparer.Ordinal);
}

public sealed class PoliticaLimite
{
    /// <summary>"ip", "usuario" (con respaldo por IP si es anónimo) o "global".</summary>
    public string Particion { get; set; } = "ip";

    /// <summary>"fija" o "deslizante".</summary>
    public string Tipo { get; set; } = "fija";

    public int PermitLimit { get; set; }
    public int? PermitLimitConfiable { get; set; }
    public int? PermitLimitAnonimo { get; set; }
    public int WindowSeconds { get; set; } = 60;
    public int Segments { get; set; } = 6;
    public int? TopeGlobal { get; set; }

    public void Validar(string nombre)
    {
        if (Particion is not ("ip" or "usuario" or "global"))
            throw new InvalidOperationException($"RateLimiting:Politicas:{nombre}:Particion inválida ('{Particion}').");
        if (Tipo is not ("fija" or "deslizante"))
            throw new InvalidOperationException($"RateLimiting:Politicas:{nombre}:Tipo inválido ('{Tipo}').");
        if (PermitLimit <= 0 || WindowSeconds <= 0)
            throw new InvalidOperationException($"RateLimiting:Politicas:{nombre}: PermitLimit y WindowSeconds deben ser mayores que 0.");
    }
}
