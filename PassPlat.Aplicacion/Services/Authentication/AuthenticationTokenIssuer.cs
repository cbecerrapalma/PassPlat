using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CBP.Authentication.JwtBearer;
using CBP.Logging.Interfaces;
using CBP.Logging.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace PassPlat.Aplicacion.Services.Authentication;

public sealed class AuthenticationTokenIssuer
{
    private readonly IJwtTokenService _jwtService;
    private readonly JwtOptions _jwtOptions;
    private readonly ILoggerService _olog;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _config;
    // S57.19 H-2: audiencia por Apps.Codigo (autoridad). Compat: IdApp numérico legacy solo como fallback.
    private const string InventaCodigo = "INVENTA";
    private const string InventaStoreCodigo = "INVENTA_STORE";

    public AuthenticationTokenIssuer(
        IJwtTokenService jwtService,
        JwtOptions jwtOptions,
        ILoggerService olog,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration config)
    {
        _jwtService = jwtService;
        _jwtOptions = jwtOptions;
        _olog = olog;
        _httpContextAccessor = httpContextAccessor;
        _config = config;
    }

    public AuthenticationTokenGenerationResult Generate(AuthenticationContext context, IReadOnlyCollection<Claim> permisoClaims)
    {
        var jti = Guid.NewGuid().ToString();
        var claims = BuildIdentityClaims(context, jti);
        claims.AddRange(permisoClaims);

        // S57.19 H-2: audiencia por Apps.Codigo (autoridad). Compat: IdApp numérico legacy solo como fallback.
        string accessToken;
        var appCodigo = (context.AppCodigo ?? string.Empty).Trim().ToUpperInvariant();
        if (appCodigo == InventaCodigo || appCodigo == InventaStoreCodigo
            || (string.IsNullOrEmpty(appCodigo) && context.IdApp is 2 or 12 or 13))
        {
            var inventaAud = _config["Jwt:AudienceInventa"] ?? "InventaNet";
            accessToken = GenerateTokenWithAudience(claims, inventaAud);
        }
        else
        {
            accessToken = _jwtService.GenerateToken(claims);
        }
        var refreshToken = _jwtService.GenerateRefreshToken();
        var refreshHash = HashSHA256(refreshToken);
        var expiresAt = DateTime.Now.AddMinutes(_jwtOptions.RefreshTokenExpirationMinutes);

        _olog.LogInformation(new LogEvent
        {
            EventName = CBP.Logging.LoggingEvents.JwtGenerated,
            Scope = CBP.Logging.LoggingScopes.Authentication,
            Message = "JWT generado",
            Properties = new Dictionary<string, object?>
            {
                [CBP.Logging.LoggingPropertyNames.Category] = CBP.Logging.LoggingCategories.ApplicationAuth,
                [CBP.Logging.LoggingPropertyNames.Operation] = CBP.Logging.LoggingOperations.Execute,
                [CBP.Logging.LoggingPropertyNames.CorrelationId] = _httpContextAccessor.HttpContext?.Items[CBP.Logging.LoggingPropertyNames.HttpCorrelationIdKey] as string,
                [CBP.Logging.LoggingPropertyNames.UserId] = context.IdUsuario.ToString(),
                [CBP.Logging.LoggingPropertyNames.TenantId] = context.IdTenant,
            }
        });

        return new AuthenticationTokenGenerationResult(accessToken, refreshToken, refreshHash, jti, expiresAt);
    }

    private static List<Claim> BuildIdentityClaims(AuthenticationContext context, string jti)
    {
        // GAP-03: emitir claims duales (legacy + Inventa compat) para backward compatibility
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, context.IdUsuario.ToString()),
            new(JwtRegisteredClaimNames.Sub, context.IdUsuario.ToString()),
            new("IdApp", context.IdApp.ToString()),
            new(JwtRegisteredClaimNames.Jti, jti)
        };

        if (context.IdTenant.HasValue)
        {
            var tid = context.IdTenant.Value.ToString();
            claims.Add(new Claim("TenantId", tid));
            claims.Add(new Claim("tid", tid));
        }

        if (context.IdUsuarioTenant.HasValue)
            claims.Add(new Claim("UsuarioTenantId", context.IdUsuarioTenant.Value.ToString()));

        if (context.EsSistema)
            claims.Add(new Claim("is_system", "true"));

        return claims;
    }

    private string GenerateTokenWithAudience(IEnumerable<Claim> claims, string audience)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var enriched = claims.Append(new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)).ToList();
        var token = new JwtSecurityToken(
            issuer: string.IsNullOrEmpty(_jwtOptions.Issuer) ? null : _jwtOptions.Issuer,
            audience: audience,
            claims: enriched,
            expires: DateTime.Now.AddMinutes(_jwtOptions.ExpirationMinutes),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashSHA256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
