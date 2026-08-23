using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PassPlat.WebAPI.Middleware;

public sealed class DiagnosticAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DiagnosticAuthMiddleware> _logger;

    public DiagnosticAuthMiddleware(RequestDelegate next, ILogger<DiagnosticAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User;
        var identity = user.Identity;
        var isAuthenticated = identity?.IsAuthenticated ?? false;

        if (!isAuthenticated)
        {
            _logger.LogWarning("DIAGNOSTIC AUTH [BEFORE]: Path={Path} NOT AUTHENTICATED", context.Request.Path);
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        var hasBearer = authHeader?.StartsWith("Bearer ") == true;
        var rawToken = hasBearer ? authHeader![7..] : null;
        var tokenHash = rawToken != null ? ComputeSha256(rawToken) : "N/A";

        var claims = user.Claims.ToList();
        var permisoClaims = claims.Where(c => c.Type == "permiso").ToList();

        var tenantId = claims.FirstOrDefault(c => c.Type is "tenant_id" or "TenantId" or "id_tenant")?.Value;
        var idApp   = claims.FirstOrDefault(c => c.Type is "id_app" or "IdApp" or "app_id")?.Value;
        var sub     = claims.FirstOrDefault(c => c.Type is "sub" or "nameidentifier" or "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        var jti     = claims.FirstOrDefault(c => c.Type is "jti" or "Jti" or "JTI")?.Value;

        var endpoint = context.GetEndpoint();
        var authorizeAttrs = endpoint?.Metadata?.GetOrderedMetadata<IAuthorizeData>() ?? [];
        var allowAnon = endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null;

        _logger.LogWarning(
            "DIAGNOSTIC AUTH [BEFORE]: " +
            "Path={Path} Method={Method} " +
            "Auth={Auth} Type={AuthType} Name={Name} " +
            "Claims={Count} Permisos={PermCount} " +
            "Bearer={HasHeader} SHA256={Hash} " +
            "TenantId={Tenant}({TP}) IdApp={App}({AP}) " +
            "Sub={Sub}({SP}) Jti={Jti}({JP}) " +
            "Anon={Anon} Attrs={Attr} Endpoint={Ep} " +
            "Permisos=[{Perms}]",
            context.Request.Path, context.Request.Method,
            identity!.IsAuthenticated, identity.AuthenticationType ?? "-", identity.Name ?? "-",
            claims.Count, permisoClaims.Count,
            hasBearer, tokenHash,
            tenantId ?? "-", tenantId != null ? "+" : "-",
            idApp ?? "-", idApp != null ? "+" : "-",
            sub ?? "-", sub != null ? "+" : "-",
            jti ?? "-", jti != null ? "+" : "-",
            allowAnon,
            string.Join(",", authorizeAttrs.Select(a => a.Policy ?? a.Roles ?? "AuthOnly")),
            endpoint?.DisplayName ?? "-",
            string.Join(",", permisoClaims.Take(10).Select(c => c.Value)));

        await _next(context);
    }

    private static string ComputeSha256(string value)
    {
        if (string.IsNullOrEmpty(value)) return "empty";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
